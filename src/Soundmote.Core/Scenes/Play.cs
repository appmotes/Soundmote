using Soundmote.Core.Dsp;

namespace Soundmote.Core.Scenes;

/// <summary>
/// How a ring plays (A11): as a shape that changes width over the turn, on and off in stretches, or as strikes
/// that come every so often or at random. Immutable; the renderer swaps a ring's play in one step.
/// </summary>
public abstract record Play
{
    /// <summary>The ring's curve decides the amount; the default.</summary>
    public static Play Shape { get; } = new ShapePlay();

    /// <summary>The moments a ring strikes over one turn, sorted. Empty for plays that are not strikes.</summary>
    public virtual Strike[] Strikes(TimeSpan length, ulong seed) => [];
}

public sealed record ShapePlay : Play;

/// <summary>A stretch of the turn, as positions from 0 to 1.</summary>
public readonly record struct Stretch(double From, double To);

/// <summary>On at one level inside its stretches, off outside, fading in and out at each end.</summary>
public sealed record OnOffPlay(IReadOnlyList<Stretch> Stretches, float Level = 0.7f, double FadeSeconds = 10) : Play
{
    public float At(double position, TimeSpan length)
    {
        var t = position * length.TotalSeconds;
        foreach (var s in Stretches)
        {
            var from = s.From * length.TotalSeconds;
            var to = s.To * length.TotalSeconds;
            if (t < from || t > to) continue;
            var fade = Math.Max(0.01, Math.Min(FadeSeconds, (to - from) / 2));
            return Level * (float)Math.Min(1, Math.Min((t - from) / fade, (to - t) / fade));
        }
        return 0f;
    }

    /// <summary>Total time on over one turn.</summary>
    public TimeSpan OnFor(TimeSpan length) => length * Stretches.Sum(s => s.To - s.From);
}

/// <summary>One strike: where on the turn and how hard, 0 to 1.</summary>
public readonly record struct Strike(double At, float Strength);

/// <summary>Strikes at a fixed interval, from <c>First</c> to the end of the turn.</summary>
public sealed record EveryPlay(TimeSpan Interval, TimeSpan First, float Strength = 0.8f) : Play
{
    public override Strike[] Strikes(TimeSpan length, ulong seed)
    {
        if (Interval <= TimeSpan.Zero || length <= TimeSpan.Zero) return [];
        var strikes = new List<Strike>();
        for (var t = First; t < length && strikes.Count < 10000; t += Interval)
            strikes.Add(new Strike(t / length, Strength));
        return strikes.ToArray();
    }
}

/// <summary>
/// Strikes that come when they like: about <c>PerTenMinutes</c> on average, busier where <c>Busy</c> is high, never
/// closer than <c>MinGap</c>, each a little stronger or weaker by up to <c>Vary</c>. The same seed gives the same
/// strikes, so the dial shows exactly what will be heard; a new seed shuffles them.
/// </summary>
public sealed record RandomPlay(double PerTenMinutes, TimeSpan MinGap, float Vary, Curve Busy, float Strength = 0.8f) : Play
{
    public override Strike[] Strikes(TimeSpan length, ulong seed)
    {
        var seconds = length.TotalSeconds;
        if (PerTenMinutes <= 0 || seconds <= 0) return [];
        // Thinning: candidates at the busiest rate, each kept with the curve's share of it.
        const int samples = 200;
        var busy = Enumerable.Range(0, samples + 1).Select(i => Math.Max(0.0, Busy.At((double)i / samples))).ToArray();
        var mean = busy.Average();
        var peak = busy.Max();
        if (mean <= 0) return [];
        // Strikes skipped for being too close lower the rate to λ / (1 + λ·gap); ask for more so the average holds.
        var rate = PerTenMinutes / 600.0;
        var dead = Math.Min(0.9, rate * MinGap.TotalSeconds);
        var peakRate = rate / (1 - dead) * peak / mean;

        var rng = new Rng(seed);
        var strikes = new List<Strike>();
        var last = double.NegativeInfinity;
        for (var t = 0.0; strikes.Count < 10000;)
        {
            t += -Math.Log(1 - rng.NextFloat() * 0.999999) / peakRate;
            if (t >= seconds) break;
            if (rng.NextFloat() * peak > Busy.At(t / seconds) || t - last < MinGap.TotalSeconds) continue;
            var strength = Math.Clamp(Strength * (1 + Vary * rng.NextBipolar()), 0.1f, 1f);
            strikes.Add(new Strike(t / seconds, strength));
            last = t;
        }
        return strikes.ToArray();
    }
}
