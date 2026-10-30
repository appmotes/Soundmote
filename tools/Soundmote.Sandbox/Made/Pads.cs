using static Soundmote.Sandbox.Made.Stereo;

namespace Soundmote.Sandbox.Made;

/// <summary>
/// The pads (S16): held harmonies that swell and drift, all on the notes D E F G A C, so any two sound well together.
/// Each is a minute long and loops by the recipe's fold.
/// </summary>
internal static class Pads
{
    private const double Seconds = 64;

    public static IReadOnlyList<(string Id, Func<Stereo> Make)> All { get; } =
    [
        ("pad-warm", Warm), ("pad-glass", Glass), ("pad-drone", Drone), ("pad-breath", Breath), ("pad-voices", Voices),
        ("pad-shimmer", Shimmer), ("pad-strings", Strings), ("pad-tide", Tide), ("pad-organ", Organ), ("pad-space", Space)
    ];

    private static Stereo New(ulong seed, double seconds = Seconds) => new((int)(seconds * Rate), seed);

    /// <summary>A note's level rising and falling between <paramref name="low"/> and 1 every <paramref name="period"/> seconds.</summary>
    private static Func<double, double> Sway(double period, double phase, double low = 0.6) =>
        t => low + (1 - low) * Wave(t, period, phase);

    /// <summary>Wide, soft saws on D minor with a ninth: the filter opens and closes over half a minute.</summary>
    private static Stereo Warm()
    {
        var s = New(11);
        s.Note(38, Shape.Sine, 0.5, _ => 1);
        double[] notes = [50, 57, 60, 65, 76];
        for (var n = 0; n < notes.Length; n++)
            s.Note(notes[n], Shape.Saw, 0.22, Sway(13 + 4 * n, n * 0.21), unison: 7, detuneCents: 16, vibratoCents: 3);
        s.LowPass(t => 700 + 1100 * Wave(t, 32), 0.9f);
        s.Chorus(0.35);
        s.Room(4.5f, 0.35, wrap: true);
        s.HighPass(30);
        s.Warm(1.2);
        return s;
    }

    /// <summary>Bright, bell-like tones held on D with a second and a fifth, glinting as their brightness breathes.</summary>
    private static Stereo Glass()
    {
        var s = New(12);
        double[] notes = [62, 69, 74, 76, 81];
        for (var n = 0; n < notes.Length; n++)
            s.Note(notes[n], Shape.Glass, 0.16, Sway(9 + 5 * n, n * 0.37, 0.35), unison: 2, detuneCents: 5);
        s.Note(50, Shape.Sine, 0.25, Sway(21, 0.5, 0.7));
        s.LowPass(_ => 7000, 0.7f);
        s.Chorus(0.3, rateHz: 0.15);
        s.Room(6.5f, 0.55, wrap: true, damping: 0.25f);
        s.HighPass(60);
        return s;
    }

    /// <summary>A deep, dark drone on D and its fifth that beats slowly against itself.</summary>
    private static Stereo Drone()
    {
        var s = New(13);
        s.Note(26, Shape.Sine, 0.55, Sway(19, 0, 0.8));
        s.Note(38, Shape.Sine, 0.45, _ => 1);
        s.Note(38, Shape.Saw, 0.35, Sway(23, 0.3, 0.7), unison: 5, detuneCents: 9);
        s.Note(45, Shape.Saw, 0.25, Sway(29, 0.6, 0.5), unison: 5, detuneCents: 9);
        s.Note(50, Shape.Saw, 0.15, Sway(17, 0.1, 0.4), unison: 3, detuneCents: 7);
        s.LowPass(t => 260 + 260 * Wave(t, 37), 1.4f);
        s.Room(5f, 0.3, wrap: true);
        s.HighPass(25);
        s.Warm(1.4);
        return s;
    }

    /// <summary>D minor seventh that opens on a four-second in-breath and closes on a six-second out-breath.</summary>
    private static Stereo Breath()
    {
        var s = New(14, 60);
        double[] notes = [38, 50, 53, 57, 60, 62];
        foreach (var note in notes)
            s.Note(note, note < 45 ? Shape.Sine : Shape.Saw, note < 45 ? 0.45 : 0.2, _ => 1, unison: note < 45 ? 1 : 5,
                detuneCents: 12, vibratoCents: 2);
        static double Breathing(double t)
        {
            var x = t % 10;
            return x < 4 ? 0.5 - 0.5 * Math.Cos(Math.PI * x / 4) : 0.5 + 0.5 * Math.Cos(Math.PI * (x - 4) / 6);
        }
        s.LowPass(t => 350 + 2200 * Breathing(t), 1.1f);
        s.Air(1800, 0.7f, 0.05, t => 0.2 + Breathing(t));
        s.Chorus(0.3);
        s.Room(4f, 0.35, wrap: true);
        s.HighPass(30);
        return s;
    }

