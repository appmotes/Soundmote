using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;

namespace Soundmote.Core.Tests;

public class PlayTests
{
    private const int Rate = 48000;
    private static readonly TimeSpan Length = TimeSpan.FromMinutes(45);

    [Fact]
    public void On_off_is_on_inside_its_stretches_and_fades_at_the_ends()
    {
        var play = new OnOffPlay([new Stretch(10 / 45.0, 18 / 45.0)], Level: 0.7f, FadeSeconds: 20);

        Assert.Equal(0f, play.At(5 / 45.0, Length));
        Assert.Equal(0.7f, play.At(14 / 45.0, Length), 4);
        Assert.Equal(0.35f, play.At((600 + 10) / 2700.0, Length), 2);
        Assert.Equal(0f, play.At(20 / 45.0, Length));
        Assert.Equal(TimeSpan.FromMinutes(8), play.OnFor(Length));
    }

    [Fact]
    public void Every_strikes_on_time_until_the_end()
    {
        var strikes = new EveryPlay(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1)).Strikes(Length, 1);

        Assert.Equal(9, strikes.Length);
        Assert.Equal(1 / 45.0, strikes[0].At, 6);
        Assert.Equal(41 / 45.0, strikes[^1].At, 6);
    }

    [Fact]
    public void Random_keeps_its_rate_its_gap_and_its_seed()
    {
        var play = new RandomPlay(6, TimeSpan.FromSeconds(20), Vary: 0.3f, Busy: Curve.Steady(1f));

        var a = play.Strikes(TimeSpan.FromHours(10), 7);
        var b = play.Strikes(TimeSpan.FromHours(10), 7);
        var c = play.Strikes(TimeSpan.FromHours(10), 8);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.InRange(a.Length, 300, 420);
        Assert.All(a.Zip(a.Skip(1), (x, y) => (y.At - x.At) * 36000), gap => Assert.True(gap >= 20));
        Assert.All(a, s => Assert.InRange(s.Strength, 0.1f, 1f));
    }

    [Fact]
    public void Random_is_busier_where_its_curve_is_high()
    {
        var strikes = new RandomPlay(10, TimeSpan.Zero, 0, Curve.Even(0f, 1f)).Strikes(TimeSpan.FromHours(10), 3);

        Assert.True(strikes.Count(s => s.At > 0.5) > 2.5 * strikes.Count(s => s.At < 0.5));
    }

    [Fact]
    public void A_strike_sounds_when_the_hand_passes_it_and_not_when_it_jumps_over()
    {
        var scene = new Scene("Bell", TimeKind.Journey, TimeSpan.FromMinutes(10),
            [new Layer("Bell", RecipeLibrary.Get("wind"), Curve.Steady(0f)) { Play = new EveryPlay(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)) }]);
        var renderer = new SceneRenderer(scene, Rate) { Speed = 1 };
        var block = new float[1024];

        renderer.Seek(TimeSpan.FromSeconds(298));
        for (var i = 0; i < 48000 / 512; i++) renderer.Render(block);
        Assert.Equal(0f, renderer.AmountOf(0));
        for (var i = 0; i < 2 * 48000 / 512; i++) renderer.Render(block);
        Assert.True(renderer.AmountOf(0) > 0.3f, "struck at 5:00");

        renderer.Seek(TimeSpan.FromSeconds(9 * 60 + 50));
        for (var i = 0; i < 4 * 48000 / 512; i++) renderer.Render(block);
        renderer.Seek(TimeSpan.FromSeconds(4 * 60));
        renderer.Seek(TimeSpan.FromSeconds(6 * 60));
        for (var i = 0; i < 48000 / 512; i++) renderer.Render(block);
        Assert.True(renderer.AmountOf(0) < 0.05f, "a seek over 5:00 does not strike");
    }

    [Fact]
    public void A_ring_can_change_how_it_plays_while_playing()
    {
        var renderer = new SceneRenderer(BuiltInScenes.FallAsleep(), Rate) { Speed = 0 };
        renderer.Seek(TimeSpan.FromMinutes(20));
        var block = new float[1024];
        renderer.Render(block);

        renderer.SetPlay(0, new OnOffPlay([new Stretch(0.4, 0.5)], Level: 0.5f), seed: 1);
        renderer.Render(block);

        Assert.Equal(0.5f, renderer.AmountOf(0), 3);
        Assert.IsType<OnOffPlay>(renderer.PlayOf(0));
        Assert.Empty(renderer.StrikesOf(0));
    }
}
