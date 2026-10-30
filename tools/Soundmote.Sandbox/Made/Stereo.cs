using Soundmote.Core.Dsp;

namespace Soundmote.Sandbox.Made;

/// <summary>Oscillator shapes for a made sound's notes (S16).</summary>
internal enum Shape { Sine, Triangle, Saw, Square, Organ, Glass }

/// <summary>
/// A stereo buffer a made sound is rendered into (S16), with the building blocks the pads and the hits share:
/// notes, noise, hits, filters, a chorus and a room. Offline only, so it may allocate freely.
/// </summary>
internal sealed class Stereo
{
    public const int Rate = 48000;

    private const int Block = 32;

    private Rng _rng;

    public Stereo(int frames, ulong seed)
    {
        Left = new float[frames];
        Right = new float[frames];
        _rng = new Rng(seed);
    }

    public float[] Left { get; }

    public float[] Right { get; }

    public int Length => Left.Length;

    public static double Hz(double midi) => 440 * Math.Pow(2, (midi - 69) / 12);

    /// <summary>
    /// Adds a held note: <paramref name="unison"/> oscillators spread up to <paramref name="detuneCents"/> apart and
    /// across the ears, each with its own slow vibrato, the whole following <paramref name="swell"/> (0–1) over time.
    /// </summary>
    public void Note(double midi, Shape shape, double level, Func<double, double> swell, int unison = 1,
        double detuneCents = 0, double vibratoCents = 0, double spread = 0.8)
    {
        for (var k = 0; k < unison; k++)
        {
            var side = unison == 1 ? 0 : 2.0 * k / (unison - 1) - 1;
            var hz = Hz(midi) * Math.Pow(2, side * detuneCents / 1200);
            var (toLeft, toRight) = Pan(side * spread);
            var gain = level / Math.Sqrt(unison);
            double phase = _rng.NextFloat();
            var vibHz = _rng.Range(0.15f, 0.4f);
            var vibPhase = _rng.NextFloat() * 2 * Math.PI;
            var modPhase = 0.0;
            for (var i = 0; i < Length; i++)
            {
                var t = (double)i / Rate;
                var dt = hz * Math.Pow(2, vibratoCents * Math.Sin(2 * Math.PI * vibHz * t + vibPhase) / 1200) / Rate;
                double y;
                switch (shape)
                {
                    case Shape.Saw:
                        y = 2 * phase - 1 - Blep(phase, dt);
                        break;
                    case Shape.Square:
                        y = (phase < 0.5 ? 1 : -1) + Blep(phase, dt) - Blep((phase + 0.5) % 1, dt);
                        y *= 0.7;
                        break;
                    case Shape.Triangle:
                        y = Additive(phase, dt, [1, 0, -1 / 9.0, 0, 1 / 25.0, 0, -1 / 49.0]);
                        break;
                    case Shape.Organ:
                        y = Additive(phase, dt, [1, 0.8, 0.45, 0.35, 0, 0.18, 0, 0.12]) * 0.6;
                        break;
                    case Shape.Glass:
                        // A sine bent by one three times its pitch, the bend breathing slowly: bright and bell-like, never struck.
                        var index = 0.9 + 0.7 * Math.Sin(2 * Math.PI * t / 17 + vibPhase);
                        y = Math.Sin(2 * Math.PI * phase + index * Math.Sin(2 * Math.PI * modPhase));
                        modPhase = (modPhase + 3 * dt) % 1;
                        break;
                    default:
                        y = Math.Sin(2 * Math.PI * phase);
                        break;
                }
                phase += dt;
                if (phase >= 1) phase -= 1;
                var s = (float)(y * gain * swell(t));
                Left[i] += s * toLeft;
                Right[i] += s * toRight;
            }
        }
    }

    /// <summary>Adds noise through a band-pass, its level following <paramref name="swell"/>; each ear its own.</summary>
    public void Air(float hz, float q, double level, Func<double, double> swell)
    {
        var left = new Svf();
        var right = new Svf();
        left.Set(hz, q, Rate);
        right.Set(hz, q, Rate);
        for (var i = 0; i < Length; i++)
        {
            var g = (float)(level * swell((double)i / Rate));
            Left[i] += left.Band(_rng.NextBipolar()) * g;
            Right[i] += right.Band(_rng.NextBipolar()) * g;
        }
    }

    /// <summary>
    /// Adds one hit at <paramref name="at"/> seconds: <paramref name="voice"/> gives its sound at each moment after
    /// the hit (white noise is passed in for it to shape), for <paramref name="seconds"/>. It wraps around the end, so
    /// a hit in the last bar rings on into the first.
    /// </summary>
    public void Hit(double at, double seconds, double level, double pan, Func<double, float, double> voice)
    {
        var (toLeft, toRight) = Pan(pan);
        var start = (int)Math.Round(at * Rate);
        var frames = (int)(seconds * Rate);
        for (var i = 0; i < frames; i++)
        {
            var s = (float)(voice((double)i / Rate, _rng.NextBipolar()) * level);
            var j = ((start + i) % Length + Length) % Length;
            Left[j] += s * toLeft;
            Right[j] += s * toRight;
        }
    }

    public float Random(float min, float max) => _rng.Range(min, max);

    public void LowPass(Func<double, double> cutoff, float q) => Filter(cutoff, q, (ref Svf f, float x) => f.Low(x));

    public void HighPass(double cutoff) => Filter(_ => cutoff, 0.707f, (ref Svf f, float x) => f.High(x));

