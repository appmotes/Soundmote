using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// A starting shape for a ring (D8). It keeps the ring's own loudness: the preset is scaled to the highest point of
/// the curve it replaces, so "Fades" on a quiet ring stays quiet.
/// </summary>
public sealed record ShapePreset(string Name, float[] Profile)
{
    public static IReadOnlyList<ShapePreset> All { get; } =
    [
        new("Steady", [0.8f, 0.8f]),
        new("Rises", [0.15f, 0.45f, 0.8f, 1f]),
        new("Fades", [1f, 0.8f, 0.45f, 0.15f]),
        new("Swells", [0.1f, 0.55f, 1f, 0.55f, 0.1f])
    ];

    public Curve Shape(Curve current) => Shape(Math.Max(0.4f, current.Points.Max(p => p.Value)));

    /// <summary>The preset with its highest point at <paramref name="level"/>.</summary>
    public Curve Shape(float level) => Curve.Even(Profile.Select(v => v * level).ToArray());
}
