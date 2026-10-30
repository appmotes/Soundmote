using System.Collections.Concurrent;
using System.Text;
using NVorbis;

namespace Soundmote.Core.Sounds;

/// <summary>
/// An audio file held in memory as two channels of floats (S12). Read once, on the caller's thread, and shared by
/// every sound that plays it; the audio thread only reads it.
/// </summary>
public sealed class Sample(float[] left, float[] right, int rate)
{
    public float[] Left { get; } = left;

    public float[] Right { get; } = right;

    public int Rate { get; } = rate;

    public int Length => Left.Length;

    /// <summary>Where the files are: a <c>Samples</c> folder beside the app.</summary>
    public static string Folder { get; set; } = Path.Combine(AppContext.BaseDirectory, "Samples");

    private static readonly ConcurrentDictionary<(string Id, double Fade), Sample> Loaded = new();

    /// <summary>
    /// The file <c>id.ogg</c> (S12) or else <c>id.wav</c> from <see cref="Folder"/>, read once
    /// and kept. With <paramref name="loopFade"/> its end is folded over its start, so it loops without a seam.
    /// </summary>
    /// <exception cref="FileNotFoundException">There is no such file.</exception>
    public static Sample Get(string id, double loopFade = 0) =>
        Loaded.GetOrAdd((id, loopFade), key =>
        {
            var ogg = Path.Combine(Folder, key.Id + ".ogg");
            var sample = File.Exists(ogg)
                ? ReadOgg(File.OpenRead(ogg))
                : Read(File.ReadAllBytes(Path.Combine(Folder, key.Id + ".wav")));
            return key.Fade > 0 ? sample.Looped(key.Fade) : sample;
        });

    /// <summary>
    /// A copy that loops seamlessly: the last <paramref name="seconds"/> fade out over the first ones as they fade in,
    /// with equal power, and are then cut off. Shorter by that much.
    /// </summary>
    public Sample Looped(double seconds)
    {
        var fade = Math.Min((int)(seconds * Rate), Length / 3);
        var length = Length - fade;
        var left = Left[..length];
        var right = Right[..length];
        for (var i = 0; i < fade; i++)
        {
            var t = (i + 0.5) / fade;
            var inGain = (float)Math.Sin(t * Math.PI / 2);
            var outGain = (float)Math.Cos(t * Math.PI / 2);
            left[i] = Left[i] * inGain + Left[length + i] * outGain;
            right[i] = Right[i] * inGain + Right[length + i] * outGain;
        }
        return new Sample(left, right, Rate);
    }

    /// <summary>A RIFF WAVE file: 16- or 24-bit PCM or 32-bit float, mono or stereo. Mono plays in both ears.</summary>
    /// <exception cref="InvalidDataException">The bytes are not such a file.</exception>
    public static Sample Read(byte[] bytes)
    {
        if (bytes.Length < 44 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
            throw new InvalidDataException("Not a WAVE file.");
        int format = 0, channels = 0, rate = 0, bits = 0;
        for (var at = 12; at + 8 <= bytes.Length;)
        {
            var id = Encoding.ASCII.GetString(bytes, at, 4);
            var size = BitConverter.ToInt32(bytes, at + 4);
            var body = at + 8;
            if (id == "fmt ")
            {
                format = BitConverter.ToUInt16(bytes, body);
                channels = BitConverter.ToUInt16(bytes, body + 2);
                rate = BitConverter.ToInt32(bytes, body + 4);
                bits = BitConverter.ToUInt16(bytes, body + 14);
                // WAVE_FORMAT_EXTENSIBLE keeps the real format in its sub-format GUID.
                if (format == 0xFFFE && size >= 26) format = BitConverter.ToUInt16(bytes, body + 24);
            }
            else if (id == "data")
            {
                if (channels is < 1 or > 2 || rate <= 0) throw new InvalidDataException("A WAVE file needs one or two channels.");
                return Decode(bytes.AsSpan(body, Math.Min(size, bytes.Length - body)), format, channels, rate, bits);
            }
            at = body + size + (size & 1);
        }
        throw new InvalidDataException("A WAVE file needs a data chunk.");
    }

    /// <summary>An Ogg Vorbis stream, mono or stereo, decoded whole. Mono plays in both ears. Closes the stream.</summary>
    /// <exception cref="InvalidDataException">The stream is not such a file.</exception>
    public static Sample ReadOgg(Stream stream)
    {
        using var reader = new VorbisReader(stream, closeOnDispose: true) { ClipSamples = false };
        var channels = reader.Channels;
        if (channels is < 1 or > 2) throw new InvalidDataException("An Ogg file needs one or two channels.");
        var left = new float[reader.TotalSamples];
        var right = new float[left.Length];
        var buffer = new float[4096 * channels];
        var at = 0;
        for (int read; at < left.Length && (read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0;)
        {
            for (var i = 0; i < read / channels && at < left.Length; i++, at++)
            {
                left[at] = buffer[i * channels];
                right[at] = buffer[i * channels + channels - 1];
            }
        }
        return new Sample(left[..at], right[..at], reader.SampleRate);
    }

    private static Sample Decode(ReadOnlySpan<byte> data, int format, int channels, int rate, int bits)
    {
        var width = bits / 8;
        if (!((format == 1 && bits is 16 or 24) || (format == 3 && bits == 32)))
            throw new InvalidDataException($"Only 16- or 24-bit PCM or 32-bit float, not format {format} at {bits} bits.");
        var frames = data.Length / (width * channels);
        var left = new float[frames];
        var right = new float[frames];
        for (var i = 0; i < frames; i++)
        {
            var frame = data.Slice(i * width * channels);
            left[i] = Value(frame, format, bits);
            right[i] = channels == 2 ? Value(frame[width..], format, bits) : left[i];
        }
        return new Sample(left, right, rate);
    }

    private static float Value(ReadOnlySpan<byte> b, int format, int bits) => (format, bits) switch
    {
        (3, _) => BitConverter.ToSingle(b),
        (_, 16) => BitConverter.ToInt16(b) / 32768f,
        _ => ((b[0] << 8 | b[1] << 16 | b[2] << 24) >> 8) / 8388608f
    };
}
