using System.Text;
using System.Text.Json;
using Soundmote.Core.Recipes;

namespace Soundmote.Core.Scenes;

/// <summary>
/// A scene as JSON (A12): every ring with its sound's recipe id, curve and play, so a scene read back renders the same
/// samples (A6). The id is the file's name and is not written inside it.
/// </summary>
public static class SceneFile
{
    public const int Format = 1;

    public static string Write(Scene scene)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("format", Format);
            w.WriteString("name", scene.Name);
            w.WriteString("intent", Word(scene.Intent));
            w.WriteString("ground", Word(scene.Ground));
            w.WriteString("kind", Word(scene.Kind));
            w.WriteNumber("seconds", scene.Length.TotalSeconds);
            w.WriteNumber("seed", scene.Seed);
            w.WriteNumber("room", scene.RoomSeconds);
            w.WriteNumber("tempo", scene.Tempo);
            w.WriteStartArray("phases");
            foreach (var p in scene.Phases)
            {
                w.WriteStartObject();
                w.WriteString("name", p.Name);
                w.WriteNumber("from", p.From);
                w.WriteNumber("to", p.To);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteStartArray("layers");
            foreach (var layer in scene.Layers) WriteLayer(w, layer);
            w.WriteEndArray();
            if (scene.Wake is { } wake)
            {
                w.WriteStartObject("wake");
                w.WriteString("at", wake.At.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture));
                w.WriteNumber("rise", wake.Rise.TotalSeconds);
                w.WriteStartArray("layers");
                foreach (var layer in wake.Layers) WriteLayer(w, layer);
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <exception cref="SceneFileException">The JSON is not a scene, or names a sound the library does not have.</exception>
    public static Scene Read(string json, string id)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        }
        catch (JsonException e)
        {
            throw new SceneFileException($"Not valid JSON: {e.Message}");
        }
        using (doc)
        {
            try
            {
                return ReadScene(doc.RootElement, id);
            }
            catch (Exception e) when (e is InvalidOperationException or FormatException or ArgumentException or IndexOutOfRangeException)
            {
                throw new SceneFileException(e.Message);
            }
        }
    }

    private static Scene ReadScene(JsonElement root, string id)
    {
        if (Get(root, "format").GetInt32() > Format) throw new SceneFileException("Made by a newer Soundmote.");
        var length = TimeSpan.FromSeconds(Get(root, "seconds").GetDouble());
        if (length <= TimeSpan.Zero || length > TimeSpan.FromDays(1)) throw new SceneFileException("The length must be up to a day.");
        return new Scene(Get(root, "name").GetString()!, Enum<TimeKind>(root, "kind"), length,
            Get(root, "layers").EnumerateArray().Select(ReadLayer).OfType<Layer>().ToArray())
        {
            Id = id,
            Intent = Enum<Intent>(root, "intent"),
            Ground = Enum<Ground>(root, "ground"),
            Seed = Get(root, "seed").GetUInt64(),
            RoomSeconds = Math.Clamp(Get(root, "room").GetSingle(), 0.1f, 10f),
            // A scene written before tempo existed keeps a resting pulse.
            Tempo = root.TryGetProperty("tempo", out var tempo) ? Math.Clamp(tempo.GetDouble(), Scene.MinTempo, Scene.MaxTempo) : Scene.DefaultTempo,
            Phases = Get(root, "phases").EnumerateArray()
                .Select(p => new Phase(Get(p, "name").GetString()!, Get(p, "from").GetDouble(), Get(p, "to").GetDouble())).ToArray(),
            Wake = root.TryGetProperty("wake", out var wake) ? ReadWake(wake) : null
        };
    }

    private static void WriteLayer(Utf8JsonWriter w, Layer layer)
    {
        w.WriteStartObject();
        w.WriteString("name", layer.Name);
        w.WriteString("sound", layer.Recipe.Id);
        w.WriteString("colour", layer.Colour);
        w.WriteNumber("drift", layer.Drift);
        w.WriteNumber("send", layer.ReverbSend);
        w.WriteNumber("level", layer.Level);
        w.WriteNumber("waver", layer.Waver);
        WritePoints(w, "curve", layer.Curve);
        w.WriteStartObject("play");
        switch (layer.Play)
        {
            case OnOffPlay on:
                w.WriteString("kind", "onoff");
                w.WriteStartArray("stretches");
                foreach (var s in on.Stretches)
                {
                    w.WriteStartArray();
                    w.WriteNumberValue(s.From);
                    w.WriteNumberValue(s.To);
                    w.WriteEndArray();
                }
                w.WriteEndArray();
                w.WriteNumber("level", on.Level);
                w.WriteNumber("fade", on.FadeSeconds);
                break;
            case EveryPlay every:
                w.WriteString("kind", "every");
                w.WriteNumber("interval", every.Interval.TotalSeconds);
                w.WriteNumber("first", every.First.TotalSeconds);
                w.WriteNumber("strength", every.Strength);
                break;
            case RandomPlay random:
                w.WriteString("kind", "random");
                w.WriteNumber("perTenMinutes", random.PerTenMinutes);
                w.WriteNumber("minGap", random.MinGap.TotalSeconds);
                w.WriteNumber("vary", random.Vary);
                WritePoints(w, "busy", random.Busy);
                w.WriteNumber("strength", random.Strength);
                break;
            default:
                w.WriteString("kind", "shape");
                break;
        }
        w.WriteEndObject();
        var place = layer.Place;
        w.WriteStartObject("place");
        w.WriteNumber("pan", place.Pan);
        w.WriteNumber("width", place.Width);
        w.WriteString("move", Word(place.Move));
        w.WriteNumber("range", place.Range);
        w.WriteNumber("seconds", place.Seconds);
        WritePoints(w, "path", place.Path);
        w.WriteEndObject();
        w.WriteEndObject();
    }

    /// <summary>A14: the time as "07:00", how long the rise takes, and the morning's own rings.</summary>
    private static Wake ReadWake(JsonElement e)
    {
        if (!TimeSpan.TryParseExact(Get(e, "at").GetString(), @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var at))
            throw new SceneFileException($"'{Get(e, "at")}' is not a time like 07:00.");
        return new Wake(at, Get(e, "layers").EnumerateArray().Select(ReadLayer).OfType<Layer>().ToArray())
        {
            Rise = TimeSpan.FromSeconds(Math.Clamp(Get(e, "rise").GetDouble(), 1, 3600))
        };
    }

    /// <summary>A13. A ring written before places existed is in the middle.</summary>
    private static Place ReadPlace(JsonElement layer)
    {
        if (!layer.TryGetProperty("place", out var p)) return Place.Centre;
        return new Place(Math.Clamp(Get(p, "pan").GetSingle(), -1f, 1f), Math.Clamp(Get(p, "width").GetSingle(), 0f, 1f))
        {
            Move = Enum<Move>(p, "move"),
            Range = Math.Clamp(Get(p, "range").GetSingle(), 0f, 2f),
            Seconds = Math.Clamp(Get(p, "seconds").GetDouble(), 0.05, 600),
            Path = ReadPoints(Get(p, "path"))
        };
    }

    /// <summary>A ring, or null when its sound is no longer in the library: the scene then opens without it.</summary>
    private static Layer? ReadLayer(JsonElement e)
    {
        var sound = Get(e, "sound").GetString()!;
        var recipe = RecipeLibrary.All.FirstOrDefault(r => r.Id == sound);
        if (recipe is null) return null;
        var p = Get(e, "play");
        Play play = Get(p, "kind").GetString() switch
        {
            "shape" => Play.Shape,
            "onoff" => new OnOffPlay(
                Get(p, "stretches").EnumerateArray().Select(s => new Stretch(s[0].GetDouble(), s[1].GetDouble())).ToArray(),
                Get(p, "level").GetSingle(), Get(p, "fade").GetDouble()),
            "every" => new EveryPlay(Seconds(p, "interval"), Seconds(p, "first"), Get(p, "strength").GetSingle()),
            "random" => new RandomPlay(Get(p, "perTenMinutes").GetDouble(), Seconds(p, "minGap"), Get(p, "vary").GetSingle(),
                ReadPoints(Get(p, "busy")), Get(p, "strength").GetSingle()),
            var kind => throw new SceneFileException($"'{kind}' is not a way to play.")
        };
        return new Layer(Get(e, "name").GetString()!, recipe, ReadPoints(Get(e, "curve")))
        {
            Colour = Get(e, "colour").GetString()!,
            Drift = Get(e, "drift").GetSingle(),
            ReverbSend = Get(e, "send").GetSingle(),
            Level = Get(e, "level").GetSingle(),
            Play = play,
            Place = ReadPlace(e),
            // A ring written before the Waver knob existed wavers by the default.
            Waver = e.TryGetProperty("waver", out var waver) ? Math.Clamp(waver.GetSingle(), 0f, 1f) : Layer.DefaultWaver
        };
    }

    /// <summary>A curve as pairs, <c>[[0, 0.2], [0.5, 0.9]]</c>: where on the turn, then how much.</summary>
    private static void WritePoints(Utf8JsonWriter w, string name, Curve curve)
    {
        w.WriteStartArray(name);
        foreach (var (at, value) in curve.Points)
        {
            w.WriteStartArray();
            w.WriteNumberValue(at);
            w.WriteNumberValue(value);
            w.WriteEndArray();
        }
        w.WriteEndArray();
    }

    private static Curve ReadPoints(JsonElement e) =>
        new(e.EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetSingle())).ToArray());

    private static TimeSpan Seconds(JsonElement e, string name) => TimeSpan.FromSeconds(Get(e, name).GetDouble());

    private static JsonElement Get(JsonElement e, string name) =>
        e.TryGetProperty(name, out var value) ? value : throw new SceneFileException($"'{name}' is missing.");

    private static T Enum<T>(JsonElement e, string name) where T : struct, Enum =>
        System.Enum.TryParse<T>(Get(e, name).GetString(), ignoreCase: true, out var value)
            ? value
            : throw new SceneFileException($"'{Get(e, name)}' is not a {name}.");

    private static string Word<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();
}

public sealed class SceneFileException(string message) : Exception(message);
