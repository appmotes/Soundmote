namespace Soundmote.Core.Recipes;

/// <summary>
/// What a sound is (S14). Every recipe belongs to one family; the add panel shows the families first (D20), so it stays
/// fifteen rows however many sounds there are. A family without sounds yet is shown as coming.
/// </summary>
public sealed record Family(string Id, string Name, string Icon, string Colour)
{
    public static IReadOnlyList<Family> All { get; } =
    [
        new("rain", "Rain", "rain", "#3F7FBF"),
        new("thunder", "Thunder", "thunder", "#5A6478"),
        new("sea", "Sea & shore", "sea", "#2E9C90"),
        new("streams", "Streams", "stream", "#3A9DB0"),
        new("wind", "Wind", "wind", "#6F8296"),
        new("fire", "Fire", "fire", "#C4683A"),
        new("birds", "Birds", "birds", "#4F9150"),
        new("forest", "Forest", "forest", "#4A7A4C"),
        new("night", "Night creatures", "crickets", "#6B5FA3"),
        new("voices", "Voices & chant", "choir", "#A0577E"),
        new("bells", "Bowls & bells", "bowl", "#B8862B"),
        new("pads", "Synth pads", "keys", "#8E6CC0"),
        new("rhythm", "Rhythms", "drum", "#C0566A"),
        new("town", "Town & traffic", "cafe", "#6E7FA3"),
        new("noise", "Noise & pulse", "noise", "#8A7867")
    ];

    /// <summary>Where a sound belongs (S14): the words a recipe's <c>places</c> may use, in the order the panel shows them.</summary>
    public static IReadOnlyList<string> Places { get; } = ["forest", "jungle", "shore", "town", "indoors", "outdoors", "temple"];

    /// <summary>How a sound feels (S14): the words a recipe's <c>moods</c> may use, in the order the panel shows them.</summary>
    public static IReadOnlyList<string> Moods { get; } = ["calm", "cosy", "warm", "mysterious", "wild", "bright"];

    /// <exception cref="ArgumentException">There is no such family.</exception>
    public static Family Get(string id) =>
        All.FirstOrDefault(f => f.Id == id) ?? throw new ArgumentException($"No family called '{id}'.", nameof(id));
}
