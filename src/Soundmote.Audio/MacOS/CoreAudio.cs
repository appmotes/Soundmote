using System.Runtime.InteropServices;

namespace Soundmote.Audio.MacOS;

/// <summary>What the live output and the M4A writer share of AudioToolbox.</summary>
internal static class CoreAudio
{
    public const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    public const uint LinearPcm = 0x6C70636D; // 'lpcm'
    public const uint FloatPacked = 1 | 8;     // kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked

    /// <summary>32-bit float stereo, interleaved: the format the engine renders.</summary>
    public static AudioStreamBasicDescription FloatStereo(int sampleRate) => new()
    {
        SampleRate = sampleRate,
        FormatId = LinearPcm,
        FormatFlags = FloatPacked,
        BytesPerPacket = 8,
        FramesPerPacket = 1,
        BytesPerFrame = 8,
        ChannelsPerFrame = 2,
        BitsPerChannel = 32
    };

    public static void Check(int status, string what)
    {
        if (status != 0) throw new InvalidOperationException($"Core Audio could not {what} (OSStatus {status}).");
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct AudioStreamBasicDescription
{
    public double SampleRate;
    public uint FormatId, FormatFlags, BytesPerPacket, FramesPerPacket, BytesPerFrame, ChannelsPerFrame, BitsPerChannel, Reserved;
}
