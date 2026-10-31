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

/// <summary>The kinds of file a scene can be exported to (A17).</summary>
public enum AudioFormat
{
    /// <summary>16-bit PCM, lossless and large; written by Core on every system.</summary>
    Wav,

    /// <summary>AAC in an MPEG-4 file, small and played everywhere; written by the system's own encoder.</summary>
    M4a
}

/// <summary>A file being written from interleaved stereo blocks. Disposing it finishes the file.</summary>
public interface IAudioFileWriter : IDisposable
{
    void Write(ReadOnlySpan<float> interleaved);
}

/// <summary>The formats this system can write a scene to, and a writer for each (A17).</summary>
public interface IAudioFiles
{
    /// <summary>Best first: M4A where the system has an AAC encoder, then WAV.</summary>
    IReadOnlyList<AudioFormat> Formats { get; }

    IAudioFileWriter Create(AudioFormat format, string path, int sampleRate);
}