    /// <summary>
    /// Sings the sound through a mouth: three formants that move from "ah" (0) to "oh" (1) as
    /// <paramref name="vowel"/> says, with some of the sound left as it was.
    /// </summary>
    public void Vowel(Func<double, double> vowel)
    {
        double[][] ah = [[800, 1, 6], [1150, 0.5, 7], [2900, 0.2, 10]];
        double[][] oh = [[450, 1, 6], [800, 0.6, 7], [2830, 0.15, 10]];
        foreach (var ear in new[] { Left, Right })
        {
            var bands = new Svf[3];
            var dry = (float[])ear.Clone();
            for (var i = 0; i < Length; i++)
            {
                if (i % Block == 0)
                {
                    var v = vowel((double)i / Rate);
                    for (var b = 0; b < 3; b++)
                        bands[b].Set((float)(ah[b][0] + (oh[b][0] - ah[b][0]) * v), (float)ah[b][2], Rate);
                }
                var x = dry[i];
                var y = 0.0;
                for (var b = 0; b < 3; b++) y += bands[b].Band(x) * ah[b][1];
                ear[i] = (float)(y * 1.6 + x * 0.25);
            }
        }
    }

    /// <summary>Two slowly swaying delays, one per ear, mixed in: a wider, thicker sound.</summary>
    public void Chorus(double mix, double depthMs = 4, double rateHz = 0.25)
    {
        var dryLeft = (float[])Left.Clone();
        var dryRight = (float[])Right.Clone();
        var baseDelay = 0.015 * Rate;
        var depth = depthMs / 1000 * Rate;
        for (var i = 0; i < Length; i++)
        {
            var w = 2 * Math.PI * rateHz * i / Rate;
            Left[i] = (float)((1 - mix) * dryLeft[i] + mix * Delayed(dryRight, i - baseDelay - depth * Math.Sin(w)));
            Right[i] = (float)((1 - mix) * dryRight[i] + mix * Delayed(dryLeft, i - baseDelay - depth * Math.Cos(w)));
        }
    }

    /// <summary>
    /// Adds a room <paramref name="seconds"/> long. With <paramref name="wrap"/> the room is played twice round the
    /// buffer, so the tail of the end lies over the start as in an endless loop.
    /// </summary>
    public void Room(float seconds, double mix, bool wrap = false, float damping = 0.4f)
    {
        var reverb = new Reverb(Rate, seconds, damping);
        var wetLeft = new float[Length];
        var wetRight = new float[Length];
        if (wrap) reverb.Process(Left, Right, wetLeft, wetRight);
        reverb.Process(Left, Right, wetLeft, wetRight);
        for (var i = 0; i < Length; i++)
        {
            Left[i] += (float)(wetLeft[i] * mix);
            Right[i] += (float)(wetRight[i] * mix);
        }
    }

    /// <summary>Rounds the loudest moments softly, as a warm amplifier does: fuller, never clipped.</summary>
    public void Warm(double drive)
    {
        var peak = Math.Max(Left.Max(Math.Abs), Right.Max(Math.Abs));
        if (peak == 0) return;
        var scale = drive / peak;
        var norm = Math.Tanh(drive);
        for (var i = 0; i < Length; i++)
        {
            Left[i] = (float)(Math.Tanh(Left[i] * scale) / norm * peak);
            Right[i] = (float)(Math.Tanh(Right[i] * scale) / norm * peak);
        }
    }

    /// <summary>A slow wave between 0 and 1 with a period of <paramref name="seconds"/>, starting at <paramref name="phase"/> (0–1).</summary>
    public static double Wave(double t, double seconds, double phase = 0) =>
        0.5 - 0.5 * Math.Cos(2 * Math.PI * (t / seconds + phase));

    private delegate float Step(ref Svf filter, float x);

    private void Filter(Func<double, double> cutoff, float q, Step step)
    {
        foreach (var ear in new[] { Left, Right })
        {
            var filter = new Svf();
            for (var i = 0; i < Length; i++)
            {
                if (i % Block == 0) filter.Set((float)cutoff((double)i / Rate), q, Rate);
                ear[i] = step(ref filter, ear[i]);
            }
        }
    }

    private static double Delayed(float[] x, double at)
    {
        if (at < 0) return 0;
        var i = (int)at;
        var k = at - i;
        return i + 1 < x.Length ? x[i] + (x[i + 1] - x[i]) * k : 0;
    }

    /// <summary>Sums sine harmonics up to just below the top of hearing at this rate, so nothing folds back.</summary>
    private static double Additive(double phase, double dt, double[] weights)
    {
        var y = 0.0;
        for (var h = 0; h < weights.Length && (h + 1) * dt < 0.45; h++)
            if (weights[h] != 0) y += weights[h] * Math.Sin(2 * Math.PI * (h + 1) * phase);
        return y;
    }

    /// <summary>The correction that takes the click out of a saw's or a square's jump (a polynomial band-limited step).</summary>
    private static double Blep(double t, double dt)
    {
        if (t < dt)
        {
            t /= dt;
            return t + t - t * t - 1;
        }
        if (t > 1 - dt)
        {
            t = (t - 1) / dt;
            return t * t + t + t + 1;
        }
        return 0;
    }

    private static (float Left, float Right) Pan(double pan)
    {
        var angle = (Math.Clamp(pan, -1, 1) + 1) * Math.PI / 4;
        return ((float)(Math.Sqrt(2) * Math.Cos(angle)), (float)(Math.Sqrt(2) * Math.Sin(angle)));
    }
}
