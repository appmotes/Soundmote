using Soundmote.App.Formatting;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

public class SceneTextTests
{
    private static readonly TimeSpan Length = TimeSpan.FromMinutes(45);

    [Fact]
    public void The_demo_rings_read_as_drawn_on_the_boards()
    {
        var scene = BuiltInScenes.FallAsleep();

        Assert.Equal("Dies down slowly", SceneText.Describe(scene.Layers[0].Curve, Length));
        Assert.Equal("Settles after 20 min", SceneText.Describe(scene.Layers[1].Curve, Length));
        Assert.Equal("Settles after 15 min", SceneText.Describe(scene.Layers[2].Curve, Length));
        Assert.Equal("Swells through the middle", SceneText.Describe(scene.Layers[3].Curve, Length));
        Assert.Equal("Swells through the middle", SceneText.Describe(scene.Layers[4].Curve, Length));
        Assert.Equal("Swells through the middle", SceneText.Describe(scene.Layers[5].Curve, Length));
    }

    [Theory]
    [InlineData(new[] { 0.5f, 0.55f, 0.5f }, "Steady")]
    [InlineData(new[] { 0.1f, 0.4f, 0.9f }, "Builds to the end")]
    public void Simple_shapes_get_simple_words(float[] values, string expected) =>
        Assert.Equal(expected, SceneText.Describe(Curve.Even(values), Length));

    [Fact]
    public void A_clock_scene_reads_in_hours_of_the_day()
    {
        var scene = BuiltInScenes.AllDay();

        Assert.Equal("Most around 07:00", SceneText.Describe(scene.Layers[0].Curve, scene.Length));
        Assert.Equal("Most around 08:00", SceneText.Describe(scene.Layers[1].Curve, scene.Length));
        Assert.Equal("Most around 20:00", SceneText.Describe(scene.Layers.Single(l => l.Recipe.Id == "crickets").Curve, scene.Length));
        Assert.Equal("12–18", SceneText.Range(scene.Phases[2], scene.Length));
        Assert.Equal("Follows the clock", SceneText.Kind(scene));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(1940, "32:20")]
    [InlineData(3725, "1:02:05")]
    public void Clock_reads_minutes_and_seconds(int seconds, string expected) =>
        Assert.Equal(expected, SceneText.Clock(TimeSpan.FromSeconds(seconds)));
}
