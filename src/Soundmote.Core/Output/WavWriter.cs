using System.Text;
using Soundmote.Core.Dsp;

namespace Soundmote.Core.Output;

/// <summary>
/// Writes a 16-bit stereo WAV file with triangular dither, block by block. The header's sizes are filled in when it is
/// disposed, so the stream must be seekable.
/// </summary>
public sealed class WavWriter : IAudioFileWriter
{
    private const int BlockFrames = 4096;

    /// <summary>The most audio one WAV file can hold: its sizes are 32-bit.</summary>
    public const long MaxDataBytes = uint.MaxValue - 36;

    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly long _start;
    private byte[] _bytes = [];
    private long _dataBytes;
    private Rng _rng = new(0x5EED);

    public WavWriter(string path, int sampleRate) : this(File.Create(path), sampleRate, ownsStream: true)
    {
    }

    public WavWriter(Stream stream, int sampleRate, bool ownsStream = false)
    {
        if (!stream.CanSeek) throw new ArgumentException("A WAV file needs a seekable stream.", nameof(stream));
        _stream = stream;
        _ownsStream = ownsStream;
        _start = stream.Position;
        using var w = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        w.Write("RIFF"u8);
        w.Write(36u);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16u);
        w.Write((ushort)1);
        w.Write((ushort)2);
        w.Write((uint)sampleRate);
        w.Write((uint)(sampleRate * 4));
        w.Write((ushort)4);
        w.Write((ushort)16);
        w.Write("data"u8);
        w.Write(0u);
    }

    public void Write(ReadOnlySpan<float> interleaved)
    {
        var count = interleaved.Length * 2;
        if (_dataBytes + count > MaxDataBytes) throw new InvalidOperationException("Too long for one WAV file.");
        if (_bytes.Length < count) _bytes = new byte[count];
        for (var i = 0; i < interleaved.Length; i++)
        {
            var dither = (_rng.NextFloat() - _rng.NextFloat()) / 32768f;
            var v = (short)Math.Clamp(MathF.Round((interleaved[i] + dither) * 32767f), -32768f, 32767f);
            _bytes[i * 2] = (byte)v;
            _bytes[i * 2 + 1] = (byte)(v >> 8);
        }
        _stream.Write(_bytes, 0, count);
        _dataBytes += count;
    }

    /// <summary>Writes the sizes into the header and leaves the stream at the end of the file.</summary>
    public void Dispose()
    {
        var end = _stream.Position;
        Span<byte> size = stackalloc byte[4];
        _stream.Position = _start + 4;
        BitConverter.TryWriteBytes(size, (uint)(36 + _dataBytes));
        _stream.Write(size);
        _stream.Position = _start + 40;
        BitConverter.TryWriteBytes(size, (uint)_dataBytes);
        _stream.Write(size);
        _stream.Position = end;
        _stream.Flush();
        if (_ownsStream) _stream.Dispose();
    }

    /// <summary>Renders a source offline into a WAV file.</summary>
    public static void Write(string path, IAudioSource source, TimeSpan duration, IProgress<double>? progress = null)
    {
        using var stream = File.Create(path);
        Write(stream, source, duration, progress);
    }

    public static void Write(Stream stream, IAudioSource source, TimeSpan duration, IProgress<double>? progress = null)
    {
        var frames = (long)(duration.TotalSeconds * source.SampleRate);
        if (frames * 4 > MaxDataBytes) throw new ArgumentOutOfRangeException(nameof(duration), "Too long for one WAV file.");

        using var file = new WavWriter(stream, source.SampleRate);
        var block = new float[BlockFrames * 2];
        for (long done = 0; done < frames;)
        {
            var n = (int)Math.Min(BlockFrames, frames - done);
            var samples = block.AsSpan(0, n * 2);
            source.Render(samples);
            file.Write(samples);
            done += n;
            progress?.Report((double)done / frames);
        }
    }
}
