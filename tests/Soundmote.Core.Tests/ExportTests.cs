using Soundmote.Audio;
using Soundmote.Core.Output;
using Soundmote.Core.Scenes;

namespace Soundmote.Core.Tests;

public class ExportTests
{
    private const int Rate = SceneExport.SampleRate;

    [Fact]
    public void A_journey_exports_whole_to_wav_with_sound_and_a_fade_at_each_end()
    {
        var scene = BuiltInScenes.FallAsleep() with { Length = TimeSpan.FromSeconds(8) };

        var (bytes, samples) = Wav(scene, scene.Length);

        Assert.Equal("RIFF"u8.ToArray(), bytes[..4]);
        Assert.Equal((uint)(bytes.Length - 8), BitConverter.ToUInt32(bytes, 4));
        Assert.Equal((ushort)2, BitConverter.ToUInt16(bytes, 22));
        Assert.Equal((uint)Rate, BitConverter.ToUInt32(bytes, 24));
        Assert.Equal((ushort)16, BitConverter.ToUInt16(bytes, 34));
        Assert.Equal((uint)(8 * Rate * 4), BitConverter.ToUInt32(bytes, 40));
        Assert.True(Rms(samples.AsSpan(Rate * 2, Rate * 2)) > 0.005, "the middle is not silent");
        Assert.True(Math.Abs(samples[0]) < 0.001 && Math.Abs(samples[^1]) < 0.001, "the ends fade");
    }

    [Fact]
    public void Peaks_stay_below_full_scale()
    {
        var scene = BuiltInScenes.Unwind() with { Length = TimeSpan.FromSeconds(20) };

        var (_, samples) = Wav(scene, scene.Length);

        Assert.True(samples.Max(Math.Abs) <= SceneExport.Ceiling + 0.001f);
    }

    [Fact]
    public void A_loop_exports_the_laps_asked_for()
    {
        var scene = BuiltInScenes.DeepWork() with { Length = TimeSpan.FromSeconds(2) };

        var (_, samples) = Wav(scene, scene.Length * 3);

        Assert.Equal(6 * Rate * 2, samples.Length);
    }

    [Fact]
    public void A_clock_scene_plays_the_stretch_of_the_day_asked_for()
    {
        var scene = BuiltInScenes.AllDay();

        var noon = Wav(scene, TimeSpan.FromSeconds(1), TimeSpan.FromHours(12)).Samples;
        var again = Wav(scene, TimeSpan.FromSeconds(1), TimeSpan.FromHours(12)).Samples;
        var night = Wav(scene, TimeSpan.FromSeconds(1), TimeSpan.FromHours(2)).Samples;

        Assert.Equal(noon, again);
        Assert.NotEqual(noon, night);
    }

    [Fact]
    public void Cancelling_stops_the_render()
    {
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        using var file = new WavWriter(new MemoryStream(), Rate);

        Assert.Throws<OperationCanceledException>(() =>
            SceneExport.Render(BuiltInScenes.Sit(), TimeSpan.FromMinutes(20), file, cancel: cancel.Token));
    }

    [Fact]
    public void M4a_is_written_where_the_system_has_an_aac_encoder()
    {
        var files = new AudioFiles();
        Assert.SkipUnless(files.Formats.Contains(AudioFormat.M4a), "This system has no AAC encoder.");
        var path = Path.Combine(Path.GetTempPath(), $"soundmote-{Guid.NewGuid():N}.m4a");
        try
        {
            var scene = BuiltInScenes.FallAsleep() with { Length = TimeSpan.FromSeconds(4) };
            using (var file = files.Create(AudioFormat.M4a, path, Rate))
                SceneExport.Render(scene, scene.Length, file, cancel: TestContext.Current.CancellationToken);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal("ftyp"u8.ToArray(), bytes[4..8]);
            // 192 kbps is 24 kB a second.
            Assert.InRange(bytes.Length, 4 * 24_000 / 2, 4 * 24_000 * 2);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static (byte[] Bytes, float[] Samples) Wav(Scene scene, TimeSpan length, TimeSpan startOfDay = default)
    {
        using var stream = new MemoryStream();
        using (var file = new WavWriter(stream, Rate))
            SceneExport.Render(scene, length, file, startOfDay);
        var bytes = stream.ToArray();
        var samples = new float[(bytes.Length - 44) / 2];
        for (var i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(bytes, 44 + i * 2) / 32768f;
        return (bytes, samples);
    }

    private static double Rms(ReadOnlySpan<float> samples)
    {
        var sum = 0.0;
        foreach (var s in samples) sum += s * s;
        return Math.Sqrt(sum / samples.Length);
    }
}
