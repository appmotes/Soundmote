using Soundmote.Core.Scenes;

namespace Soundmote.Core.Tests;

public class BuiltInScenesTests
{
    private const int Rate = 48000;

    private sealed class Afternoon : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 14, 40, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    public static TheoryData<string> Names => new(BuiltInScenes.All().Select(s => s.Name));

    [Theory]
    [MemberData(nameof(Names))]
    public void Each_scene_is_heard_and_stays_inside_full_scale(string name)
    {
        var scene = BuiltInScenes.All(3).Single(s => s.Name == name);
        var renderer = new SceneRenderer(scene, Rate, new Afternoon()) { Speed = 0 };
        renderer.Seek(scene.Length * 0.4);
        var samples = new float[Rate * 2 * 3];
        renderer.Render(samples);

        Assert.All(samples, s => Assert.True(float.IsFinite(s) && MathF.Abs(s) < 1f));
        var rms = Math.Sqrt(samples.Skip(Rate * 2).Average(s => (double)s * s));
        Assert.InRange(20 * Math.Log10(rms), -40, -6);
    }

    [Fact]
    public void Each_scene_has_its_own_intent_and_phases_that_cover_the_turn()
    {
        var scenes = BuiltInScenes.All();

        Assert.Equal(scenes.Count, scenes.Select(s => s.Intent).Distinct().Count());
        Assert.All(scenes, s =>
        {
            Assert.Equal(0, s.Phases[0].From);
            Assert.Equal(1, s.Phases[^1].To);
            for (var i = 1; i < s.Phases.Count; i++) Assert.Equal(s.Phases[i - 1].To, s.Phases[i].From);
        });
    }

    [Fact]
    public void Deep_work_takes_its_break_in_the_last_ten_minutes()
    {
        var scene = BuiltInScenes.DeepWork();
        var fire = scene.Layers.Single(l => l.Recipe.Id == "fire").Curve;
        var bell = scene.Layers.Single(l => l.Recipe.Id == "temple-bell").Play;

        Assert.Equal(0, fire.At(0.4));
        Assert.True(fire.At(55 / 60d) > 0.5f);
        Assert.Equal([0, 50 / 60d], bell.Strikes(scene.Length, 1).Select(s => s.At));
    }
}
