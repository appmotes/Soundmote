using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;

namespace Soundmote.Core.Tests;

public class SceneFileTests
{
    private const int Rate = 48000;

    public static TheoryData<string> Ids => new(BuiltInScenes.All().Select(s => s.Id));

    /// <summary>N3: a scene read back from its file renders the same samples (A6).</summary>
    [Theory]
    [MemberData(nameof(Ids))]
    public void A_scene_read_back_renders_the_same_samples(string id)
    {
        var scene = BuiltInScenes.All(7).Single(s => s.Id == id);

        var read = SceneFile.Read(SceneFile.Write(scene), id);

        Assert.Equal(Render(scene), Render(read));
        Assert.Equal((scene.Id, scene.Name, scene.Kind, scene.Length, scene.Intent, scene.Ground, scene.Seed),
            (read.Id, read.Name, read.Kind, read.Length, read.Intent, read.Ground, read.Seed));
        Assert.Equal(scene.Phases, read.Phases);
        Assert.Equal(scene.Layers.Select(l => (l.Name, l.Colour, l.Recipe.Id, l.Play.GetType())),
            read.Layers.Select(l => (l.Name, l.Colour, l.Recipe.Id, l.Play.GetType())));
    }

    [Fact]
    public void Every_way_to_play_comes_back_the_same()
    {
        Play[] plays =
        [
            Play.Shape,
            new OnOffPlay([new Stretch(0.1, 0.3), new Stretch(0.6, 0.9)], 0.55f, 12.5),
            new EveryPlay(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30), 0.7f),
            new RandomPlay(2.5, TimeSpan.FromSeconds(40), 0.3f, Curve.Even(0.2f, 1f, 0.1f), 0.65f)
        ];
        var scene = new Scene("Plays", TimeKind.Loop, TimeSpan.FromMinutes(30),
            plays.Select(p => new Layer("Choir", RecipeLibrary.Get("choir"), Curve.Even(0.1f, 0.9f)) { Play = p }).ToArray());

        var read = SceneFile.Read(SceneFile.Write(scene), "plays");

        for (var i = 0; i < plays.Length; i++)
            Assert.Equal(plays[i].Strikes(scene.Length, 9), read.Layers[i].Play.Strikes(scene.Length, 9));
        Assert.Equal(((OnOffPlay)plays[1]).Stretches, ((OnOffPlay)read.Layers[1].Play).Stretches);
        Assert.Equal(((RandomPlay)plays[3]).Busy.Points, ((RandomPlay)read.Layers[3].Play).Busy.Points);
        Assert.IsType<ShapePlay>(read.Layers[0].Play);
    }

    [Fact]
    public void A_place_comes_back_and_a_ring_without_one_is_in_the_middle()
    {
        var place = new Place(-0.6f, 0.3f) { Move = Move.Cross, Range = 0.8f, Seconds = 3.5, Path = Curve.Even(0.1f, 0.9f, 0.2f) };
        var scene = new Scene("Birds", TimeKind.Journey, TimeSpan.FromMinutes(10),
            [new Layer("Choir", RecipeLibrary.Get("choir"), Curve.Steady(0.5f)) { Place = place }]);

        var read = SceneFile.Read(SceneFile.Write(scene), "birds").Layers[0].Place;

        Assert.Equal((place.Pan, place.Width, place.Move, place.Range, place.Seconds), (read.Pan, read.Width, read.Move, read.Range, read.Seconds));
        Assert.Equal(place.Path.Points, read.Path.Points);

        var old = System.Text.RegularExpressions.Regex.Replace(SceneFile.Write(scene), @",\s*""place"": \{[^}]*\}", "");
        Assert.DoesNotContain("place", old);
        Assert.Same(Place.Centre, SceneFile.Read(old, "birds").Layers[0].Place);
    }

    [Fact]
    public void A_scenes_tempo_comes_back_and_one_without_it_keeps_a_resting_pulse()
    {
        var scene = BuiltInScenes.Unwind() with { Tempo = 72 };

        Assert.Equal(72, SceneFile.Read(SceneFile.Write(scene), "unwind").Tempo);

        var old = System.Text.RegularExpressions.Regex.Replace(SceneFile.Write(scene), @"\s*""tempo"": [0-9.]+,", "");
        Assert.DoesNotContain("\"tempo\"", old);
        Assert.Equal(Scene.DefaultTempo, SceneFile.Read(old, "unwind").Tempo);
    }

    [Fact]
    public void A_rings_waver_comes_back_and_a_ring_without_one_has_the_default()
    {
        var scene = new Scene("Pads", TimeKind.Journey, TimeSpan.FromMinutes(10),
            [new Layer("Warm pad", RecipeLibrary.Get("pad-warm"), Curve.Steady(0.5f)) { Waver = 0.8f }]);

        Assert.Equal(0.8f, SceneFile.Read(SceneFile.Write(scene), "pads").Layers[0].Waver);

        var old = System.Text.RegularExpressions.Regex.Replace(SceneFile.Write(scene), @",\s*""waver"": [0-9.]+", "");
        Assert.DoesNotContain("waver", old);
        Assert.Equal(Layer.DefaultWaver, SceneFile.Read(old, "pads").Layers[0].Waver);
    }

    [Theory]
    [InlineData("not json", "Not valid JSON")]
    [InlineData("""{ "format": 1, "kind": "journey" }""", "'seconds' is missing")]
    [InlineData("""{ "format": 9 }""", "newer Soundmote")]
    public void A_broken_file_says_what_is_wrong(string json, string message)
    {
        var e = Assert.Throws<SceneFileException>(() => SceneFile.Read(json, "x"));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void A_ring_whose_sound_is_gone_is_left_out()
    {
        var json = SceneFile.Write(BuiltInScenes.FallAsleep()).Replace("\"rain\"", "\"gone\"");

        var scene = SceneFile.Read(json, "x");

        Assert.Equal(BuiltInScenes.FallAsleep().Layers.Where(l => l.Recipe.Id != "rain").Select(l => l.Name), scene.Layers.Select(l => l.Name));
    }

    /// <summary>A clock scene reads the time; both renders must read the same one.</summary>
    private sealed class Stopped : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 28, 9, 15, 0, TimeSpan.Zero);
    }

    private static float[] Render(Scene scene)
    {
        var renderer = new SceneRenderer(scene, Rate, new Stopped()) { Speed = 120 };
        var samples = new float[Rate * 2];
        renderer.Render(samples);
        return samples;
    }
}
