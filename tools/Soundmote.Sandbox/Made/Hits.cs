using Soundmote.Core.Dsp;
using static Soundmote.Sandbox.Made.Stereo;

namespace Soundmote.Sandbox.Made;

/// <summary>
/// The single hits the rhythms are made of (S18): soft, low drums and tuned notes on D E F G A C, each one strike with
/// its own ringing out and no room, since the scene's room is added when it plays. A rhythm's recipe places them on the
/// scene's beat, so a tempo changes their spacing, never their pitch.
/// </summary>
internal static class Hits
{
    public static IReadOnlyList<(string Id, Func<Stereo> Make)> All { get; } =
    [
        ("hit-heart-lub", () => One(0.6, Thump(58, 80, 0.13))),
        ("hit-heart-dub", () => One(0.5, Thump(50, 66, 0.11))),
        ("hit-pulse", () => One(1.2, Pulse())),
        ("hit-kick", () => One(0.9, (t, _) => Drop(t, 48, 130) * Tau(t, 0.38) * Soft(t, 0.001))),
        ("hit-snare", () => One(0.6, Snare())),
        ("hit-shaker", () => One(0.35, Shaker(3000, 0.07))),
        ("hit-shaker-soft", () => One(0.35, Shaker(2400, 0.1))),
        ("hit-dum", () => One(1.0, Dum())),
        ("hit-tek", () => One(0.3, Tek())),
        ("hit-hand-low", () => One(1.0, Bend())),
        ("hit-hand-d4", () => One(0.8, Ring(Hz(62)))),
        ("hit-hand-a4", () => One(0.8, Ring(Hz(69)))),
        .. new[] { 62, 65, 67, 69, 72, 74 }.Select(n => ($"hit-kalimba-{n}", (Func<Stereo>)(() => One(3.5, Kalimba(Hz(n)))))),
        ("hit-bass-38", () => One(3.0, Bass(Hz(38)))),
        ("hit-bass-45", () => One(3.0, Bass(Hz(45)))),
        ("hit-drum-low", () => One(2.8, Taiko(55))),
        ("hit-drum-mid", () => One(2.4, Taiko(70)))
    ];

    private static Stereo One(double seconds, Func<double, float, double> voice)
    {
        var s = new Stereo((int)(seconds * Rate), 7);
        s.Hit(0, seconds, 1, 0, voice);
        return s;
    }

    private static double Tau(double t, double seconds) => Math.Exp(-t / seconds);

    private static double Soft(double t, double seconds = 0.002) => Math.Min(1, t / seconds);

    /// <summary>A sine whose pitch falls from <paramref name="high"/> to <paramref name="low"/> in a few hundredths of a second.</summary>
    private static double Drop(double t, double low, double high, double fall = 0.03) =>
        Math.Sin(2 * Math.PI * (low * t + (high - low) * fall * (1 - Tau(t, fall))));

    /// <summary>A soft, round beat with its octave, so it is felt low and still heard on small speakers.</summary>
    private static Func<double, float, double> Thump(double low, double high, double decay)
    {
        var body = new Svf();
        body.Set(220, 0.7f, Rate);
        return (t, n) => (Drop(t, low, high, 0.05) + 0.25 * Drop(t, 2 * low, 2 * high, 0.05) + body.Low(n) * 0.6 * Tau(t, 0.02))
            * Tau(t, decay) * Soft(t, 0.008);
    }

    /// <summary>A deep, slow-starting beat: the pulse under a pad, more felt than struck.</summary>
    private static Func<double, float, double> Pulse() =>
        (t, _) => (Drop(t, 46, 70, 0.06) + 0.3 * Drop(t, 92, 140, 0.06) * Tau(t, 0.2)) * Tau(t, 0.45) * Soft(t, 0.012);

    /// <summary>A brushed snare, its noise low and soft rather than a crack.</summary>
    private static Func<double, float, double> Snare()
    {
        var band = new Svf();
        band.Set(1500, 0.8f, Rate);
        return (t, n) => 0.4 * Math.Sin(2 * Math.PI * 180 * t) * Tau(t, 0.05) + band.Band(n) * 1.3 * Tau(t, 0.16) * Soft(t, 0.004);
    }

