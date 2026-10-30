using Soundmote.Core.Recipes;

namespace Soundmote.Core.Scenes;

/// <summary>The scenes Soundmote ships with. A changed one is stored under the same id and shown instead (A12).</summary>
public static class BuiltInScenes
{
    /// <summary>A ring named after its sound, so the dial says which rain or which pad it is.</summary>
    private static Layer Ring(string id, Curve curve) => new(RecipeLibrary.Get(id).Name, RecipeLibrary.Get(id), curve);

    /// <summary>Every built-in scene, in the order the library shows them.</summary>
    public static IReadOnlyList<Scene> All(ulong seed = 1) => [DeepWork(seed), FallAsleep(seed), Sit(seed), Unwind(seed), AllDay(seed)];

    /// <summary>
    /// A 45-minute journey into sleep, one sound handing over to the next: soft rain to settle, a pad that breathes four
    /// seconds in and six out over a slow heartbeat, then a deep drone that carries the drift with a lake lapping at its
    /// edge, and distant surf as sleep comes. Points every 3 minutes.
    /// </summary>
    public static Scene FallAsleep(ulong seed = 1) => new("Fall Asleep", TimeKind.Journey, TimeSpan.FromMinutes(45),
    [
        Ring("rain", Curve.Even(0.7f, 0.75f, 0.7f, 0.55f, 0.35f, 0.18f, 0.06f, 0, 0, 0, 0, 0, 0, 0, 0, 0)) with { Drift = 0.1f },
        Ring("pad-breath", Curve.Even(0, 0.25f, 0.45f, 0.55f, 0.55f, 0.45f, 0.3f, 0.15f, 0.05f, 0, 0, 0, 0, 0, 0, 0)) with { Drift = 0.1f },
        Ring("rhythm-heart", Curve.Even(0, 0.15f, 0.3f, 0.35f, 0.3f, 0.2f, 0.1f, 0.03f, 0, 0, 0, 0, 0, 0, 0, 0)) with { Drift = 0.05f },
        Ring("pad-drone", Curve.Even(0, 0, 0, 0.05f, 0.15f, 0.3f, 0.4f, 0.45f, 0.45f, 0.42f, 0.38f, 0.32f, 0.25f, 0.18f, 0.08f, 0)) with { Drift = 0.1f },
        Ring("sea-lake", Curve.Even(0, 0, 0, 0, 0, 0.05f, 0.15f, 0.3f, 0.4f, 0.4f, 0.3f, 0.15f, 0.05f, 0, 0, 0)) with { Drift = 0.1f },
        Ring("sea-far", Curve.Even(0, 0, 0, 0, 0, 0, 0, 0, 0, 0.05f, 0.15f, 0.28f, 0.35f, 0.3f, 0.15f, 0.02f)) with { Drift = 0.1f }
    ])
    {
        Id = "fall-asleep",
        Seed = seed,
        Tempo = 56,
        Intent = Intent.Sleep,
        Ground = Ground.Night,
        Phases = [new Phase("Settle", 0, 10 / 45d), new Phase("Drift", 10 / 45d, 30 / 45d), new Phase("Sleep", 30 / 45d, 1)]
    };

    /// <summary>
    /// A 60-minute loop: 50 minutes of work on a low, even bed, where rain on a window gives way to a stream and the stream
    /// to wind in the firs, each over ten minutes, then a 10-minute break by the fire with a warm pad. A bell marks the
    /// start of the work and of the break. Points every 5 minutes.
    /// </summary>
    public static Scene DeepWork(ulong seed = 1) => new("Deep Work", TimeKind.Loop, TimeSpan.FromMinutes(60),
    [
        Ring("brown", Curve.Even(0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.3f, 0.1f, 0.1f, 0.3f)),
        Ring("rain-window", Curve.Even(0.55f, 0.6f, 0.55f, 0.4f, 0.15f, 0.03f, 0, 0, 0, 0, 0, 0.1f, 0.55f)),
        Ring("stream", Curve.Even(0, 0, 0.05f, 0.25f, 0.5f, 0.6f, 0.55f, 0.35f, 0.12f, 0.02f, 0, 0, 0)),
        Ring("wind", Curve.Even(0, 0, 0, 0, 0, 0, 0.08f, 0.3f, 0.45f, 0.4f, 0.1f, 0, 0)),
        Ring("fire", Curve.Even(0, 0, 0, 0, 0, 0, 0, 0, 0, 0.08f, 0.5f, 0.55f, 0)),
        Ring("pad-warm", Curve.Even(0, 0, 0, 0, 0, 0, 0, 0, 0, 0.05f, 0.35f, 0.4f, 0)),
        Ring("temple-bell", Curve.Even(0.5f)) with
        {
            Play = new EveryPlay(TimeSpan.FromMinutes(50), TimeSpan.Zero, Strength: 0.7f)
        }
    ])
    {
        Id = "deep-work",
        Seed = seed,
        Intent = Intent.Focus,
        Ground = Ground.Paper,
        Phases = [new Phase("Work", 0, 50 / 60d), new Phase("Break", 50 / 60d, 1)]
    };

