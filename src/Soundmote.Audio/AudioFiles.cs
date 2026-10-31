using Soundmote.Core.Output;

namespace Soundmote.Audio;

/// <summary>
/// The files a scene can be exported to on this system (A17): M4A through the system's own AAC encoder on macOS and
/// Windows, and WAV everywhere. Linux has no AAC encoder of its own, so it writes WAV only.
/// </summary>
public sealed class AudioFiles : IAudioFiles
{
    public IReadOnlyList<AudioFormat> Formats { get; } =
        OperatingSystem.IsMacOS() || OperatingSystem.IsWindows() ? [AudioFormat.M4a, AudioFormat.Wav] : [AudioFormat.Wav];

    /// <param name="tags">Written in both formats on macOS; Windows writes none in M4A yet (W11).</param>
    public IAudioFileWriter Create(AudioFormat format, string path, int sampleRate, AudioTags? tags = null)
    {
        if (format == AudioFormat.Wav) return new WavWriter(path, sampleRate, tags);
        if (OperatingSystem.IsMacOS()) return new MacOS.M4aWriter(path, sampleRate, tags);
        if (OperatingSystem.IsWindows()) return new Windows.M4aWriter(path, sampleRate);
        throw new PlatformNotSupportedException("This system has no AAC encoder; export to WAV instead.");
    }
}
