using Soundmote.Core.Output;
using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;

namespace Soundmote.Core.Tests;

/// <summary>A14: a journey that ends by waking the listener.</summary>
public class WakeTests
{
    private const int Rate = 48000;

    private sealed class Clock(TimeSpan timeOfDay) : TimeProvider
    {
        public TimeSpan TimeOfDay { get; set; } = timeOfDay;

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero) + TimeOfDay;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private static Scene Nap(Wake? wake) => new("Nap", TimeKind.Journey, TimeSpan.FromSeconds(2),
        [new Layer("Rain", RecipeLibrary.Get("rain"), Curve.Steady(0.8f))]) { RoomSeconds = 0.5f, Wake = wake };

    private static readonly Wake Seven = Wake.Morning(TimeSpan.FromHours(7));

    [Fact]
    public void A_journey_with_a_morning_waits_in_silence_after_its_end()
    {
        var renderer = new SceneRenderer(Nap(Seven), Rate, new Clock(TimeSpan.FromHours(23)));

        var samples = Render(renderer, seconds: 6);

        Assert.False(renderer.Finished);
        Assert.Equal(0f, renderer.AmountOf(0));
        Assert.Equal(0f, renderer.WakeAmount);
        Assert.True(Rms(samples.AsSpan(samples.Length - Rate)) < -70);
    }

    [Fact]
    public void The_morning_rises_before_the_hour_and_stays_after_it()
    {
        var clock = new Clock(new TimeSpan(6, 55, 0));
        var renderer = new SceneRenderer(Nap(Seven), Rate, clock);
        Render(renderer, seconds: 3);

        Assert.InRange(renderer.WakeAmount, 0.49f, 0.51f);

        clock.TimeOfDay = new TimeSpan(7, 0, 0);
        Render(renderer, seconds: 0.1);
        clock.TimeOfDay = new TimeSpan(7, 30, 0);
        var morning = Render(renderer, seconds: 2);

        Assert.Equal(1f, renderer.WakeAmount);
        Assert.True(Rms(morning.AsSpan(morning.Length - Rate)) > -40);
    }

    [Fact]
    public void A_morning_that_was_missed_stays_silent()
    {
        var renderer = new SceneRenderer(Nap(Seven), Rate, new Clock(new TimeSpan(7, 20, 0)));

        Render(renderer, seconds: 3);

        Assert.Equal(0f, renderer.WakeAmount);
    }

    [Fact]
    public void A_rise_across_midnight_is_found()
    {
        var renderer = new SceneRenderer(Nap(Wake.Morning(TimeSpan.FromMinutes(5))), Rate, new Clock(new TimeSpan(23, 58, 0)));

        Render(renderer, seconds: 0.1);

        Assert.InRange(renderer.WakeAmount, 0.29f, 0.31f);
    }

    [Fact]
    public void Clearing_the_morning_lets_the_journey_end()
    {
        var renderer = new SceneRenderer(Nap(Seven), Rate, new Clock(TimeSpan.FromHours(23)));
        Render(renderer, seconds: 3);

        renderer.SetWake(null);

        Assert.True(renderer.Finished);
        Assert.Null(renderer.Wake);
    }

    [Fact]
    public void A_loop_never_takes_a_morning()
    {
        var loop = new SceneRenderer(Nap(null) with { Kind = TimeKind.Loop }, Rate);

        loop.SetWake(Seven);

        Assert.Null(loop.Wake);
    }

    [Fact]
    public void The_morning_does_not_change_how_the_journey_sounds()
    {
        var plain = Render(new SceneRenderer(BuiltInScenes.FallAsleep(4), Rate, new Clock(TimeSpan.FromHours(22))) { Speed = 60 }, 1);
        var waking = Render(new SceneRenderer(BuiltInScenes.FallAsleep(4) with { Wake = Seven }, Rate, new Clock(TimeSpan.FromHours(22))) { Speed = 60 }, 1);

        Assert.Equal(plain, waking);
    }

    [Fact]
    public void A_morning_comes_back_from_the_file()
    {
        var scene = BuiltInScenes.FallAsleep() with { Wake = Wake.Morning(new TimeSpan(6, 45, 0)) with { Rise = TimeSpan.FromMinutes(15) } };

        var read = SceneFile.Read(SceneFile.Write(scene), "fall-asleep").Wake!;

        Assert.Equal((new TimeSpan(6, 45, 0), TimeSpan.FromMinutes(15)), (read.At, read.Rise));
        Assert.Equal(["sea", "stream"], read.Layers.Select(l => l.Recipe.Id));
        Assert.Null(SceneFile.Read(SceneFile.Write(BuiltInScenes.Sit()), "sit").Wake);
    }

    [Fact]
    public void Waking_does_not_allocate()
    {
        var renderer = new SceneRenderer(Nap(Seven), Rate, new Clock(new TimeSpan(6, 55, 0)));
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

    private static double Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        foreach (var s in samples) sum += s * s;
        var rms = Math.Sqrt(sum / samples.Length);
        return rms <= 1e-9 ? -180 : 20 * Math.Log10(rms);
    }
}
