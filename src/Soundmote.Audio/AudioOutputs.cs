using Soundmote.Audio.MacOS;
using Soundmote.Core.Output;

namespace Soundmote.Audio;

public static class AudioOutputs
{
    public static bool LivePlaybackSupported => OperatingSystem.IsMacOS();

    /// <summary>The system's default output device. Only macOS has one so far (N1).</summary>
    public static IAudioOutput CreateDefault() => OperatingSystem.IsMacOS()
        ? new AudioQueueOutput()
        : throw new PlatformNotSupportedException("Live playback is macOS-only for now. Render to a WAV file instead.");
}
