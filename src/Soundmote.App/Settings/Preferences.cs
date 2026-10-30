using System.Text.Json;
using System.Text.Json.Serialization;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Settings;

/// <summary>What happens when Soundmote opens (D18).</summary>
public enum StartWith
{
    Scenes,
    LastScene,
    PlayLastScene
}

/// <summary>The user's settings (D18), kept as one small JSON file beside the scenes folder. Immutable; a change is a copy.</summary>
public sealed record Preferences
{
    /// <summary>How long a scene fades out when it stops by itself (Stop after, D16).</summary>
    public double FadeSeconds { get; init; } = 10;

    /// <summary>Closing the window hides it and the scene plays on, while the menu bar is shown.</summary>
    public bool KeepPlaying { get; init; } = true;

    public bool ShowMenuBar { get; init; } = true;

    public StartWith StartWith { get; init; } = StartWith.LastScene;

    /// <summary>The ground New scene starts with; null to take it from what the scene is for (D14).</summary>
    public Ground? NewGround { get; init; }

    /// <summary>The bedside clock (D17).</summary>
    public bool DimAtNight { get; init; } = true;

    /// <summary>The id of the scene opened last.</summary>
    public string? LastScene { get; init; }

    /// <summary>First run is over (D19): a scene was picked, or an empty dial begun.</summary>
    public bool Welcomed { get; init; }

    /// <summary>The first-run hints already used (D19).</summary>
    public IReadOnlyList<string> HintsSeen { get; init; } = [];

    /// <summary>Settings for someone who has been here before: no first run, no hints. Tests start from these.</summary>
    public static Preferences Seasoned { get; } = new() { Welcomed = true, HintsSeen = Hints.All };
}

/// <summary>The three first-run hints on the dial, in the order they are shown (D19).</summary>
public static class Hints
{
    public const string Shape = "shape";
    public const string Seek = "seek";
    public const string Add = "add";

    public static IReadOnlyList<string> All { get; } = [Shape, Seek, Add];
}

/// <summary>Reads and writes <see cref="Preferences"/>. A missing or broken file reads as a first run.</summary>
public sealed class PreferencesStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Path { get; } = path;

    /// <summary>settings.json beside the scenes folder.</summary>
    public static string DefaultPath() =>
        System.IO.Path.Combine(System.IO.Path.GetDirectoryName(SceneStore.DefaultFolder())!, "settings.json");

    public Preferences Load()
    {
        try
        {
            return File.Exists(Path) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path), Json) ?? new() : new();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    /// <summary>Writes in one step, as scenes are (A12); a folder the system refuses leaves the file as it was.</summary>
    public void Save(Preferences preferences)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temporary = Path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, Json));
            File.Move(temporary, Path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
