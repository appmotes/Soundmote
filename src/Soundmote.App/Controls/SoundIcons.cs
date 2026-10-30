using Avalonia.Media;
using Soundmote.Core.Recipes;

namespace Soundmote.App.Controls;

/// <summary>
/// The sound glyphs (D10): one line drawing per sound on a 24 grid, stroked, never filled. A recipe names its glyph;
/// one without its own gets its family's.
/// </summary>
public static class SoundIcons
{
    private const string Cloud = "M7 16A3.5 3.5 0 0 1 7.2 9.05 5.5 5.5 0 0 1 17.6 8.5 3.75 3.75 0 0 1 17.5 16Z";

    private static readonly Dictionary<string, (string Path, double Stroke)> Paths = new()
    {
        ["rain"] = (Cloud + " M8.5 18.5 7.5 21 M12.5 18.5 11.5 21 M16.5 18.5 15.5 21", 1.8),
        ["sea"] = ("M2 8c2.5 0 2.5-2 5-2s2.5 2 5 2 2.5-2 5-2 2.5 2 5 2 M2 13c2.5 0 2.5-2 5-2s2.5 2 5 2 2.5-2 5-2 2.5 2 5 2 M2 18c2.5 0 2.5-2 5-2s2.5 2 5 2 2.5-2 5-2 2.5 2 5 2", 1.8),
        ["stream"] = ("M3 6c3 0 3 2 6 2s3-2 6-2 3 2 6 2 M3 11c3 0 3 2 6 2s3-2 6-2 3 2 6 2 M5 18.5a2.5 1.6 0 1 0 5 0a2.5 1.6 0 1 0-5 0 M13.5 18a2 1.3 0 1 0 4 0a2 1.3 0 1 0-4 0", 1.8),
        ["wind"] = ("M3 8h9.5a2.5 2.5 0 1 0-2.5-2.5 M3 12h15.5a2.5 2.5 0 1 1-2.5 2.5 M3 16h8", 1.8),
        ["forest"] = ("M8 3l4 6h-2.2l3.2 5H3l3.2-5H4z M8 14v6 M17 8l3 4.5h-1.6l2.4 3.5h-7.6l2.4-3.5H14z M17 16v4", 1.8),
        ["crickets"] = ("M9 14.5c0-2.8 1.3-5.5 3-5.5s3 2.7 3 5.5-1.3 5.5-3 5.5-3-2.7-3-5.5z M11 9C9.5 6 7 4.5 4 4 M13 9c1.5-3 4-4.5 7-5 M9.3 13 6 11.5 M14.7 13 18 11.5 M9.3 16.5 5.5 19 M14.7 16.5 18.5 19", 1.8),
        ["fire"] = ("M12 3c.8 3.2 5 5.2 5 10a5 5 0 0 1-10 0c0-2.2 1-3.6 2.2-4.6.1 1.9.9 3.1 2 3.4-.4-3.3.1-6.3.8-8.8z M12 21a2 2 0 0 1-2-2c0-1.4 1-2.2 2-3.2 1 1 2 1.8 2 3.2a2 2 0 0 1-2 2z", 1.8),
        ["choir"] = ("M6 10.5a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0 M13 10.5a2.5 2.5 0 1 0 5 0a2.5 2.5 0 1 0-5 0 M4 20c0-2.8 2-4.5 4.5-4.5S13 17.2 13 20 M11 20c0-2.8 2-4.5 4.5-4.5S20 17.2 20 20 M9.5 4.5c1-1 2-1 3 0 M14.5 3.5c1-1 2-1 3 0", 1.8),
        ["monks"] = ("M11 3c-3.3 0-5.5 3-5.5 7.5V21h11V10.5C16.5 6 14.3 3 11 3z M8.8 10.5c.4 1.4 1.2 2.1 2.2 2.1s1.8-.7 2.2-2.1 M8 21v-4 M14 21v-4 M19.5 8c.8.8.8 2.2 0 3 M21.3 6.3c1.6 1.6 1.6 4.8 0 6.4", 1.8),
        ["bell"] = ("M6.5 16V11a5.5 5.5 0 0 1 11 0v5l1.5 2h-14z M10 20.5a2 2 0 0 0 4 0 M12 3.5v2", 1.8),
        ["bowl"] = ("M3 11h18c0 4.4-4 8-9 8s-9-3.6-9-8z M9 21h6 M8 7.5c1-1 2-1 3 0 M13 6c1-1 2-1 3 0", 1.8),
        ["chimes"] = ("M4 4h16 M7 4v9 M11 4v13 M15 4v7 M19 4v11", 1.8),
        ["birds"] = ("M3 10c2.2-2.6 5.2-2.6 7 .5 1.8-3.1 4.8-3.1 7-.5 M9 17c1.3-1.6 3.1-1.6 4 .3.9-1.9 2.7-1.9 4-.3", 1.8),
        ["thunder"] = (Cloud + " M12.5 13.5 10 18h3l-2 3.5", 1.8),
        ["drum"] = ("M3 9.5a9 3.5 0 1 0 18 0a9 3.5 0 1 0-18 0 M3 9.5v5c0 1.9 4 3.5 9 3.5s9-1.6 9-3.5v-5 M15 3l4 4", 1.8),
        ["noise"] = ("M5 6h0 M9 4h0 M14 6h0 M19 5h0 M7 10h0 M12 9h0 M17 11h0 M4 14h0 M10 13h0 M15 15h0 M20 14h0 M6 18h0 M12 18h0 M18 19h0", 2.8),
        ["cafe"] = ("M4 9h12v4.5A5.5 5.5 0 0 1 10.5 19h-1A5.5 5.5 0 0 1 4 13.5z M16 10.5h1.5a2.5 2.5 0 0 1 0 5H16 M8 3.5c-.8.9-.8 1.6 0 2.5 M12 3.5c-.8.9-.8 1.6 0 2.5 M3 21h14", 1.8),
        ["keys"] = ("M3 5h18v14H3z M6 5v7h3V5 M10.5 5v7h3V5 M15 5v7h3V5 M7.5 12v7 M12 12v7 M16.5 12v7", 1.6),
        ["pulse"] = ("M2 12h4l2-5 3 10 3-12 3 9 2-2h3", 1.8)
    };

    private static readonly Dictionary<string, Geometry> Parsed = [];

    public static IEnumerable<string> Names => Paths.Keys;

    /// <summary>The glyph for a recipe: its own, else its family's.</summary>
    public static (Geometry Data, double Stroke) For(Recipe recipe) =>
        Get(recipe.Icon is { } icon && Paths.ContainsKey(icon) ? icon : Family.Get(recipe.Family).Icon);

    public static (Geometry Data, double Stroke) Get(string name)
    {
        var (path, stroke) = Paths[name];
        if (!Parsed.TryGetValue(name, out var geometry)) Parsed[name] = geometry = Geometry.Parse(path);
        return (geometry, stroke);
    }
}
