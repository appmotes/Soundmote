using Soundmote.Core.Dsp;
using OggVorbisEncoder;
using Soundmote.Core.Sounds;

namespace Soundmote.Sandbox;

/// <summary>
/// Turns a recording into samples (S13), written as Ogg Vorbis without rumble below the high-pass. <see cref="Run"/>
/// makes a loop: the steadiest stretch of the asked length, its near drops held at most <see cref="CrestDb"/> above its
/// loudness, its peak at −1 dBFS. <see cref="Takes"/> makes one-shots: each strike, as of thunder, from just before it
/// to where it has died away. Both print what they measured, so a bump or a passing plane can be found by ear.
/// </summary>
internal static class Prepare
{
    private const double BlockSeconds = 0.5;
    private const int EdgeBlocks = 2;
    private const float PeakDb = -1f;

    /// <summary>How far a peak may stand above the stretch's loudness; at the −23 dBFS target a ring alone then peaks near −7 dBFS (S4).</summary>
    private const double CrestDb = 16;

    private const double LookSeconds = 0.0015;
    /// <summary>How fast the gain comes back after a peak: slow enough for a strike's tail, too slow for a dense bed (see Run).</summary>
    private const double ReleaseSeconds = 0.08;

    /// <summary>A loop lets go this fast, so only a loud drop is shaved and the bed under it is never ducked (S13).</summary>
    private const double LoopReleaseSeconds = 0.006;

    /// <summary>Vorbis's variable-bit-rate quality, −0.1 to 1: 0.6 is near 190 kbit/s for stereo.</summary>
    private const float Quality = 0.6f;

    private const int EncodeFrames = 1024;

    private const double EnvelopeSeconds = 0.05;
    private const double StrikeDb = 15;

    /// <summary>A strike must stand this far above the floor to be kept; a fainter one would lift the floor with it.</summary>
    private const double KeptDb = 20;
    private const double SettledDb = 4;
    private const double SettleSeconds = 1.5;
    private const double LeadSeconds = 0.25;
    private const double MinTakeSeconds = 3;
    private const double MaxTakeSeconds = 40;

    /// <summary>A take's loudness is that of its loudest span this long, the strike rather than the tail, set to <see cref="TakeDb"/>.</summary>
    private const double StrikeSeconds = 4;
    private const double TakeDb = -22;

    /// <summary>A crack may stand this far above its strike's loudness, so takes peak near −2 dBFS.</summary>
    private const double TakeCrestDb = 20;

