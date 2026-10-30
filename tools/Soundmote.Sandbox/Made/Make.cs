using System.Text;
using Soundmote.Core.Sounds;

namespace Soundmote.Sandbox.Made;

/// <summary>
/// Renders the made sounds (S16, S18): each to a 32-bit WAV for listening, then to the Ogg Vorbis sample the app plays,
/// its peak at −1 dBFS. A pad's peaks are held as a recording's are (S13); a hit is left whole, its strike being the
/// sound. The length is kept to the frame.
/// </summary>
internal static class Make
{
    private const float PeakDb = -1f;

    /// <summary>How far a pad's peak may stand above its loudness: a pad is smooth.</summary>
    private const double PadCrestDb = 12;

    /// <summary>The frames a Vorbis decoder drops from the start of a stream before its first sound.</summary>
    private const int Priming = 1024;

    public static void Run(string wavFolder, string oggFolder, IReadOnlyCollection<string> only)
    {
        Directory.CreateDirectory(wavFolder);
        var sounds = Pads.All.Select(p => (p.Id, p.Make, Pad: true)).Concat(Hits.All.Select(h => (h.Id, h.Make, Pad: false)));
        foreach (var (id, make, pad) in sounds)
        {
            if (only.Count > 0 && !only.Contains(id)) continue;
            var s = make();
            if (pad) Prepare.Limit(s.Left, s.Right, Stereo.Rate, Prepare.Rms(s.Left, s.Right, s.Length), PadCrestDb);
            var peak = Math.Max(s.Left.Max(Math.Abs), s.Right.Max(Math.Abs));
            var gain = MathF.Pow(10, PeakDb / 20) / peak;
            for (var i = 0; i < s.Length; i++)
            {
                s.Left[i] *= gain;
                s.Right[i] *= gain;
            }
            WriteWav(Path.Combine(wavFolder, id + ".wav"), s.Left, s.Right, Stereo.Rate);
            // Frames go in front for the decoder to drop: a pad's last ones, as it loops, or silence before a hit, so its
            // strike is not lost. What plays is the WAV to the frame.
            var lead = pad ? (s.Left[^Priming..], s.Right[^Priming..]) : (new float[Priming], new float[Priming]);
            var ogg = Path.Combine(oggFolder, id + ".ogg");
            Prepare.WriteOgg(ogg, [.. lead.Item1, .. s.Left], [.. lead.Item2, .. s.Right], Stereo.Rate);
            using (var file = File.OpenRead(ogg))
            {
                var decoded = Sample.ReadOgg(file);
                if (decoded.Length != s.Length)
                    throw new InvalidDataException($"{id}.ogg plays {decoded.Length} frames, not {s.Length}.");
            }
            Console.WriteLine($"{id}: {s.Length / (double)Stereo.Rate:0.00} s, loudness {20 * Math.Log10(Prepare.Rms(s.Left, s.Right, s.Length)):0.0} dBFS");
        }
    }

    /// <summary>Stereo 32-bit float RIFF WAVE.</summary>
    private static void WriteWav(string path, float[] left, float[] right, int rate)
    {
        using var w = new BinaryWriter(File.Create(path), Encoding.ASCII);
        var data = left.Length * 8;
        w.Write("RIFF"u8);
        w.Write(36 + data);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)3);
        w.Write((short)2);
        w.Write(rate);
        w.Write(rate * 8);
        w.Write((short)8);
        w.Write((short)32);
        w.Write("data"u8);
        w.Write(data);
        for (var i = 0; i < left.Length; i++)
        {
            w.Write(left[i]);
            w.Write(right[i]);
        }
    }
}
