using System.Reflection;

namespace Soundmote.Core.Recipes;

/// <summary>The recipes that ship with Soundmote, read from the JSON files in Recipes/Library (S10).</summary>
public static class RecipeLibrary
{
    private static readonly Lazy<IReadOnlyList<Recipe>> Loaded = new(Load);

    /// <summary>Every built-in recipe, in the order the add panel shows them.</summary>
    public static IReadOnlyList<Recipe> All => Loaded.Value;

    private static readonly string[] Order =
    [
        "rain", "rain-window", "rain-skylight", "rain-tent", "rain-jungle", "rain-heavy", "thunder-far", "thunder-roll",
        "thunder-close", "thunder-mountain", "thunder-rumble", "sea", "sea-sand", "sea-pebbles", "sea-lake",
        "sea-rocks", "sea-far", "sea-lapping", "stream", "stream-forest", "stream-river", "stream-trickle",
        "stream-rapids", "stream-fall", "wind", "wind-grass", "wind-howl", "wind-mountain", "wind-strong",
        "wind-whistle", "fire", "fire-stove", "fire-small", "fire-crackle", "fire-camp", "fire-heater", "birds-dawn",
        "birds-blackbird", "birds-gulls", "birds-woodpecker", "birds-pond", "birds-morning", "birds-nightsong",
        "forest-morning", "forest-deep", "forest-pines", "forest-breeze", "forest-alps", "crickets", "night-woods",
        "night-frogs", "night-cicadas", "night-treefrogs", "choir", "voices-temple", "voices-monks", "voices-tibetan",
        "voices-pilgrims", "voices-throat", "bowl", "bowl-small", "bowl-sung", "temple-bell", "gong", "chimes",
        "pad-warm", "pad-glass", "pad-drone", "pad-breath", "pad-voices", "pad-shimmer", "pad-strings", "pad-tide",
        "pad-organ", "pad-space", "rhythm-heart", "rhythm-pulse", "rhythm-downtempo", "rhythm-mystic", "rhythm-shaker",
        "rhythm-hands", "rhythm-kalimba", "rhythm-bass", "rhythm-ritual", "town-night", "town-cafe", "town-train",
        "town-street", "town-sbahn", "brown", "noise-washer", "noise-purr", "noise-fan", "noise-fan-ceiling",
        "noise-purifier", "noise-clock"
    ];

    public static Recipe Get(string id) =>
        All.FirstOrDefault(r => r.Id == id) ?? throw new ArgumentException($"No recipe called '{id}'.", nameof(id));

    private static IReadOnlyList<Recipe> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string prefix = "Soundmote.Core.Recipes.Library.";
        var recipes = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                using var reader = new StreamReader(stream);
                try
                {
                    return Recipe.Parse(reader.ReadToEnd());
                }
                catch (RecipeException e)
                {
                    throw new InvalidOperationException($"Built-in recipe {n[prefix.Length..]} is broken: {e.Message}", e);
                }
            })
            .ToList();
        return recipes.OrderBy(r => Array.IndexOf(Order, r.Id) is var i and >= 0 ? i : int.MaxValue).ThenBy(r => r.Name).ToArray();
    }
}