    private static Func<double, float, double> Shaker(float hz, double decay)
    {
        var band = new Svf();
        band.Set(hz, 1.5f, Rate);
        return (t, n) => band.Band(n) * 1.6 * (1 - Tau(t, 0.008)) * Tau(t, decay);
    }

    private static Func<double, float, double> Dum()
    {
        var low = new Svf();
        low.Set(300, 0.7f, Rate);
        return (t, n) => (Drop(t, 70, 95, 0.04) * Tau(t, 0.45) + low.Low(n) * 0.6 * Tau(t, 0.03)) * Soft(t, 0.001);
    }

    private static Func<double, float, double> Tek()
    {
        var band = new Svf();
        band.Set(2200, 1.2f, Rate);
        return (t, n) => band.Band(n) * 1.4 * Tau(t, 0.035) + 0.5 * Math.Sin(2 * Math.PI * 520 * t) * Tau(t, 0.03);
    }

    /// <summary>A low hand drum pressed as it sounds, so its pitch bends up.</summary>
    private static Func<double, float, double> Bend()
    {
        var low = new Svf();
        low.Set(400, 0.7f, Rate);
        return (t, n) => (Math.Sin(2 * Math.PI * (85 * t + 20 * (t - 0.15 * (1 - Tau(t, 0.15))))) * Tau(t, 0.55)
            + low.Low(n) * 0.5 * Tau(t, 0.02)) * Soft(t, 0.001);
    }

    /// <summary>A small tuned drum struck open: a pitch and its first overtones, and a soft tick of the finger.</summary>
    private static Func<double, float, double> Ring(double hz)
    {
        var tick = new Svf();
        tick.Set(2500, 1, Rate);
        return (t, n) => (Math.Sin(2 * Math.PI * hz * t) + 0.5 * Math.Sin(4 * Math.PI * hz * t) * Tau(t, 0.12)
            + 0.3 * Math.Sin(6 * Math.PI * hz * t) * Tau(t, 0.08)) * 0.6 * Tau(t, 0.25) + tick.Band(n) * 0.6 * Tau(t, 0.01);
    }

    /// <summary>A plucked metal tine: its note ringing long, the tine's high, unrelated overtone gone at once, a soft touch.</summary>
    private static Func<double, float, double> Kalimba(double hz)
    {
        var touch = new Svf();
        touch.Set(1800, 1, Rate);
        return (t, n) => (Math.Sin(2 * Math.PI * hz * t) * Tau(t, 1.4) + 0.25 * Math.Sin(2 * Math.PI * 2 * hz * t) * Tau(t, 0.5)
            + 0.3 * Math.Sin(2 * Math.PI * 5.4 * hz * t) * Tau(t, 0.08) + touch.Band(n) * 0.3 * Tau(t, 0.004)) * Soft(t, 0.002);
    }

    /// <summary>A warm bass note with a slow start, a held body and a long fall: a pulse under a pad.</summary>
    private static Func<double, float, double> Bass(double hz) =>
        (t, _) => (Math.Sin(2 * Math.PI * hz * t) + 0.35 * Math.Sin(4 * Math.PI * hz * t) * Tau(t, 0.8)
            + 0.12 * Math.Sin(6 * Math.PI * hz * t) * Tau(t, 0.4)) * Tau(t, 1.3) * Soft(t, 0.03);

    /// <summary>A great drum: a deep membrane with two of its overtones and the boom of its body.</summary>
    private static Func<double, float, double> Taiko(double low)
    {
        var boom = new Svf();
        boom.Set(450, 0.7f, Rate);
        return (t, n) => (Drop(t, low, low * 1.6, 0.05) + 0.4 * Drop(t, low * 1.59, low * 2.4, 0.05) * Tau(t, 0.4)
            + 0.2 * Drop(t, low * 2.14, low * 3.2, 0.05) * Tau(t, 0.25) + boom.Low(n) * 0.8 * Tau(t, 0.08)) * Tau(t, 1.1) * Soft(t, 0.002);
    }
}
