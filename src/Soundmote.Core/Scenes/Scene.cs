using Soundmote.Core.Recipes;

namespace Soundmote.Core.Scenes;

/// <summary>What one turn of the dial means (A3).</summary>
public enum TimeKind
{
    /// <summary>Has an end; every ring falls to silence when the turn is over.</summary>
    Journey,

    /// <summary>Goes round until stopped; each lap repeats the curves with new variation.</summary>
    Loop,

    /// <summary>One turn is the real day, midnight to midnight, read from the local clock.</summary>
    Clock
}

/// <summary>What a scene is for; the library groups scenes by it.</summary>
public enum Intent
{
    Sleep,
    Focus,
    Meditate,
    Relax,
    Rhythm,
    Background
}

/// <summary>The colours a scene is drawn on (D12). The app maps each to a theme; the engine ignores it.</summary>
public enum Ground
{
    Night,
    Ember,
    Paper,
    Sky
}

/// <summary>One ring: a sound's recipe, its curve over the turn and how it sits in the mix.</summary>
public sealed record Layer(string Name, Recipe Recipe, Curve Curve)
{
    /// <summary>Hex colour of the ring, shared by every view of the scene. The recipe's own by default (D3).</summary>
    public string Colour { get; init; } = Recipe.Colour;

    /// <summary>How far the amount may wander around the curve: 0.2 is ±20 %.</summary>
    public float Drift { get; init; } = 0.15f;

    /// <summary>How much goes to the shared room (A4). The recipe's own by default.</summary>
    public float ReverbSend { get; init; } = Recipe.Room;

    public float Level { get; init; } = 1f;

    /// <summary>Shape, on/off, every or random (A11). Only a shape reads <see cref="Curve"/>.</summary>
    public Play Play { get; init; } = Play.Shape;

    /// <summary>Where it is heard between left and right, and how that moves (A13).</summary>
    public Place Place { get; init; } = Place.Centre;

    /// <summary>How much the sound wavers in pitch, 0–1, for a sound that reads it (S17).</summary>
    public float Waver { get; init; } = DefaultWaver;

    public const float DefaultWaver = 0.3f;
}

public sealed record Scene(string Name, TimeKind Kind, TimeSpan Length, IReadOnlyList<Layer> Layers)
{
    /// <summary>The scene's file name without extension (A12). Built-in scenes have fixed ids; a changed one keeps it.</summary>
    public string Id { get; init; } = "";

    public ulong Seed { get; init; } = 1;

    public float RoomSeconds { get; init; } = 3.5f;

    /// <summary>Beats a minute for every ring that keeps the beat (A16, S18); a resting pulse by default.</summary>
    public double Tempo { get; init; } = DefaultTempo;

    public const double DefaultTempo = 60;

    public const double MinTempo = 40, MaxTempo = 120;

    public Intent Intent { get; init; } = Intent.Sleep;

    public Ground Ground { get; init; } = Ground.Night;

    /// <summary>Named stretches of the turn, shown beside the dial. They do not change the sound.</summary>
    public IReadOnlyList<Phase> Phases { get; init; } = [];

    /// <summary>For a journey: wake the listener at a time of day instead of ending in silence (A14). Null for silence.</summary>
    public Wake? Wake { get; init; }
}

/// <summary>
/// A morning at the end of a journey (A14): at <see cref="At"/> o'clock its own rings have risen over
/// <see cref="Rise"/> and keep playing until stopped. A wake that was missed, because the scene started later, stays silent.
/// </summary>
public sealed record Wake(TimeSpan At, IReadOnlyList<Layer> Layers)
{
    public TimeSpan Rise { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>Waves and a stream, until there are birds (N12).</summary>
    public static Wake Morning(TimeSpan at) => new(at,
    [
        new Layer("Distant sea", RecipeLibrary.Get("sea"), Curve.Steady(0.7f)),
        new Layer("Stream", RecipeLibrary.Get("stream"), Curve.Steady(0.6f))
    ]);
}

/// <summary>A named stretch of the turn, from and to as positions 0–1.</summary>
public sealed record Phase(string Name, double From, double To);