    /// <summary>
    /// A 20-minute sitting: a stream to arrive by; a sung bowl and a pad that breathes through the quiet middle, a bowl
    /// struck every 5 minutes; birds and a shimmer to return to. Points every 2 minutes.
    /// </summary>
    public static Scene Sit(ulong seed = 1) => new("Sit", TimeKind.Journey, TimeSpan.FromMinutes(20),
    [
        Ring("stream", Curve.Even(0.6f, 0.4f, 0.15f, 0.03f, 0, 0, 0, 0, 0, 0.1f, 0.35f)),
        Ring("bowl-sung", Curve.Even(0, 0.08f, 0.3f, 0.4f, 0.35f, 0.3f, 0.35f, 0.3f, 0.15f, 0.03f, 0)),
        Ring("pad-breath", Curve.Even(0, 0, 0.1f, 0.25f, 0.35f, 0.4f, 0.35f, 0.25f, 0.1f, 0, 0)),
        Ring("bowl", Curve.Even(0.5f)) with
        {
            Play = new EveryPlay(TimeSpan.FromMinutes(5), TimeSpan.Zero, Strength: 0.8f)
        },
        Ring("birds-dawn", Curve.Even(0, 0, 0, 0, 0, 0, 0, 0, 0.08f, 0.3f, 0.5f)),
        Ring("pad-shimmer", Curve.Even(0, 0, 0, 0, 0, 0, 0, 0.05f, 0.2f, 0.3f, 0.15f))
    ])
    {
        Id = "sit",
        Seed = seed,
        Intent = Intent.Meditate,
        Ground = Ground.Ember,
        Phases = [new Phase("Arrive", 0, 3 / 20d), new Phase("Sit", 3 / 20d, 17 / 20d), new Phase("Return", 17 / 20d, 1)]
    };

    /// <summary>
    /// A 32-minute loop to unwind to: a warm pad throughout, a tide of harmony and the chant rising and falling over it,
    /// and a slow beat that comes in once the room has settled and leaves before the turn. Points every 4 minutes.
    /// </summary>
    public static Scene Unwind(ulong seed = 1) => new("Unwind", TimeKind.Loop, TimeSpan.FromMinutes(32),
    [
        Ring("pad-warm", Curve.Even(0.6f, 0.7f, 0.75f, 0.65f, 0.6f, 0.65f, 0.75f, 0.7f, 0.6f)),
        Ring("pad-tide", Curve.Even(0.2f, 0.35f, 0.55f, 0.4f, 0.25f, 0.45f, 0.6f, 0.4f, 0.2f)),
        Ring("rhythm-downtempo", Curve.Even(0, 0.05f, 0.4f, 0.55f, 0.6f, 0.55f, 0.45f, 0.1f, 0)),
        Ring("choir", Curve.Even(0.05f, 0.1f, 0.35f, 0.6f, 0.3f, 0.55f, 0.4f, 0.15f, 0.05f))
    ])
    {
        Id = "unwind",
        Seed = seed,
        Tempo = 90,
        Intent = Intent.Relax,
        Ground = Ground.Ember,
        Phases = [new Phase("Settle", 0, 8 / 32d), new Phase("Flow", 8 / 32d, 24 / 32d), new Phase("Return", 24 / 32d, 1)]
    };

    /// <summary>
    /// The real day, one sound at a time handing over to the next: the sea and an owl's wood through the night, birds at
    /// dawn, a stream in the morning, wind in the grass at noon, rain through the afternoon, birds by a pond before
    /// evening and crickets at dusk. Points every hour.
    /// </summary>
    public static Scene AllDay(ulong seed = 1) => new("All Day", TimeKind.Clock, TimeSpan.FromDays(1),
    [
        Ring("birds-dawn", Curve.Even(0, 0, 0, 0, 0, 0.3f, 0.8f, 0.85f, 0.5f, 0.2f, 0.05f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
        Ring("stream", Curve.Even(0, 0, 0, 0, 0, 0, 0.1f, 0.4f, 0.8f, 0.75f, 0.5f, 0.2f, 0.05f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
        Ring("wind-grass", Curve.Even(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.15f, 0.45f, 0.55f, 0.35f, 0.1f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)),
        Ring("rain", Curve.Even(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.25f, 0.55f, 0.65f, 0.55f, 0.25f, 0.05f, 0, 0, 0, 0, 0, 0)),
        Ring("birds-pond", Curve.Even(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.1f, 0.35f, 0.45f, 0.25f, 0.05f, 0, 0, 0, 0)),
        Ring("crickets", Curve.Even(0.1f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.1f, 0.4f, 0.65f, 0.6f, 0.35f, 0.2f, 0.1f)),
        Ring("sea", Curve.Even(0.55f, 0.6f, 0.55f, 0.4f, 0.3f, 0.2f, 0.05f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0.1f, 0.3f, 0.5f, 0.55f)),
        Ring("night-woods", Curve.Even(0, 0.1f, 0.3f, 0.35f, 0.2f, 0.05f, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
    ])
    {
        Id = "all-day",
        Seed = seed,
        Intent = Intent.Background,
        Ground = Ground.Sky,
        Phases = [new Phase("Night", 0, 6 / 24d), new Phase("Morning", 6 / 24d, 12 / 24d), new Phase("Afternoon", 12 / 24d, 18 / 24d), new Phase("Evening", 18 / 24d, 1)]
    };
}