    /// <summary>Saws sung through a mouth on D minor, the vowel moving slowly between "ah" and "oh": a choir of machines.</summary>
    private static Stereo Voices()
    {
        var s = New(15);
        double[] notes = [45, 50, 53, 57, 62];
        for (var n = 0; n < notes.Length; n++)
            s.Note(notes[n], Shape.Saw, 0.25, Sway(11 + 3 * n, n * 0.29, 0.55), unison: 5, detuneCents: 10, vibratoCents: 12);
        s.Vowel(t => Wave(t, 24));
        s.Chorus(0.4, depthMs: 5);
        s.Room(5f, 0.45, wrap: true);
        s.HighPass(60);
        return s;
    }

    /// <summary>High open fifths on D with air above them, glittering in and out, over a soft low D.</summary>
    private static Stereo Shimmer()
    {
        var s = New(16);
        s.Note(50, Shape.Sine, 0.3, Sway(27, 0, 0.7));
        double[] notes = [74, 81, 86, 88, 93];
        for (var n = 0; n < notes.Length; n++)
            s.Note(notes[n], Shape.Triangle, 0.12, Sway(5 + 2.5 * n, n * 0.43, 0.15), unison: 3, detuneCents: 7);
        s.Air(9000, 0.8f, 0.04, t => 0.4 + 0.6 * Wave(t, 13));
        s.Chorus(0.4, rateHz: 0.4);
        s.Room(7f, 0.6, wrap: true, damping: 0.2f);
        s.HighPass(80);
        return s;
    }

    /// <summary>A slow string section on D with a minor seventh and a ninth, each player's vibrato its own.</summary>
    private static Stereo Strings()
    {
        var s = New(17);
        double[] notes = [38, 50, 53, 57, 60, 64, 69];
        for (var n = 0; n < notes.Length; n++)
            s.Note(notes[n], Shape.Saw, n == 0 ? 0.25 : 0.17, Sway(15 + 3 * n, n * 0.17, 0.65), unison: 3, detuneCents: 9,
                vibratoCents: 9);
        s.LowPass(t => 1800 + 900 * Wave(t, 29), 0.7f);
        s.Chorus(0.4, depthMs: 6, rateHz: 0.3);
        s.Room(4f, 0.4, wrap: true);
        s.HighPass(35);
        return s;
    }

    /// <summary>
    /// A tide of harmony: notes of D minor pentatonic come and go on their own slow waves over a low D, so the chord
    /// keeps changing and never leaves the key.
    /// </summary>
    private static Stereo Tide()
    {
        var s = New(18);
        s.Note(38, Shape.Sine, 0.45, _ => 1);
        s.Note(50, Shape.Triangle, 0.25, Sway(31, 0, 0.7), unison: 3, detuneCents: 6);
        double[] notes = [53, 55, 57, 60, 62, 65, 67, 69, 72];
        for (var n = 0; n < notes.Length; n++)
        {
            var period = 16 + 3 * n;
            var phase = n * 0.31;
            s.Note(notes[n], Shape.Saw, 0.16, t => Math.Pow(Wave(t, period, phase), 2), unison: 3, detuneCents: 11, vibratoCents: 4);
        }
        s.LowPass(_ => 1600, 0.8f);
        s.Chorus(0.35);
        s.Room(5f, 0.45, wrap: true);
        s.HighPass(30);
        return s;
    }

    /// <summary>A reed organ held on D and A, its sound turning slowly round the room.</summary>
    private static Stereo Organ()
    {
        var s = New(19);
        double[] notes = [38, 50, 57, 62, 69];
        foreach (var note in notes)
            s.Note(note, Shape.Organ, note == 38 ? 0.3 : 0.2, Sway(23, note / 100.0, 0.85), unison: 2, detuneCents: 4);
        // A slow rotating speaker: each ear rises as the other falls, twice in three seconds.
        for (var i = 0; i < s.Length; i++)
        {
            var w = 2 * Math.PI * 0.66 * i / Rate;
            s.Left[i] *= (float)(1 + 0.18 * Math.Sin(w));
            s.Right[i] *= (float)(1 - 0.18 * Math.Sin(w));
        }
        s.LowPass(_ => 3500, 0.7f);
        s.Room(3.5f, 0.3, wrap: true);
        s.HighPass(35);
        s.Warm(1.1);
        return s;
    }

    /// <summary>A dark, wide pad on D with the second and the fourth, a resonant filter sweeping it like a slow searchlight.</summary>
    private static Stereo Space()
    {
        var s = New(20);
        double[] notes = [38, 45, 52, 55, 62];
        for (var n = 0; n < notes.Length; n++)
            s.Note(notes[n], Shape.Saw, 0.2, Sway(19 + 6 * n, n * 0.23, 0.5), unison: 9, detuneCents: 22, vibratoCents: 4);
        s.LowPass(t => 450 + 1500 * Math.Pow(Wave(t, 42), 1.6), 2.2f);
        s.Chorus(0.45, depthMs: 7, rateHz: 0.12);
        s.Room(7f, 0.55, wrap: true, damping: 0.5f);
        s.HighPass(30);
        s.Warm(1.2);
        return s;
    }
}
