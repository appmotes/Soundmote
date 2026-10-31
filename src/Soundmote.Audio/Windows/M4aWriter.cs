using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Soundmote.Core.Output;

namespace Soundmote.Audio.Windows;

/// <summary>
/// Writes AAC in an M4A file through Media Foundation's sink writer and the system's AAC encoder (A17). The encoder
/// takes 16-bit PCM only, so each block is converted first. COM methods are called by their place in the interface
/// (the slot numbers below count IUnknown's three first). Create, write and dispose on one thread.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed unsafe partial class M4aWriter : IAudioFileWriter
{
    private const uint MfVersion = 0x00020070;
    private const uint Multithreaded = 0;

    /// <summary>192 kbps, the encoder's highest for stereo.</summary>
    private const uint AacBytesPerSecond = 24_000;

    private static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    private static readonly Guid Subtype = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    private static readonly Guid Audio = new("73647561-0000-0010-8000-00aa00389b71");
    private static readonly Guid Aac = new("00001610-0000-0010-8000-00aa00389b71");
    private static readonly Guid Pcm = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid Channels = new("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    private static readonly Guid SamplesPerSecond = new("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    private static readonly Guid BitsPerSample = new("f2deb57f-40fa-4764-aa33-ed4f2d1ff669");
    private static readonly Guid AvgBytesPerSecond = new("1aab75c8-cfef-451c-ab95-ac034b8e1731");
    private static readonly Guid BlockAlignment = new("322de230-9eeb-43bd-ab7a-ff412251541d");
    private static readonly Guid ContainerType = new("150ff23f-4abc-478b-ac4f-e1916fba1cca");
    private static readonly Guid Mpeg4 = new("dc6cd05d-b9d0-40ef-bd35-fa622c1ab28a");

    private readonly int _sampleRate;
    private readonly bool _com;
    private bool _started, _writing;
    private nint _writer;
    private long _frames;

    public M4aWriter(string path, int sampleRate)
    {
        _sampleRate = sampleRate;
        // S_FALSE when COM is already up on this thread; a thread in another mode still works with the sink writer.
        _com = CoInitializeEx(0, Multithreaded) >= 0;
        nint attributes = 0, output = 0, input = 0;
        try
        {
            Check(MFStartup(MfVersion, 0), "start Media Foundation");
            _started = true;
            Check(MFCreateAttributes(out attributes, 1), "make attributes");
            SetGuid(attributes, ContainerType, Mpeg4);
            Check(MFCreateSinkWriterFromURL(Path.GetFullPath(path), 0, attributes, out _writer), "create the file");

            Check(MFCreateMediaType(out output), "make a media type");
            SetGuid(output, MajorType, Audio);
            SetGuid(output, Subtype, Aac);
            SetUInt32(output, BitsPerSample, 16);
            SetUInt32(output, SamplesPerSecond, (uint)sampleRate);
            SetUInt32(output, Channels, 2);
            SetUInt32(output, AvgBytesPerSecond, AacBytesPerSecond);
            uint stream;
            Check(Vtbl<nint, nint>(_writer, 3)(_writer, output, (nint)(&stream)), "add the stream");

            Check(MFCreateMediaType(out input), "make a media type");
            SetGuid(input, MajorType, Audio);
            SetGuid(input, Subtype, Pcm);
            SetUInt32(input, BitsPerSample, 16);
            SetUInt32(input, SamplesPerSecond, (uint)sampleRate);
            SetUInt32(input, Channels, 2);
            SetUInt32(input, BlockAlignment, 4);
            SetUInt32(input, AvgBytesPerSecond, (uint)sampleRate * 4);
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, nint, int>)Slot(_writer, 4))(_writer, 0, input, 0), "set the input format");
            Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(_writer, 5))(_writer), "begin writing");
            _writing = true;
        }
        catch
        {
            Dispose();
            throw;
        }
        finally
        {
            Release(attributes);
            Release(output);
            Release(input);
        }
    }

    public void Write(ReadOnlySpan<float> interleaved)
    {
        var bytes = (uint)(interleaved.Length * sizeof(short));
        var frames = interleaved.Length / 2;
        nint buffer = 0, sample = 0;
        try
        {
            Check(MFCreateMemoryBuffer(bytes, out buffer), "make a buffer");
            byte* data;
            Check(((delegate* unmanaged[Stdcall]<nint, byte**, uint*, uint*, int>)Slot(buffer, 3))(buffer, &data, null, null), "lock a buffer");
            var pcm = new Span<short>(data, interleaved.Length);
            for (var i = 0; i < pcm.Length; i++) pcm[i] = (short)Math.Clamp(MathF.Round(interleaved[i] * 32767f), -32768f, 32767f);
            Check(((delegate* unmanaged[Stdcall]<nint, int>)Slot(buffer, 4))(buffer), "unlock a buffer");
            Check(((delegate* unmanaged[Stdcall]<nint, uint, int>)Slot(buffer, 6))(buffer, bytes), "fill a buffer");

            Check(MFCreateSample(out sample), "make a sample");
            Check(Vtbl<nint>(sample, 42)(sample, buffer), "add a buffer");
            // Times are in units of 100 ns; the encoder needs both on every sample.
            Check(Vtbl<long>(sample, 36)(sample, _frames * 10_000_000 / _sampleRate), "time a sample");
            Check(Vtbl<long>(sample, 38)(sample, (long)frames * 10_000_000 / _sampleRate), "time a sample");
            Check(((delegate* unmanaged[Stdcall]<nint, uint, nint, int>)Slot(_writer, 6))(_writer, 0, sample), "write");
            _frames += frames;
        }
        finally
        {
            Release(sample);
            Release(buffer);
        }
    }

    /// <summary>Encodes what is left, closes the file and shuts Media Foundation down again.</summary>
    public void Dispose()
    {
        var status = 0;
        if (_writer != 0)
        {
            if (_writing) status = ((delegate* unmanaged[Stdcall]<nint, int>)Slot(_writer, 11))(_writer);
            Release(_writer);
            _writer = 0;
            _writing = false;
        }
        if (_started) MFShutdown();
        _started = false;
        if (_com) CoUninitialize();
        Check(status, "finish the file");
    }

    private static nint Slot(nint com, int slot) => (*(nint**)com)[slot];

    private static delegate* unmanaged[Stdcall]<nint, T, int> Vtbl<T>(nint com, int slot) where T : unmanaged =>
        (delegate* unmanaged[Stdcall]<nint, T, int>)Slot(com, slot);

    private static delegate* unmanaged[Stdcall]<nint, T, U, int> Vtbl<T, U>(nint com, int slot) where T : unmanaged where U : unmanaged =>
        (delegate* unmanaged[Stdcall]<nint, T, U, int>)Slot(com, slot);

    /// <summary>IMFAttributes::SetUINT32.</summary>
    private static void SetUInt32(nint attributes, Guid key, uint value) =>
        Check(Vtbl<nint, uint>(attributes, 21)(attributes, (nint)(&key), value), "describe the audio");

    /// <summary>IMFAttributes::SetGUID.</summary>
    private static void SetGuid(nint attributes, Guid key, Guid value) =>
        Check(Vtbl<nint, nint>(attributes, 24)(attributes, (nint)(&key), (nint)(&value)), "describe the audio");

    private static void Release(nint com)
    {
        if (com != 0) ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(com, 2))(com);
    }

    private static void Check(int hresult, string what)
    {
        if (hresult < 0) throw new InvalidOperationException($"Media Foundation could not {what} (HRESULT 0x{hresult:X8}).");
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint mode);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("mfplat.dll")]
    private static partial int MFStartup(uint version, uint flags);

    [LibraryImport("mfplat.dll")]
    private static partial int MFShutdown();

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateAttributes(out nint attributes, uint size);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateMediaType(out nint type);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateSample(out nint sample);

    [LibraryImport("mfplat.dll")]
    private static partial int MFCreateMemoryBuffer(uint maxLength, out nint buffer);

    [LibraryImport("mfreadwrite.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MFCreateSinkWriterFromURL(string url, nint byteStream, nint attributes, out nint writer);
}
