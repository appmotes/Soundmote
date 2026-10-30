using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Soundmote.Core.Output;

namespace Soundmote.Audio.MacOS;

/// <summary>
/// Live output through Core Audio's AudioQueue (A7). Three buffers of 1024 frames keep latency near 64 ms at
/// 48 kHz. The queue converts to the device's own rate. Buffers are filled on the queue's own thread.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed unsafe partial class AudioQueueOutput : IAudioOutput
{
    private const string AudioToolbox = "/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox";
    private const int BufferCount = 3;
    private const int FramesPerBuffer = 1024;
    private const uint LinearPcm = 0x6C70636D; // 'lpcm'
    private const uint FloatPacked = 1 | 8;     // kAudioFormatFlagIsFloat | kAudioFormatFlagIsPacked
    private const uint VolumeParam = 1;         // kAudioQueueParam_Volume

    private nint _queue;
    private GCHandle _self;
    private IAudioSource? _source;
    private float _volume = 1f;

    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_queue != 0) AudioQueueSetParameter(_queue, VolumeParam, _volume);
        }
    }

    public void Start(IAudioSource source)
    {
        if (_queue != 0) throw new InvalidOperationException("Already playing.");
        _source = source;
        _self = GCHandle.Alloc(this);

        var format = new AudioStreamBasicDescription
        {
            SampleRate = source.SampleRate,
            FormatId = LinearPcm,
            FormatFlags = FloatPacked,
            BytesPerPacket = 8,
            FramesPerPacket = 1,
            BytesPerFrame = 8,
            ChannelsPerFrame = 2,
            BitsPerChannel = 32
        };
        Check(AudioQueueNewOutput(&format, &OnBufferDone, GCHandle.ToIntPtr(_self), 0, 0, 0, out _queue), "create the queue");
        AudioQueueSetParameter(_queue, VolumeParam, _volume);

        for (var i = 0; i < BufferCount; i++)
        {
            Check(AudioQueueAllocateBuffer(_queue, FramesPerBuffer * 8, out var buffer), "allocate a buffer");
            Fill((AudioQueueBuffer*)buffer);
            Check(AudioQueueEnqueueBuffer(_queue, buffer, 0, 0), "queue a buffer");
        }
        Check(AudioQueueStart(_queue, 0), "start");
    }

    public void Stop()
    {
        if (_queue == 0) return;
        AudioQueueStop(_queue, 1);
        AudioQueueDispose(_queue, 1);
        _queue = 0;
        _self.Free();
        _source = null;
    }

    public void Dispose() => Stop();

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnBufferDone(nint userData, nint queue, nint buffer)
    {
        if (GCHandle.FromIntPtr(userData).Target is not AudioQueueOutput self || self._queue == 0) return;
        self.Fill((AudioQueueBuffer*)buffer);
        AudioQueueEnqueueBuffer(queue, buffer, 0, 0);
    }

    /// <summary>Never lets an exception reach the native thread: a failing source plays silence.</summary>
    private void Fill(AudioQueueBuffer* buffer)
    {
        var samples = new Span<float>((void*)buffer->AudioData, (int)(buffer->AudioDataBytesCapacity / sizeof(float)));
        try
        {
            if (_source is { } source) source.Render(samples);
            else samples.Clear();
        }
        catch
        {
            samples.Clear();
        }
        buffer->AudioDataByteSize = buffer->AudioDataBytesCapacity;
    }

    private static void Check(int status, string what)
    {
        if (status != 0) throw new InvalidOperationException($"Core Audio could not {what} (OSStatus {status}).");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioStreamBasicDescription
    {
        public double SampleRate;
        public uint FormatId, FormatFlags, BytesPerPacket, FramesPerPacket, BytesPerFrame, ChannelsPerFrame, BitsPerChannel, Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioQueueBuffer
    {
        public uint AudioDataBytesCapacity;
        public nint AudioData;
        public uint AudioDataByteSize;
        public nint UserData;
        public uint PacketDescriptionCapacity;
        public nint PacketDescriptions;
        public uint PacketDescriptionCount;
    }

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueNewOutput(AudioStreamBasicDescription* format,
        delegate* unmanaged[Cdecl]<nint, nint, nint, void> callback, nint userData, nint runLoop, nint runLoopMode,
        uint flags, out nint queue);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueAllocateBuffer(nint queue, uint byteSize, out nint buffer);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueEnqueueBuffer(nint queue, nint buffer, uint packetDescCount, nint packetDescs);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueStart(nint queue, nint startTime);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueStop(nint queue, byte immediate);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueDispose(nint queue, byte immediate);

    [LibraryImport(AudioToolbox)]
    private static partial int AudioQueueSetParameter(nint queue, uint parameter, float value);
}
