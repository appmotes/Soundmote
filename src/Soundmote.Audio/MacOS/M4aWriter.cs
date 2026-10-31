using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Soundmote.Core.Output;

namespace Soundmote.Audio.MacOS;

/// <summary>
/// Writes AAC in an M4A file through AudioToolbox's ExtAudioFile, which encodes as it writes (A17). It takes the
/// engine's float stereo as it is.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed unsafe partial class M4aWriter : IAudioFileWriter
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const uint M4aFile = 0x6D346166;        // 'm4af'
    private const uint Aac = 0x61616320;            // 'aac '
    private const uint EraseFile = 1;               // kAudioFileFlags_EraseFile
    private const uint ClientDataFormat = 0x63666D74; // 'cfmt'
    private const uint AudioConverter = 0x61636E76;   // 'acnv'
    private const uint ConverterConfig = 0x61636366;  // 'accf'
    private const uint EncodeBitRate = 0x62726174;    // 'brat'

    /// <summary>Bits a second, the same as on Windows, where it is the encoder's highest.</summary>
    public const uint BitRate = 192_000;

    private nint _file;

    public M4aWriter(string path, int sampleRate)
    {
        var bytes = Encoding.UTF8.GetBytes(Path.GetFullPath(path));
        nint url;
        fixed (byte* p = bytes) url = CFURLCreateFromFileSystemRepresentation(0, p, bytes.Length, 0);
        if (url == 0) throw new IOException($"Not a path the system can write: {path}");
        try
        {
            var aac = new AudioStreamBasicDescription { SampleRate = sampleRate, FormatId = Aac, FramesPerPacket = 1024, ChannelsPerFrame = 2 };
            CoreAudio.Check(ExtAudioFileCreateWithURL(url, M4aFile, &aac, 0, EraseFile, out _file), "create the file");
        }
        finally
        {
            CFRelease(url);
        }
        try
        {
            var pcm = CoreAudio.FloatStereo(sampleRate);
            CoreAudio.Check(ExtAudioFileSetProperty(_file, ClientDataFormat, (uint)sizeof(AudioStreamBasicDescription), &pcm), "set the input format");
            nint converter;
            var size = (uint)sizeof(nint);
            CoreAudio.Check(ExtAudioFileGetProperty(_file, AudioConverter, &size, &converter), "find the encoder");
            var rate = BitRate;
            CoreAudio.Check(AudioConverterSetProperty(converter, EncodeBitRate, sizeof(uint), &rate), "set the bit rate");
            // The file's converter takes the new bit rate only once its configuration is set again, here to none.
            nint config = 0;
            CoreAudio.Check(ExtAudioFileSetProperty(_file, ConverterConfig, (uint)sizeof(nint), &config), "apply the bit rate");
        }
        catch
        {
            ExtAudioFileDispose(_file);
            _file = 0;
            throw;
        }
    }

    public void Write(ReadOnlySpan<float> interleaved)
    {
        fixed (float* p = interleaved)
        {
            var list = new AudioBufferList
            {
                NumberBuffers = 1,
                NumberChannels = 2,
                DataByteSize = (uint)(interleaved.Length * sizeof(float)),
                Data = (nint)p
            };
            CoreAudio.Check(ExtAudioFileWrite(_file, (uint)(interleaved.Length / 2), &list), "write");
        }
    }

    /// <summary>Encodes what is left and closes the file.</summary>
    public void Dispose()
    {
        if (_file == 0) return;
        var status = ExtAudioFileDispose(_file);
        _file = 0;
        CoreAudio.Check(status, "finish the file");
    }

    /// <summary>One interleaved buffer. The buffer starts at 8, after the count and its padding.</summary>
    [StructLayout(LayoutKind.Explicit)]
    private struct AudioBufferList
    {
        [FieldOffset(0)] public uint NumberBuffers;
        [FieldOffset(8)] public uint NumberChannels;
        [FieldOffset(12)] public uint DataByteSize;
        [FieldOffset(16)] public nint Data;
    }

    [LibraryImport(CoreFoundation)]
    private static partial nint CFURLCreateFromFileSystemRepresentation(nint allocator, byte* buffer, nint length, byte isDirectory);

    [LibraryImport(CoreFoundation)]
    private static partial void CFRelease(nint value);

    [LibraryImport(CoreAudio.AudioToolbox)]
    private static partial int ExtAudioFileCreateWithURL(nint url, uint fileType, AudioStreamBasicDescription* format,
        nint channelLayout, uint flags, out nint file);

    [LibraryImport(CoreAudio.AudioToolbox)]
    private static partial int ExtAudioFileSetProperty(nint file, uint property, uint size, void* data);

    [LibraryImport(CoreAudio.AudioToolbox)]
    private static partial int ExtAudioFileGetProperty(nint file, uint property, uint* size, void* data);

    [LibraryImport(CoreAudio.AudioToolbox)]
    private static partial int ExtAudioFileWrite(nint file, uint frames, AudioBufferList* data);

    [LibraryImport(CoreAudio.AudioToolbox)]
    private static partial int ExtAudioFileDispose(nint file);

    [LibraryImport(CoreAudio.AudioToolbox)]
    private static partial int AudioConverterSetProperty(nint converter, uint property, uint size, void* data);
}