    public static void Run(string input, string output, int seconds, float highPass, double? crestAt = null, double? release = null)
    {
        var crestDb = crestAt ?? CrestDb;
        var releaseSeconds = release ?? LoopReleaseSeconds;
        var sample = Sample.Read(File.ReadAllBytes(input));
        var rate = sample.Rate;
        var left = HighPass(sample.Left, highPass, rate);
        var right = HighPass(sample.Right, highPass, rate);

        var block = (int)(BlockSeconds * rate);
        var count = sample.Length / block;
        var loud = new double[count];
        var low = new double[count];
        var rumble = new Svf();
        rumble.Set(120, 0.707f, rate);
        for (var b = 0; b < count; b++)
        {
            double all = 0, under = 0;
            for (var i = b * block; i < (b + 1) * block; i++)
            {
                var mid = (left[i] + right[i]) / 2;
                var l = rumble.Low(mid);
                all += mid * mid;
                under += l * l;
            }
            loud[b] = Db(all / block);
            low[b] = Db(under / block) - loud[b];
        }

        var usable = count - 2 * EdgeBlocks;
        var want = Math.Min((int)(seconds / BlockSeconds), usable);
        if (want < 20) throw new ArgumentException($"{input} is too short: {sample.Length / (double)rate:0.0} s.");
        var lowAverage = low.Skip(EdgeBlocks).Take(usable).Average();
        var (start, score) = Enumerable.Range(EdgeBlocks, usable - want + 1)
            .Select(s => (s, Unsteadiness(loud.AsSpan(s, want), low.AsSpan(s, want), lowAverage)))
            .MinBy(x => x.Item2);

        var from = start * block;
        var length = want * block;
        left = left[from..(from + length)];
        right = right[from..(from + length)];
        var (crest, ducked) = Limit(left, right, rate, Rms(left, right, length), crestDb, releaseSeconds);
        var peak = 0f;
        for (var i = 0; i < length; i++) peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i])));
        var gain = MathF.Pow(10, PeakDb / 20) / peak;
        var clipped = sample.Left.Concat(sample.Right).Count(x => Math.Abs(x) >= 0.999f);
        var window = loud.AsSpan(start, want).ToArray();
        var floor = loud.Skip(EdgeBlocks).Take(usable).Order().ElementAt(usable / 10);

        for (var i = 0; i < length; i++)
        {
            left[i] *= gain;
            right[i] *= gain;
        }
        WriteOgg(output, left, right, rate);
        Console.WriteLine($"{Path.GetFileName(output)}: {length / (double)rate:0.0} s from {from / (double)rate:0.0} s of {sample.Length / (double)rate:0.0} s at {rate} Hz");
        Console.WriteLine($"  loudness {window.Min():0.0}…{window.Max():0.0} dB in the stretch, quietest tenth of the file {floor:0.0} dB");
        Console.WriteLine($"  below 120 Hz {low.AsSpan(start, want).ToArray().Max():0.0} dB at most, {lowAverage:0.0} dB on average; unsteadiness {score:0.00}");
        Console.WriteLine($"  peaks {crest:0.0} dB above the loudness, held to {crestDb:0} dB, {100 * ducked:0.0}% of the time held down 3 dB or more; gain {20 * Math.Log10(gain):+0.0;-0.0} dB to a −1 dBFS peak");
        Console.WriteLine($"  {clipped} clipped samples in the recording; hiss {Hiss(left, right, rate):0.0} dBFS above 6 kHz in the quietest tenth");
    }

    /// <summary>How much a stretch swells, jumps and rumbles: lower is steadier and cleaner.</summary>
    private static double Unsteadiness(ReadOnlySpan<double> loud, ReadOnlySpan<double> low, double lowAverage)
    {
        double sum = 0, squares = 0, max = double.MinValue, lowMax = double.MinValue;
        foreach (var x in loud) { sum += x; squares += x * x; max = Math.Max(max, x); }
        foreach (var x in low) lowMax = Math.Max(lowMax, x);
        var mean = sum / loud.Length;
        var spread = Math.Sqrt(Math.Max(0, squares / loud.Length - mean * mean));
        return spread + (max - mean) + Math.Max(0, lowMax - lowAverage - 3);
    }

    /// <summary>
    /// Lowers the gain just before each peak that stands more than <paramref name="crestDb"/> above
    /// <paramref name="loudness"/>, and lets it rise again over the release. The gain is the lowest one asked for within
    /// the look-ahead, averaged over the look-ahead, so it has reached the asked gain at the peak. Returns how far the
    /// highest peak stood above.
    /// </summary>
    internal static (double Crest, double Ducked) Limit(float[] left, float[] right, int rate, double loudness, double crestDb,
        double releaseSeconds = ReleaseSeconds)
    {
        var n = left.Length;
        var ceiling = (float)(loudness * Math.Pow(10, crestDb / 20));
        var asked = new float[n];
        var highest = 0f;
        for (var i = 0; i < n; i++)
        {
            var x = Math.Max(Math.Abs(left[i]), Math.Abs(right[i]));
            highest = Math.Max(highest, x);
            asked[i] = x > ceiling ? ceiling / x : 1f;
        }

        var look = Math.Max(1, (int)(LookSeconds * rate));
        var rise = 1f - MathF.Exp(-1f / (float)(releaseSeconds * rate));
        var held = new float[n];
        var g = 1f;
        for (var i = 0; i < n; i++)
        {
            var lowest = 1f;
            for (var j = i; j < Math.Min(n, i + look + 1); j++) lowest = Math.Min(lowest, asked[j]);
            g = Math.Min(lowest, g + (1f - g) * rise);
            held[i] = g;
        }
        // A running sum over the last look + 1 gains; before the start the gain is 1.
        double sum = look + 1;
        var down = 0;
        for (var i = 0; i < n; i++)
        {
            sum += held[i] - (i > look ? held[i - look - 1] : 1f);
            var smooth = (float)(sum / (look + 1));
            if (smooth < 0.708f) down++;
            left[i] *= smooth;
            right[i] *= smooth;
        }
        return (20 * Math.Log10(highest / ceiling) + crestDb, (double)down / n);
    }

    /// <summary>
    /// How loud the band above 6 kHz is in the quietest tenth of half-second blocks: in the pauses, that is mostly the
    /// recorder's own hiss, lifted with the rest. Lower is cleaner.
    /// </summary>
    private static double Hiss(float[] left, float[] right, int rate)
    {
        var filter = new Svf();
        filter.Set(6000, 0.707f, rate);
        var block = (int)(BlockSeconds * rate);
        var bands = new double[left.Length / block];
        for (var b = 0; b < bands.Length; b++)
        {
            double sum = 0;
            for (var i = b * block; i < (b + 1) * block; i++)
            {
                var h = filter.High((left[i] + right[i]) / 2);
                sum += h * h;
            }
            bands[b] = Db(sum / block);
        }
        return bands.Order().ElementAt(bands.Length / 10);
    }

    /// <summary>The loudness of <paramref name="frames"/> frames from <paramref name="from"/>, both ears together.</summary>
    internal static double Rms(float[] left, float[] right, int frames, int from = 0)
    {
        var to = Math.Min(from + frames, left.Length);
        double power = 0;
        for (var i = from; i < to; i++) power += (left[i] * left[i] + right[i] * right[i]) / 2;
        return Math.Sqrt(power / Math.Max(1, to - from));
    }

    private static float[] HighPass(float[] x, float hz, int rate)
    {
        var filter = new Svf();
        filter.Set(hz, 0.707f, rate);
        var y = new float[x.Length];
        for (var i = 0; i < x.Length; i++) y[i] = filter.High(x[i]);
        return y;
    }

    private static double Db(double power) => 10 * Math.Log10(power + 1e-12);

    /// <summary>
    /// Cuts up to <paramref name="count"/> strikes into <c>name-1.ogg</c>, <c>name-2.ogg</c>…: a strike starts where a
    /// quiet stretch is broken by <see cref="StrikeDb"/> or more above the recording's floor, and ends where it has stayed
    /// near the floor for <see cref="SettleSeconds"/>, at the next strike, or after <see cref="MaxTakeSeconds"/>. The
    /// loudest strikes are kept, in the order they came. Each is held at most <see cref="TakeCrestDb"/> above its loudest
    /// <see cref="StrikeSeconds"/> and set to the same loudness there, so takes of one sound strike alike (S12).
    /// </summary>
    public static void Takes(string input, string output, int count, float highPass)
    {
        var sample = Sample.Read(File.ReadAllBytes(input));
        var rate = sample.Rate;
        var left = HighPass(sample.Left, highPass, rate);
        var right = HighPass(sample.Right, highPass, rate);

        var block = (int)(EnvelopeSeconds * rate);
        var loud = new double[sample.Length / block];
        for (var b = 0; b < loud.Length; b++) loud[b] = Db(Math.Pow(Rms(left, right, block, b * block), 2));
        var floor = loud.Order().ElementAt(loud.Length / 5);
        var settle = (int)(SettleSeconds / EnvelopeSeconds);

        var strikes = new List<(int From, int To, double Top)>();
        var quiet = true;
        for (var b = 0; b < loud.Length; b++)
        {
            if (quiet && loud[b] > floor + StrikeDb)
            {
                var end = b + 1;
                var calm = 0;
                while (end < loud.Length && calm < settle && end - b < MaxTakeSeconds / EnvelopeSeconds)
                {
                    calm = loud[end] < floor + SettledDb ? calm + 1 : 0;
                    if (calm == 0 && loud[end] > floor + StrikeDb && loud[end - 1] < floor + SettledDb) break;
                    end++;
                }
                var from = Math.Max(0, b - (int)(LeadSeconds / EnvelopeSeconds));
                strikes.Add((from, end, loud[b..end].Max()));
                b = end - 1;
                quiet = false;
            }
            else if (loud[b] < floor + SettledDb) quiet = true;
        }

        var kept = strikes.Where(s => (s.To - s.From) * EnvelopeSeconds >= MinTakeSeconds && s.Top - floor >= KeptDb)
            .OrderByDescending(s => s.Top).Take(count).OrderBy(s => s.From).ToArray();
        if (kept.Length == 0) throw new ArgumentException($"{input} has no strike {StrikeDb:0} dB above its floor.");

        var name = output.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ? output[..^4] : output;
        Console.WriteLine($"{Path.GetFileName(input)}: {strikes.Count} strikes, {kept.Length} kept; floor {floor:0.0} dB");
        for (var k = 0; k < kept.Length; k++)
        {
            var (from, to, top) = kept[k];
            var takeLeft = left[(from * block)..Math.Min(to * block, left.Length)];
            var takeRight = right[(from * block)..Math.Min(to * block, right.Length)];
            Fade(takeLeft, takeRight, rate);
            var loudness = Loudest(takeLeft, takeRight, (int)(StrikeSeconds * rate), block);
            var (crest, _) = Limit(takeLeft, takeRight, rate, loudness, TakeCrestDb);
            var gain = (float)(Math.Pow(10, TakeDb / 20) / loudness);
            for (var i = 0; i < takeLeft.Length; i++)
            {
                takeLeft[i] *= gain;
                takeRight[i] *= gain;
            }
            var path = $"{name}-{k + 1}.ogg";
            WriteOgg(path, takeLeft, takeRight, rate);
            Console.WriteLine($"  {Path.GetFileName(path)}: {takeLeft.Length / (double)rate:0.0} s from {from * EnvelopeSeconds:0.0} s, " +
                              $"{top - floor:0} dB above the floor, peaks {crest:0.0} dB above the strike, gain {20 * Math.Log10(gain):+0.0;-0.0} dB, " +
                              $"hiss {Hiss(takeLeft, takeRight, rate):0.0} dBFS");
        }
    }

    /// <summary>The loudness of the loudest <paramref name="frames"/> in a take, looked for in steps of <paramref name="step"/>.</summary>
    private static double Loudest(float[] left, float[] right, int frames, int step)
    {
        var best = 0.0;
        for (var from = 0; from == 0 || from + frames <= left.Length; from += step) best = Math.Max(best, Rms(left, right, frames, from));
        return best;
    }

    /// <summary>A take's first 10 ms rise and its last second falls along a quarter cosine, so it starts and ends in silence.</summary>
    private static void Fade(float[] left, float[] right, int rate)
    {
        var rise = Math.Min(left.Length / 4, (int)(0.01 * rate));
        var fall = Math.Min(left.Length / 2, rate);
        for (var i = 0; i < rise; i++)
        {
            var g = (float)Math.Sin((i + 0.5) / rise * Math.PI / 2);
            left[i] *= g;
            right[i] *= g;
        }
        for (var i = 0; i < fall; i++)
        {
            var g = (float)Math.Cos((i + 0.5) / fall * Math.PI / 2);
            left[left.Length - fall + i] *= g;
            right[right.Length - fall + i] *= g;
        }
    }

    /// <summary>Encodes stereo Ogg Vorbis, written beside the file and moved into place in one step.</summary>
    internal static void WriteOgg(string path, float[] left, float[] right, int rate)
    {
        var info = VorbisInfo.InitVariableBitRate(2, rate, Quality);
        // A fixed serial number: the same recording makes the same file.
        var ogg = new OggStream(1);
        ogg.PacketIn(HeaderPacketBuilder.BuildInfoPacket(info));
        ogg.PacketIn(HeaderPacketBuilder.BuildCommentsPacket(new Comments()));
        ogg.PacketIn(HeaderPacketBuilder.BuildBooksPacket(info));

        var temporary = path + ".tmp";
        using (var file = File.Create(temporary))
        {
            Flush(ogg, file, force: true);
            var state = ProcessingState.Create(info);
            float[][] channels = [left, right];
            for (var at = 0; at < left.Length; at += EncodeFrames)
            {
                state.WriteData(channels, Math.Min(EncodeFrames, left.Length - at), at);
                Packets(state, ogg, file);
            }
            state.WriteEndOfStream();
            Packets(state, ogg, file);
            Flush(ogg, file, force: true);
        }
        File.Move(temporary, path, overwrite: true);
    }

    private static void Packets(ProcessingState state, OggStream ogg, Stream file)
    {
        while (!ogg.Finished && state.PacketOut(out var packet))
        {
            ogg.PacketIn(packet);
            Flush(ogg, file, force: false);
        }
    }

    private static void Flush(OggStream ogg, Stream file, bool force)
    {
        while (ogg.PageOut(out var page, force))
        {
            file.Write(page.Header);
            file.Write(page.Body);
        }
    }
}
