using Soundmote.Core.Output;

namespace Soundmote.Core.Scenes;

/// <summary>
/// Renders a scene into a file offline, as fast as the machine allows (A17): a renderer of its own at speed 1, from
/// the start of the turn or, for a clock scene, from a time of day; a short fade in, a fade out at the end, and
/// peaks at -1 dBFS.
/// </summary>
public static class SceneExport
{
    public const int SampleRate = 48000;

    /// <summary>The fade at the end; a scene shorter than four times this fades over its last quarter.</summary>
    public const double FadeOutSeconds = 5;

    /// <summary>Keeps the first sample from clicking.</summary>
    private const double FadeInSeconds = 0.05;

    /// <summary>-1 dBFS: the limiter stays below 1, and this leaves room for an encoder's overshoot.</summary>
    public const float Ceiling = 0.891f;

    /// <summary>The clock of a clock scene moves on after every block, so a block is short against the day.</summary>
    private const int BlockFrames = 2048;

    /// <param name="startOfDay">For a clock scene: the time of day the file starts at. Ignored otherwise.</param>
    /// <exception cref="OperationCanceledException">When <paramref name="cancel"/> is set; the file is left half written.</exception>
    public static void Render(Scene scene, TimeSpan length, IAudioFileWriter file, TimeSpan startOfDay = default,
        IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        var clock = new RenderClock(startOfDay);
        // A journey's file ends with the journey; a morning after it would only be silence until the wake time (A14).
        var renderer = new SceneRenderer(scene with { Wake = null }, SampleRate, clock);
        var frames = (long)Math.Round(length.TotalSeconds * SampleRate);
        var fadeIn = Math.Max(1, FadeInSeconds * SampleRate);
        var fadeOut = Math.Max(1, Math.Min(FadeOutSeconds, length.TotalSeconds / 4) * SampleRate);
        var block = new float[BlockFrames * 2];
        var reported = 0.0;
        for (long done = 0; done < frames;)
        {
            cancel.ThrowIfCancellationRequested();
            var n = (int)Math.Min(BlockFrames, frames - done);
            var samples = block.AsSpan(0, n * 2);
            renderer.Render(samples);
            for (var i = 0; i < n; i++)
            {
                var at = done + i;
                var gain = Ceiling * (float)Math.Min(1, Math.Min((at + 1) / fadeIn, (frames - at) / fadeOut));
                samples[i * 2] *= gain;
                samples[i * 2 + 1] *= gain;
            }
            file.Write(samples);
            done += n;
            clock.Advance(n);
            var share = (double)done / frames;
            if (share - reported >= 0.002 || done == frames)
            {
                reported = share;
                progress?.Report(share);
            }
        }
    }

    /// <summary>Local time that moves with the frames rendered, so a clock scene plays its stretch of the day.</summary>
    private sealed class RenderClock(TimeSpan startOfDay) : TimeProvider
    {
        private readonly DateTimeOffset _start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) + startOfDay;
        private long _frames;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => _start.AddTicks(_frames * TimeSpan.TicksPerSecond / SampleRate);

        public void Advance(int frames) => _frames += frames;
    }
}
