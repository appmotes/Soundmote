using System.Text.RegularExpressions;
using System.Globalization;
using System.Text.Json;

namespace Soundmote.Core.Recipes;

/// <summary>
/// A sound described as data (S10): named signals that move over time, and parts that make sound from them. Nothing in
/// a recipe is code; every expression and number is checked and bounded when the recipe is read.
/// </summary>
public sealed record Recipe(
    string Id,
    string Name,
    string Family,
    string Colour,
    string Character,
    float Room,
    float Glide,
    IReadOnlyList<SignalSpec> Signals,
    IReadOnlyList<PartSpec> Parts)
{
    public const int MaxSignals = 16;
    public const int MaxParts = 8;
    public const int MaxFilters = 3;
    public const int MaxVoices = 16;

    /// <summary>The gain that brings this recipe to the shared loudness (S4); measured when absent.</summary>
    public float? Trim { get; init; }

    /// <summary>The name of the sound's glyph (D10); the app falls back to its family's glyph.</summary>
    public string? Icon { get; init; }

    /// <summary>Where the sound belongs, from <see cref="Recipes.Family.Places"/> (S14).</summary>
    public IReadOnlyList<string> Places { get; init; } = [];

    /// <summary>How the sound feels, from <see cref="Recipes.Family.Moods"/> (S14).</summary>
    public IReadOnlyList<string> Moods { get; init; } = [];

    /// <summary>A sound that takes strikes joins a scene striking at random, as thunder, rather than every few minutes (S12).</summary>
    public bool StrikesAtRandom { get; init; }

    /// <summary>Plays something whole at each strike, as a bowl (S12): it belongs on a ring that plays every or random.</summary>
    public bool TakesStrikes => Parts.Any(p => p is SamplePart { TakesStrikes: true });

    /// <summary>Plays on the scene's beat (S18): the ring shows the scene's tempo.</summary>
    public bool KeepsBeat => Parts.Any(p => p is SamplePart { Pattern: not null });

    /// <summary>
    /// The names an expression may use, in slot order: <c>amount</c>, <c>ear</c>, <c>waver</c> (the ring's own knob, 0–1,
    /// S17), then every signal.
    /// </summary>
    public IReadOnlyList<string> Names => ["amount", "ear", "waver", .. Signals.Select(s => s.Name)];

    /// <summary>An expression reads <c>waver</c>, so the ring shows its Waver knob (S17).</summary>
    public bool Wavers { get; init; }

    /// <exception cref="RecipeException">The JSON is not a valid recipe; the message says what is wrong.</exception>
    public static Recipe Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new RecipeException($"Not valid JSON: {e.Message}");
        }
        using (doc)
        {
            var root = doc.RootElement;
            var id = Text(root, "id");
            var names = new List<string> { "amount", "ear", "waver" };

            var signals = new List<SignalSpec>();
            foreach (var s in Array(root, "signals", optional: true))
            {
                var signal = ReadSignal(s, names);
                if (names.Contains(signal.Name)) throw new RecipeException($"The name '{signal.Name}' is used twice.");
                names.Add(signal.Name);
                signals.Add(signal);
            }
            if (signals.Count > MaxSignals) throw new RecipeException($"At most {MaxSignals} signals.");

            var parts = Array(root, "parts", optional: false).Select(p => ReadPart(p, names)).ToList();
            if (parts.Count is 0 or > MaxParts) throw new RecipeException($"A recipe needs 1 to {MaxParts} parts.");

            var family = Text(root, "family");
            if (Recipes.Family.All.All(f => f.Id != family))
                throw new RecipeException($"Unknown family '{family}'. Use one of: {string.Join(", ", Recipes.Family.All.Select(f => f.Id))}.");
            return new Recipe(id, Text(root, "name"), family, ReadColour(root), Text(root, "character", ""),
                Number(root, "room", 0.15f, 0f, 1f), Number(root, "glide", 0.2f, 0.01f, 5f), signals, parts)
            {
                Trim = root.TryGetProperty("trim", out _) ? Number(root, "trim", 1f, 0.01f, 50f) : null,
                Icon = root.TryGetProperty("icon", out _) ? Text(root, "icon") : null,
                Places = Words(root, "places", Recipes.Family.Places),
                Moods = Words(root, "moods", Recipes.Family.Moods),
                // The name is reserved, so it can only stand for itself; a file name holds it only between hyphens.
                Wavers = Regex.IsMatch((root.TryGetProperty("signals", out var sig) ? sig.GetRawText() : "") + root.GetProperty("parts").GetRawText(),
                    @"(?<![\w-])waver(?![\w-])"),
                StrikesAtRandom = Text(root, "strikes", "every") switch
                {
                    "every" => false,
                    "random" => true,
                    var s => throw new RecipeException($"'strikes' is every or random, not '{s}'.")
                }
            };
        }
    }

    private static SignalSpec ReadSignal(JsonElement s, List<string> names)
    {
        var name = Text(s, "name");
        if (!IsName(name)) throw new RecipeException($"'{name}' is not a usable name: letters, digits and _, starting with a letter.");
        var perEar = s.TryGetProperty("perEar", out var e) && e.ValueKind == JsonValueKind.True;
        if (s.TryGetProperty("walk", out var walk))
            return new WalkSignal(name, perEar, Clamp(walk.GetSingle(), 0.02f, 120f));
        if (s.TryGetProperty("wave", out var wave))
        {
            var (min, max) = Range(s, "wave", 1f, 60f);
            var (sizeMin, sizeMax) = s.TryGetProperty("size", out _) ? Range(s, "size", 0f, 1f) : (1f, 1f);
            return new WaveSignal(name, perEar, min, max, Number(s, "rise", 0.33f, 0.05f, 0.95f), sizeMin, sizeMax, Number(s, "floor", 0f, 0f, 1f));
        }
        if (s.TryGetProperty("lag", out _))
            return new LagSignal(name, perEar, Checked(s, "lag", names), Number(s, "seconds", 0.5f, 0.01f, 30f));
        if (s.TryGetProperty("expr", out _))
            return new ExprSignal(name, perEar, Checked(s, "expr", names));
        throw new RecipeException($"Signal '{name}' needs one of walk, wave, lag or expr.");
    }

    private static PartSpec ReadPart(JsonElement p, List<string> names)
    {
        var type = Text(p, "type");
        var gain = Checked(p, "gain", names, "1");
        switch (type)
        {
            case "noise":
                return new NoisePart(Source(p), Filters(p, names), gain);
            case "events":
                return new EventsPart(Source(p), Trigger(p, names), Range(p, "level", 0f, 4f, (1f, 1f)),
                    Number(p, "decay", 0.005f, 0.0003f, 2f), RangeInt(p, "burst", 1, 32), Number(p, "spacing", 0.005f, 0.0005f, 1f),
                    Filters(p, names), gain);
            case "pings":
                return new PingsPart(Trigger(p, names), Range(p, "pitch", 20f, 12000f), Number(p, "glide", 1f, 0.25f, 4f),
                    Number(p, "glideTime", 0.02f, 0.001f, 2f), Range(p, "decay", 0.001f, 4f), Range(p, "level", 0f, 4f, (1f, 1f)),
                    Number(p, "pan", 1f, 0f, 1f), Number(p, "place", 0f, -1f, 1f), (int)Number(p, "voices", 8, 1, MaxVoices),
                    p.TryGetProperty("lowpass", out _) ? Number(p, "lowpass", 20000f, 20f, 20000f) : 0f,
                    RangeInt(p, "burst", 1, 32), Number(p, "spacing", 0.03f, 0.002f, 1f), gain);
            case "choir":
                return new ChoirPart(gain);
            case "sample":
                var files = Array(p, "files", optional: false).Select(f => f.ValueKind == JsonValueKind.String ? f.GetString()! : "").ToArray();
                if (files.Length is 0 or > 16 || files.Any(f => !IsFile(f)))
                    throw new RecipeException("'files' must list 1 to 16 names of files in Samples: letters, digits and -.");
                var loop = !p.TryGetProperty("loop", out var l) || l.ValueKind != JsonValueKind.False;
                var tune = Checked(p, "tune", names, "1");
                if (tune != "1" && !loop) throw new RecipeException("'tune' is for a loop.");
                var pattern = ReadPattern(p);
                var triggered = p.TryGetProperty("rate", out _) || p.TryGetProperty("bpm", out _);
                if (pattern is not null && (loop || triggered)) throw new RecipeException("'pattern' is for one-shots without a rate or bpm.");
                return new SamplePart(files, loop, tune, pattern, triggered ? Trigger(p, names) : null,
                    Range(p, "pitch", 0.25f, 4f, (1f, 1f)), Range(p, "level", 0f, 4f, (1f, 1f)), Number(p, "spread", 0f, 0f, 1f),
                    Number(p, "fade", 2f, 0.05f, 10f), Number(p, "change", 40f, 5f, 600f), Filters(p, names), gain);
            default:
                throw new RecipeException($"Unknown part type '{type}'. Use noise, events, pings, choir or sample.");
        }
    }

    /// <summary>S18: hits at beats within a cycle of the scene's tempo, each with its level.</summary>
    private static Pattern? ReadPattern(JsonElement p)
    {
        if (!p.TryGetProperty("pattern", out var e)) return null;
        var every = Number(e, "every", 4f, 0.25f, 64f);
        var at = Array(e, "at", optional: false).Select(x => x.ValueKind == JsonValueKind.Number ? x.GetSingle() : -1f).ToArray();
        if (at.Length is 0 or > 64 || at.Any(x => x < 0 || x >= every))
            throw new RecipeException("'at' must list 1 to 64 beats from 0 up to 'every'.");
        var levels = Array(e, "levels", optional: true).Select(x => x.ValueKind == JsonValueKind.Number ? Math.Clamp(x.GetSingle(), 0f, 1f) : 1f).ToArray();
        if (levels.Length != 0 && levels.Length != at.Length) throw new RecipeException("'levels' must have one level per beat in 'at'.");
        return new Pattern(every, at, levels.Length == 0 ? at.Select(_ => 1f).ToArray() : levels);
    }

    private static NoiseMix Source(JsonElement p)
    {
        if (!p.TryGetProperty("source", out var s)) return new NoiseMix(1, 0, 0);
        if (s.ValueKind == JsonValueKind.String)
        {
            return s.GetString() switch
            {
                "white" => new NoiseMix(1, 0, 0),
                "pink" => new NoiseMix(0, 1, 0),
                "brown" => new NoiseMix(0, 0, 1),
                var other => throw new RecipeException($"Unknown noise '{other}'. Use white, pink or brown.")
            };
        }
        return new NoiseMix(Number(s, "white", 0, 0, 2), Number(s, "pink", 0, 0, 2), Number(s, "brown", 0, 0, 2));
    }

    private static Trigger Trigger(JsonElement p, List<string> names)
    {
        if (p.TryGetProperty("bpm", out _))
            return new Trigger(null, Number(p, "bpm", 60, 10, 400), Number(p, "offset", 0, 0, 1), Number(p, "jitter", 0, 0, 0.1f));
        return new Trigger(Checked(p, "rate", names), 0, 0, 0);
    }

    private static IReadOnlyList<FilterSpec> Filters(JsonElement p, List<string> names)
    {
        var filters = Array(p, "filters", optional: true).Select(f =>
        {
            var kind = Text(f, "type") switch
            {
                "lowpass" => FilterKind.LowPass,
                "highpass" => FilterKind.HighPass,
                "bandpass" => FilterKind.BandPass,
                var other => throw new RecipeException($"Unknown filter '{other}'. Use lowpass, highpass or bandpass.")
            };
            return new FilterSpec(kind, Checked(f, "freq", names), Checked(f, "q", names, "0.7"));
        }).ToList();
        if (filters.Count > MaxFilters) throw new RecipeException($"At most {MaxFilters} filters per part.");
        return filters;
    }

    /// <summary>An expression from a number or a string, parsed now so a bad recipe fails here and not while playing.</summary>
    private static string Checked(JsonElement e, string key, List<string> names, string? fallback = null)
    {
        string text;
        if (!e.TryGetProperty(key, out var v))
            text = fallback ?? throw new RecipeException($"Missing '{key}'.");
        else if (v.ValueKind == JsonValueKind.Number)
            text = v.GetDouble().ToString(CultureInfo.InvariantCulture);
        else if (v.ValueKind == JsonValueKind.String)
            text = v.GetString()!;
        else
            throw new RecipeException($"'{key}' must be a number or an expression.");
        Expr.Parse(text, names);
        return text;
    }

    private static string Text(JsonElement e, string key, string? fallback = null)
    {
        if (e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s) return s;
        return fallback ?? throw new RecipeException($"Missing '{key}'.");
    }

    private static string ReadColour(JsonElement e)
    {
        var colour = Text(e, "colour");
        if (colour.Length != 7 || colour[0] != '#' || !int.TryParse(colour[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            throw new RecipeException($"Colour '{colour}' must look like #6FA8DC.");
        return colour;
    }

    private static float Number(JsonElement e, string key, float fallback, float min, float max)
    {
        if (!e.TryGetProperty(key, out var v)) return fallback;
        if (v.ValueKind != JsonValueKind.Number) throw new RecipeException($"'{key}' must be a number.");
        return Clamp(v.GetSingle(), min, max);
    }

    private static (float Min, float Max) Range(JsonElement e, string key, float min, float max, (float, float)? fallback = null)
    {
        if (!e.TryGetProperty(key, out var v))
            return fallback ?? throw new RecipeException($"Missing '{key}'.");
        if (v.ValueKind == JsonValueKind.Number)
        {
            var x = Clamp(v.GetSingle(), min, max);
            return (x, x);
        }
        if (v.ValueKind != JsonValueKind.Array || v.GetArrayLength() != 2 || v[0].ValueKind != JsonValueKind.Number || v[1].ValueKind != JsonValueKind.Number)
            throw new RecipeException($"'{key}' must be a number or [low, high].");
        var a = Clamp(v[0].GetSingle(), min, max);
        var b = Clamp(v[1].GetSingle(), min, max);
        return (Math.Min(a, b), Math.Max(a, b));
    }

    private static (int Min, int Max) RangeInt(JsonElement e, string key, int min, int max)
    {
        if (!e.TryGetProperty(key, out _)) return (1, 1);
        var (a, b) = Range(e, key, min, max);
        return ((int)a, (int)b);
    }

    private static IEnumerable<JsonElement> Array(JsonElement e, string key, bool optional)
    {
        if (!e.TryGetProperty(key, out var v))
            return optional ? [] : throw new RecipeException($"Missing '{key}'.");
        if (v.ValueKind != JsonValueKind.Array) throw new RecipeException($"'{key}' must be a list.");
        return v.EnumerateArray().ToList();
    }

    /// <summary>An optional list of words, each one of <paramref name="known"/>, kept in the order given.</summary>
    private static IReadOnlyList<string> Words(JsonElement e, string key, IReadOnlyList<string> known) =>
        Array(e, key, optional: true).Select(w =>
        {
            var word = w.ValueKind == JsonValueKind.String ? w.GetString()! : "";
            return known.Contains(word) ? word
                : throw new RecipeException($"Unknown word '{word}' in '{key}'. Use one of: {string.Join(", ", known)}.");
        }).Distinct().ToArray();

    private static float Clamp(float x, float min, float max) => float.IsFinite(x) ? Math.Clamp(x, min, max) : min;

    private static bool IsFile(string name) =>
        name.Length is > 0 and <= 48 && char.IsAsciiLetterOrDigit(name[0]) && name.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    private static bool IsName(string name) =>
        name.Length is > 0 and <= 24 && char.IsLetter(name[0]) && name.All(c => char.IsLetterOrDigit(c) || c == '_')
        && name is not ("amount" or "ear");
}

public abstract record SignalSpec(string Name, bool PerEar);

/// <summary>Slow random wandering in [-1, 1] around a mean period (RandomWalk).</summary>
public sealed record WalkSignal(string Name, bool PerEar, float Period) : SignalSpec(Name, PerEar);

/// <summary>Swells in [floor, size]: rises over <c>Rise</c> of each cycle, drains over the rest. Cycles last min–max seconds.</summary>
public sealed record WaveSignal(string Name, bool PerEar, float MinPeriod, float MaxPeriod, float Rise, float MinSize, float MaxSize, float Floor)
    : SignalSpec(Name, PerEar);

/// <summary>An expression that follows its value late, with a time constant.</summary>
public sealed record LagSignal(string Name, bool PerEar, string Of, float Seconds) : SignalSpec(Name, PerEar);

public sealed record ExprSignal(string Name, bool PerEar, string Expr) : SignalSpec(Name, PerEar);

public abstract record PartSpec(string Gain);

/// <summary>Continuous noise through filters: beds, hiss, rumble, wind.</summary>
public sealed record NoisePart(NoiseMix Source, IReadOnlyList<FilterSpec> Filters, string Gain) : PartSpec(Gain);

/// <summary>Filtered noise opened by short decaying envelopes at random or on the beat: ticks, crackles, a shaker.</summary>
public sealed record EventsPart(NoiseMix Source, Trigger Trigger, (float Min, float Max) Level, float Decay, (int Min, int Max) Burst,
    float Spacing, IReadOnlyList<FilterSpec> Filters, string Gain) : PartSpec(Gain);

/// <summary>
/// Short pitched tones that glide while they ring out: drops, bubbles, a soft thump. With a burst, each trigger plays
/// a run of the same tone at a steady spacing, as a cricket's chirp. <c>Place</c> is where in the field (-1 left, 1
/// right) and <c>Pan</c> how far around it each trigger may land.
/// </summary>
public sealed record PingsPart(Trigger Trigger, (float Min, float Max) Pitch, float Glide, float GlideTime, (float Min, float Max) Decay,
    (float Min, float Max) Level, float Pan, float Place, int Voices, float Lowpass, (int Min, int Max) Burst, float Spacing, string Gain)
    : PartSpec(Gain);

/// <summary>
/// Audio files (S12). A loop plays one file at a time without a seam, folding its end over its start in
/// <c>Fade</c> seconds, and every <c>Change</c> seconds or so crossfades to another. A one-shot (<c>Loop</c> false)
/// plays a whole file each time it fires: on a <c>Trigger</c>, or, without one, at each of the ring's strikes (A11).
/// Each shot takes a random file, a pitch from <c>Pitch</c>, a level from <c>Level</c> and a place up to
/// <c>Spread</c> from the middle. A loop that keeps the beat (<c>Sync</c>) plays its one file whole, as it is, from
/// <c>Tune</c> is a loop's speed and pitch as an expression, 1 as recorded, held to 0.5–2 (S17). One-shots with a
/// <c>Pattern</c> fire on the scene's beat (S18, A16).
/// </summary>
public sealed record SamplePart(IReadOnlyList<string> Files, bool Loop, string Tune, Pattern? Pattern, Trigger? Trigger, (float Min, float Max) Pitch,
    (float Min, float Max) Level, float Spread, float Fade, float Change, IReadOnlyList<FilterSpec> Filters, string Gain) : PartSpec(Gain)
{
    /// <summary>Fired by the ring's strikes rather than by itself.</summary>
    public bool TakesStrikes => !Loop && Trigger is null && Pattern is null;
}

/// <summary>Hits <c>At</c> these beats in every cycle of <c>Every</c> beats of the scene's tempo, each at its level (S18).</summary>
public sealed record Pattern(float Every, IReadOnlyList<float> At, IReadOnlyList<float> Levels);

/// <summary>A six-voice formant choir as one part.</summary>
public sealed record ChoirPart(string Gain) : PartSpec(Gain);

public sealed record NoiseMix(float White, float Pink, float Brown);

/// <summary>Random events at <c>Rate</c> per second, or on a beat at <c>Bpm</c> when Rate is null.</summary>
public sealed record Trigger(string? Rate, float Bpm, float Offset, float Jitter);

public enum FilterKind
{
    LowPass,
    HighPass,
    BandPass
}

public sealed record FilterSpec(FilterKind Kind, string Freq, string Q);
