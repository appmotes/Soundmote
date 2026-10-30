using System.Globalization;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Formatting;

/// <summary>The words beside the dial: what a curve does, how long a scene is, the clock in the middle.</summary>
public static class SceneText
{
    /// <summary>"A 45-minute journey", "A 60-minute loop", "Follows the clock".</summary>
    public static string Summary(Scene scene) => scene.Kind switch
    {
        TimeKind.Journey => $"A {scene.Length.TotalMinutes:0}-minute journey",
        TimeKind.Loop => $"A {scene.Length.TotalMinutes:0}-minute loop",
        _ => "Follows the clock"
    };

    /// <summary>The kind of time in a library card: "Journey · 45 min", "Loop · 60 min", "Follows the clock".</summary>
    public static string Kind(Scene scene) => scene.Kind switch
    {
        TimeKind.Journey => $"Journey · {scene.Length.TotalMinutes:0} min",
        TimeKind.Loop => $"Loop · {scene.Length.TotalMinutes:0} min",
        _ => "Follows the clock"
    };

    /// <summary>One line for how a ring plays (D11): its shape in words, or its stretches, interval or rate.</summary>
    public static string Describe(Play play, Curve curve, TimeSpan length) => play switch
    {
        OnOffPlay { Stretches.Count: 0 } => "Off",
        OnOffPlay o => $"On {Times(o.Stretches.Count)} · {o.OnFor(length).TotalMinutes:0} min in all",
        EveryPlay e => e.First > TimeSpan.Zero ? $"Every {Minutes(e.Interval)} from {Clock(e.First)}" : $"Every {Minutes(e.Interval)}",
        RandomPlay r => $"{r.PerTenMinutes:0} per 10 min, random",
        _ => Describe(curve, length)
    };

    /// <summary>Where a ring is heard, in words (D15): "A little left", "Wanders around the right", "Crosses left to right".</summary>
    public static string Where(Place place) => place.Move switch
    {
        Move.Wander => $"Wanders around {Side(place.Pan, "the ")}",
        Move.Flit => $"Flits around {Side(place.Pan, "the ")}",
        Move.Cross => place.Path.Points.Count > 2 ? "Crosses and comes back"
            : place.Path.At(0) <= place.Path.At(1) ? "Crosses left to right" : "Crosses right to left",
        _ => Capital(Side(place.Pan, "in the "))
    };

    /// <summary>"middle", "a little left", "left", "far left"; <paramref name="article"/> goes before "middle".</summary>
    private static string Side(float pan, string article)
    {
        var side = pan < 0 ? "left" : "right";
        return Math.Abs(pan) switch
        {
            < 0.1f => article + "middle",
            < 0.45f => $"a little {side}",
            < 0.9f => side,
            _ => $"far {side}"
        };
    }

    private static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>"Distant sea and stream rise over 10 min before, and play until you stop them."</summary>
    public static string Morning(Wake wake) =>
        Capital(string.Join(" and ", wake.Layers.Select(l => l.Name.ToLowerInvariant())))
        + $" rise over {Minutes(wake.Rise)} before, and play until you stop them.";

    /// <summary>A time of day as "07:00".</summary>
    public static string Clock24(TimeSpan timeOfDay) => $"{(int)timeOfDay.TotalHours % 24:00}:{timeOfDay.Minutes:00}";

    public static string Minutes(TimeSpan t) => t.TotalMinutes >= 1 ? $"{t.TotalMinutes:0} min" : $"{t.TotalSeconds:0} s";

    private static string Times(int n) => n switch { 1 => "once", 2 => "twice", _ => $"{n} times" };

    /// <summary>One line for a ring's shape over the turn, from where its peak falls (D6).</summary>
    public static string Describe(Curve curve, TimeSpan length)
    {
        const int samples = 90;
        var values = Enumerable.Range(0, samples + 1).Select(i => curve.At((double)i / samples)).ToArray();
        var peak = values.Max();
        var low = values.Min();
        var peakAt = (double)Array.IndexOf(values, peak) / samples;

        if (peak - low < 0.15f) return "Steady";
        // A day has no beginning or end to settle from; say when the sound is strongest.
        if (length >= Day) return $"Most around {(int)Math.Round(peakAt * 24) % 24:00}:00";
        if (peakAt <= 0.1) return values[^1] < peak * 0.3f ? "Dies down slowly" : "Eases off";
        if (peakAt >= 0.9) return "Builds to the end";
        if (peakAt < 0.4)
        {
            var settles = Array.FindIndex(values, (int)(peakAt * samples), v => v < peak * 0.5f);
            if (settles > 0) return $"Settles after {RoundTo5((double)settles / samples * length.TotalMinutes)} min";
        }
        return values[^1] > peak * 0.6f ? "Rises and stays" : "Swells through the middle";
    }

    /// <summary>m:ss below an hour, h:mm:ss above.</summary>
    public static string Clock(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    public static string TimeOfDay(DateTimeOffset at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"10–30" in minutes, or "06–12" in hours of the day for a clock scene.</summary>
    public static string Range(Phase phase, TimeSpan length) => length >= Day
        ? $"{phase.From * 24:00}–{phase.To * 24:00}"
        : $"{phase.From * length.TotalMinutes:0}–{phase.To * length.TotalMinutes:0}";

    private static readonly TimeSpan Day = TimeSpan.FromDays(1);

    private static int RoundTo5(double minutes) => (int)(Math.Round(minutes / 5) * 5);
}
