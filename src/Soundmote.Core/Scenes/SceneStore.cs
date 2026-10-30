using System.Text;

namespace Soundmote.Core.Scenes;

/// <summary>
/// The scenes folder (A12): one <c>.json</c> per scene, named by its id. A write goes to a temporary file first and
/// replaces the old one in one step, so a crash never leaves half a scene.
/// </summary>
public sealed class SceneStore(string folder)
{
    private const string Extension = ".json";

    public string Folder { get; } = folder;

    /// <summary>Application Support on macOS, AppData on Windows, the XDG data folder on Linux.</summary>
    public static string DefaultFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS()) return Path.Combine(home, "Library", "Application Support", "Soundmote", "Scenes");
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Soundmote", "Scenes");
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return Path.Combine(string.IsNullOrEmpty(data) ? Path.Combine(home, ".local", "share") : data, "soundmote", "scenes");
    }

    public string PathOf(string id) => Path.Combine(Folder, id + Extension);

    /// <summary>Every scene in the folder, by name. A file that does not read is left alone and named in <paramref name="skipped"/>.</summary>
    public IReadOnlyList<Scene> Load(ICollection<string>? skipped = null)
    {
        if (!Directory.Exists(Folder)) return [];
        var scenes = new List<Scene>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*" + Extension))
        {
            try
            {
                scenes.Add(SceneFile.Read(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path)));
            }
            catch (Exception e) when (e is SceneFileException or IOException or UnauthorizedAccessException)
            {
                skipped?.Add($"{Path.GetFileName(path)}: {e.Message}");
            }
        }
        return scenes.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public void Save(Scene scene)
    {
        if (scene.Id.Length == 0) throw new ArgumentException("A stored scene needs an id.", nameof(scene));
        Directory.CreateDirectory(Folder);
        var path = PathOf(scene.Id);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, SceneFile.Write(scene), Encoding.UTF8);
        File.Move(temporary, path, overwrite: true);
    }

    public void Delete(string id) => File.Delete(PathOf(id));

    /// <summary>
    /// An id for a new scene: its name in lower-case words joined by dashes, with a number added when the folder or
    /// <paramref name="taken"/> already has it.
    /// </summary>
    public string NewId(string name, IEnumerable<string> taken)
    {
        var words = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c)) words.Append(c);
            else if (words.Length > 0 && words[^1] != '-') words.Append('-');
        }
        var stem = words.ToString().Trim('-');
        if (stem.Length == 0) stem = "scene";
        var used = taken.ToHashSet();
        var id = stem;
        for (var n = 2; used.Contains(id) || File.Exists(PathOf(id)); n++) id = $"{stem}-{n}";
        return id;
    }
}
