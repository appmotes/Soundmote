using System.Text;
using Soundmote.Core.Dsp;

namespace Soundmote.Core.Output;

/// <summary>Renders a source offline to a 16-bit stereo WAV file, with triangular dither.</summary>
public static class WavWriter
{
    private const int BlockFrames = 4096;

    public static void Write(string path, IAudioSource source, TimeSpan duration, IProgress<double>? progress = null)
    {
        using var stream = File.Create(path);
        Write(stream, source, duration, progress);
    }

    public static void Write(Stream stream, IAudioSource source, TimeSpan duration, IProgress<double>? progress = null)
    {
        var frames = (long)(duration.TotalSeconds * source.SampleRate);
        var dataBytes = frames * 4;
        if (dataBytes > uint.MaxValue - 36) throw new ArgumentOutOfRangeException(nameof(duration), "Too long for one WAV file.");

        using var w = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        w.Write("RIFF"u8);
        w.Write((uint)(36 + dataBytes));
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16u);
        w.Write((ushort)1);
        w.Write((ushort)2);
        w.Write((uint)source.SampleRate);
        w.Write((uint)(source.SampleRate * 4));
        w.Write((ushort)4);
        w.Write((ushort)16);
        w.Write("data"u8);
        w.Write((uint)dataBytes);

        var block = new float[BlockFrames * 2];
        var bytes = new byte[BlockFrames * 4];
        var rng = new Rng(0x5EED);
        for (long done = 0; done < frames;)
        {
            var n = (int)Math.Min(BlockFrames, frames - done);
            var samples = block.AsSpan(0, n * 2);
            source.Render(samples);
            for (var i = 0; i < samples.Length; i++)
            {
                var dither = (rng.NextFloat() - rng.NextFloat()) / 32768f;
                var v = (short)Math.Clamp(MathF.Round((samples[i] + dither) * 32767f), -32768f, 32767f);
                bytes[i * 2] = (byte)v;
                bytes[i * 2 + 1] = (byte)(v >> 8);
            }
            w.Write(bytes, 0, n * 4);
            done += n;
            progress?.Report((double)done / frames);
        }
    }
}
