using Soundmote.Core.Output;
using Soundmote.Core.Scenes;
using Soundmote.Core.Recipes;

namespace Soundmote.Core.Tests;

public class SceneRendererTests
{
    private const int Rate = 48000;

    [Fact]
    public void Same_seed_renders_the_same_samples()
    {
        var a = Render(new SceneRenderer(BuiltInScenes.FallAsleep(5), Rate) { Speed = 60 }, seconds: 2);
        var b = Render(new SceneRenderer(BuiltInScenes.FallAsleep(5), Rate) { Speed = 60 }, seconds: 2);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Another_seed_renders_other_samples()
    {
        var a = Render(new SceneRenderer(BuiltInScenes.FallAsleep(5), Rate), seconds: 1);
        var b = Render(new SceneRenderer(BuiltInScenes.FallAsleep(6), Rate), seconds: 1);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void A_whole_journey_stays_finite_and_inside_full_scale()
    {
        var samples = Render(new SceneRenderer(BuiltInScenes.FallAsleep(), Rate) { Speed = 150 }, seconds: 20);

        Assert.All(samples, s => Assert.True(float.IsFinite(s) && MathF.Abs(s) <= 1f));
    }

    [Fact]
    public void The_hand_follows_the_curve()
    {
        var renderer = new SceneRenderer(BuiltInScenes.FallAsleep(), Rate) { Speed = 0 };
        renderer.Seek(TimeSpan.FromMinutes(22.5));
        Render(renderer, seconds: 0.1);

        Assert.Equal(0.5, renderer.Position, 3);
        var choir = renderer.Scene.Layers[2].Curve.At(0.5);
        Assert.InRange(renderer.AmountOf(2), choir * 0.85f, choir * 1.15f);
    }

    [Fact]
    public void A_journey_ends_in_silence()
    {
        var scene = new Scene("Short", TimeKind.Journey, TimeSpan.FromSeconds(2),
            [new Layer("Rain", RecipeLibrary.Get("rain"), Curve.Steady(1f))]) { RoomSeconds = 1f };
        var renderer = new SceneRenderer(scene, Rate);

        var samples = Render(renderer, seconds: 6);

        Assert.True(renderer.Finished);
        Assert.Equal(0f, renderer.AmountOf(0));
        Assert.True(Rms(samples.AsSpan(samples.Length - Rate)) < -70);
    }

    [Fact]
    public void A_loop_counts_laps_and_starts_the_curve_again()
    {
        var scene = new Scene("Loop", TimeKind.Loop, TimeSpan.FromSeconds(10),
            [new Layer("Wind", RecipeLibrary.Get("wind"), Curve.Even(0f, 1f))]);
        var renderer = new SceneRenderer(scene, Rate) { Speed = 0 };

        renderer.Seek(TimeSpan.FromSeconds(25));
        Render(renderer, seconds: 0.05);

        Assert.Equal(3, renderer.Lap);
        Assert.Equal(0.5, renderer.Position, 3);
    }

    [Fact]
    public void Pause_fades_out_and_holds_the_hand()
    {
        var renderer = new SceneRenderer(BuiltInScenes.FallAsleep(), Rate) { Speed = 1 };
        renderer.Seek(TimeSpan.FromMinutes(12));
        Render(renderer, seconds: 1);

        renderer.Paused = true;
        var samples = Render(renderer, seconds: 3);
        var held = renderer.Elapsed;
        Render(renderer, seconds: 1);

        Assert.Equal(held, renderer.Elapsed);
        Assert.True(Rms(samples.AsSpan(samples.Length - Rate)) < -80);
    }

    /// <summary>A15: a long fade is a straight line of the asked length; playing again brings back the short one.</summary>
    [Fact]
    public void A_long_fade_out_lasts_as_long_as_asked()
    {
        var renderer = new SceneRenderer(BuiltInScenes.FallAsleep(), Rate) { Speed = 1 };
        renderer.Seek(TimeSpan.FromMinutes(12));
        Render(renderer, seconds: 1);

        renderer.FadeOutSeconds = 2;
        renderer.Paused = true;
        var fading = Render(renderer, seconds: 1.5);
        var after = Render(renderer, seconds: 1);

        Assert.True(Rms(fading.AsSpan(fading.Length - Rate / 2)) > -60, "still heard a second in");
        Assert.True(Rms(after.AsSpan(after.Length - Rate / 2)) < -80);
    }

    [Fact]
    public void Muting_a_ring_takes_it_out_of_the_mix()
    {
        var scene = new Scene("Mute", TimeKind.Journey, TimeSpan.FromHours(1),
            [new Layer("Choir", RecipeLibrary.Get("choir"), Curve.Steady(1f))]) { RoomSeconds = 0.5f };
        var renderer = new SceneRenderer(scene, Rate);
        Render(renderer, seconds: 1);

        renderer.SetMuted(0, true);
        var samples = Render(renderer, seconds: 3);

        Assert.True(Rms(samples.AsSpan(samples.Length - Rate)) < -80);
    }

    /// <summary>A5: the audio thread must not allocate, or the GC can interrupt playback.</summary>
    [Fact]
    public void Rendering_does_not_allocate()
    {
        var renderer = new SceneRenderer(BuiltInScenes.FallAsleep(), Rate) { Speed = 60 };
        var block = new float[1024 * 2];
        renderer.Render(block);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 400; i++) renderer.Render(block);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Wav_file_has_a_correct_header_and_length()
    {
        using var stream = new MemoryStream();
        WavWriter.Write(stream, new SceneRenderer(BuiltInScenes.FallAsleep(), Rate), TimeSpan.FromSeconds(1.5));
        var bytes = stream.ToArray();

        Assert.Equal("RIFF"u8.ToArray(), bytes[..4]);
        Assert.Equal("WAVE"u8.ToArray(), bytes[8..12]);
        Assert.Equal((uint)Rate, BitConverter.ToUInt32(bytes, 24));
        Assert.Equal((uint)(Rate * 1.5 * 4), BitConverter.ToUInt32(bytes, 40));
        Assert.Equal(44 + Rate * 1.5 * 4, bytes.Length);
    }

    private static float[] Render(IAudioSource source, double seconds)
    {
        var samples = new float[(int)(seconds * Rate) * 2];
        source.Render(samples);
        return samples;
    }

    private static double Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        foreach (var s in samples) sum += s * s;
        var rms = Math.Sqrt(sum / samples.Length);
        return rms <= 1e-9 ? -180 : 20 * Math.Log10(rms);
    }

    [Fact]
    public void A_new_curve_is_heard_from_the_next_block()
    {
        var renderer = new SceneRenderer(BuiltInScenes.FallAsleep(), Rate) { Speed = 0 };
        renderer.Seek(TimeSpan.FromMinutes(4));
        Render(renderer, seconds: 0.05);
        Assert.True(renderer.AmountOf(0) > 0.6f);

        renderer.SetCurve(0, Curve.Steady(0f));
        Render(renderer, seconds: 0.05);

        Assert.Equal(0f, renderer.AmountOf(0));
        Assert.Same(renderer.CurveOf(0), renderer.CurveOf(0));
    }

    [Fact]
    public void A_ring_added_while_playing_is_heard_and_one_removed_fades_out()
    {
        var scene = new Scene("Add", TimeKind.Journey, TimeSpan.FromHours(1),
            [new Layer("Wind", RecipeLibrary.Get("wind"), Curve.Steady(0f))]) { RoomSeconds = 0.5f };
        var renderer = new SceneRenderer(scene, Rate);
        Render(renderer, seconds: 0.5);

        var index = renderer.AddLayer(new Layer("Sea", RecipeLibrary.Get("sea"), Curve.Steady(0.8f)));
        var playing = Render(renderer, seconds: 3);

        Assert.Equal(1, index);
        Assert.Equal(2, renderer.LayerCount);
        Assert.Equal("Sea", renderer.LayerAt(1).Name);
        Assert.True(Rms(playing.AsSpan(playing.Length - Rate)) > -45);

        renderer.RemoveLayer(1);
        var after = Render(renderer, seconds: 2);

        Assert.Equal(1, renderer.LayerCount);
        Assert.True(Rms(after.AsSpan(after.Length - Rate)) < -80);
    }

    [Fact]
    public void Rendering_does_not_allocate_after_rings_are_added_and_removed()
    {
        var renderer = new SceneRenderer(BuiltInScenes.FallAsleep(), Rate) { Speed = 60 };
        renderer.AddLayer(new Layer("Fire", RecipeLibrary.Get("fire"), Curve.Steady(0.5f)));
        renderer.RemoveLayer(0);
        var block = new float[1024 * 2];
        renderer.Render(block);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 400; i++) renderer.Render(block);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
