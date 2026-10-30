using Soundmote.Core.Output;
using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;

namespace Soundmote.Core.Tests;

/// <summary>A13: where a ring is heard between left and right.</summary>
public class PlaceTests
{
    private const int Rate = 48000;

    private static Scene One(Place place, Play? play = null, string sound = "rain", float send = 0f, double minutes = 10) =>
        new("Place", TimeKind.Journey, TimeSpan.FromMinutes(minutes),
            [new Layer("Ring", RecipeLibrary.Get(sound), Curve.Steady(0.8f)) { Place = place, ReverbSend = send, Play = play ?? Play.Shape }]);

    [Fact]
    public void A_ring_hard_left_is_silent_on_the_right()
    {
        var samples = Render(new SceneRenderer(One(new Place(-1f)), Rate), seconds: 1);

        Assert.All(Channel(samples, 1), s => Assert.Equal(0f, s));
        Assert.True(Rms(Channel(samples, 0)) > -50);
    }

    [Fact]
    public void Moving_a_point_never_changes_its_loudness()
    {
        double Power(float pan)
        {
            var samples = Render(new SceneRenderer(One(new Place(pan, Width: 0f)), Rate), seconds: 1);
            return Math.Pow(10, Rms(Channel(samples, 0)) / 10) + Math.Pow(10, Rms(Channel(samples, 1)) / 10);
        }

        var middle = Power(0f);
        foreach (var pan in new[] { -1f, -0.4f, 0.7f })
            Assert.InRange(10 * Math.Log10(Power(pan) / middle), -0.1, 0.1);
    }

    [Fact]
    public void A_point_has_the_same_sound_in_both_ears()
    {
        var samples = Render(new SceneRenderer(One(new Place(0f, Width: 0f)), Rate), seconds: 0.5);

        Assert.Equal(Channel(samples, 0), Channel(samples, 1));
    }

    [Fact]
    public void A_crossing_ring_goes_from_left_to_right_over_the_turn()
    {
        var renderer = new SceneRenderer(One(new Place { Move = Move.Cross, Path = Curve.Even(0f, 1f) }), Rate) { Speed = 0 };

        renderer.Seek(TimeSpan.FromMinutes(1));
        Render(renderer, seconds: 0.05);
        var early = renderer.PanOf(0);
        renderer.Seek(TimeSpan.FromMinutes(9));
        Render(renderer, seconds: 0.05);

        Assert.InRange(early, -1f, -0.8f);
        Assert.InRange(renderer.PanOf(0), 0.8f, 1f);
    }

    [Fact]
    public void A_wandering_ring_stays_within_its_range()
    {
        var renderer = new SceneRenderer(One(new Place(0.5f) { Move = Move.Wander, Range = 0.3f, Seconds = 2 }), Rate);
        var pans = new List<float>();
        for (var i = 0; i < 100; i++)
        {
            Render(renderer, seconds: 0.1);
            pans.Add(renderer.PanOf(0));
        }

        Assert.All(pans, p => Assert.InRange(p, 0.2f - 1e-4f, 0.8f + 1e-4f));
        Assert.True(pans.Max() - pans.Min() > 0.2f, "it moves");
    }

    /// <summary>A bird that lands somewhere new at each call and glides there.</summary>
    [Fact]
    public void A_flitting_ring_of_strikes_moves_at_each_strike()
    {
        var play = new EveryPlay(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1), 0.8f);
        var renderer = new SceneRenderer(One(new Place { Move = Move.Flit, Range = 1f, Seconds = 0.5 }, play, "choir", minutes: 2), Rate);
        var landed = new List<float>();
        for (var call = 0; call < 6; call++)
        {
            Render(renderer, seconds: 3);
            var settled = renderer.PanOf(0);
            Render(renderer, seconds: 7);
            Assert.Equal(settled, renderer.PanOf(0));
            landed.Add(settled);
        }

        Assert.True(landed.Distinct().Count() >= 5, string.Join(", ", landed));
        Assert.All(landed, p => Assert.InRange(p, -1f, 1f));
    }

    [Fact]
    public void A_new_place_is_heard_from_the_next_block()
    {
        var renderer = new SceneRenderer(One(Place.Centre), Rate);
        Render(renderer, seconds: 0.1);

        renderer.SetPlace(0, new Place(1f));
        var samples = Render(renderer, seconds: 0.5);

        Assert.Equal(1f, renderer.PanOf(0));
        Assert.All(Channel(samples, 0).Skip(Rate / 10), s => Assert.Equal(0f, s));
    }

    /// <summary>A5 with every kind of move.</summary>
    [Fact]
    public void Moving_places_do_not_allocate()
    {
        Place[] places =
        [
            new(-0.5f, 0.2f) { Move = Move.Wander },
            new() { Move = Move.Cross, Path = Curve.Even(0f, 1f) },
            new(0.3f) { Move = Move.Flit, Seconds = 0.2 }
        ];
        var scene = new Scene("Moves", TimeKind.Loop, TimeSpan.FromMinutes(1),
            places.Select(p => new Layer("Ring", RecipeLibrary.Get("wind"), Curve.Steady(0.5f)) { Place = p }).ToArray());
        var renderer = new SceneRenderer(scene, Rate) { Speed = 60 };
        var block = new float[1024 * 2];
        renderer.Render(block);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 400; i++) renderer.Render(block);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static float[] Render(IAudioSource source, double seconds)
    {
        var samples = new float[(int)(seconds * Rate) * 2];
        source.Render(samples);
        return samples;
    }

    private static float[] Channel(float[] samples, int channel) => samples.Where((_, i) => i % 2 == channel).ToArray();

    private static double Rms(float[] samples)
    {
        var rms = Math.Sqrt(samples.Average(s => (double)s * s));
        return rms <= 1e-12 ? -240 : 20 * Math.Log10(rms);
    }
}
