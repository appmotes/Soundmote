namespace Soundmote.Core.Output;

/// <summary>Anything that fills stereo blocks. Render runs on the audio thread (A5).</summary>
public interface IAudioSource
{
    int SampleRate { get; }

    /// <summary>Fills interleaved stereo frames: left, right, left, right…</summary>
    void Render(Span<float> interleaved);
}

/// <summary>A device that pulls blocks from a source in real time (A7).</summary>
public interface IAudioOutput : IDisposable
{
    void Start(IAudioSource source);

    void Stop();

    /// <summary>Device volume, 0 to 1. Separate from the scene's own levels.</summary>
    float Volume { get; set; }
}
