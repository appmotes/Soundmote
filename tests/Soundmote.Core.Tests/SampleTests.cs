using System.Text;
using Soundmote.Core.Output;
using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;
using Soundmote.Core.Sounds;

namespace Soundmote.Core.Tests;

/// <summary>S12: sounds played from audio files.</summary>
public class SampleTests
{
    private const int Rate = 48000;

    [Theory]
    [InlineData(1, 16, 1)]
    [InlineData(1, 24, 2)]
    [InlineData(3, 32, 2)]
    public void A_wave_file_reads_in_each_format(int format, int bits, int channels)
    {
        float[] values = [0f, 0.5f, -0.5f, 0.25f];
        var bytes = Wave(format, bits, channels, values);

        var sample = Sample.Read(bytes);

        Assert.Equal(values.Length, sample.Length);
        Assert.Equal(44100, sample.Rate);
        for (var i = 0; i < values.Length; i++)
        {
            Assert.Equal(values[i], sample.Left[i], 3);
            Assert.Equal(channels == 2 ? -values[i] : values[i], sample.Right[i], 3);
        }
    }

    [Fact]
    public void Anything_else_is_refused_with_a_reason()
    {
        Assert.Throws<InvalidDataException>(() => Sample.Read("not a wave file at all, not even close to one"u8.ToArray()));
        Assert.Contains("format 1 at 8 bits", Assert.Throws<InvalidDataException>(() => Sample.Read(Wave(1, 8, 1, [0f]))).Message);
    }

    /// <summary>A looped file joins without a step: the frame after its end is close to its first.</summary>
    [Fact]
    public void A_loop_has_no_seam()
    {
        var length = Rate * 4;
        var left = new float[length];
        for (var i = 0; i < length; i++) left[i] = MathF.Sin(i * 0.013f) * (1 + i / (float)length);

        var loop = new Sample(left, left.ToArray(), Rate).Looped(0.5);

        Assert.Equal(length - Rate / 2, loop.Length);
        Assert.True(MathF.Abs(loop.Left[^1] - loop.Left[0]) < 0.05f, $"{loop.Left[^1]} then {loop.Left[0]}");
    }

    [Fact]
    public void Every_file_a_recipe_names_is_there()
    {
        var files = RecipeLibrary.All.SelectMany(r => r.Parts).OfType<SamplePart>().SelectMany(p => p.Files).Distinct().ToArray();

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.True(
            File.Exists(Path.Combine(Sample.Folder, f + ".ogg")) || File.Exists(Path.Combine(Sample.Folder, f + ".wav")), $"{f} is missing"));
    }

    /// <summary>S18: a pattern's hits land on the scene's beat, so a quicker tempo brings the next hit sooner.</summary>
    [Theory]
    [InlineData(60, 0.5)]
    [InlineData(120, 0.25)]
    public void A_pattern_hits_on_the_beat_at_any_tempo(double tempo, double seconds)
    {
        var sound = RecipeSound.Create(RecipeLibrary.Get("rhythm-pulse"), Rate, 1);
        sound.Tempo = tempo;
        sound.Beat = 1.5;

        var heard = Play(sound, Rate);
        var hit = (int)(seconds * Rate);

        Assert.True(Rms(heard.AsSpan(Rate / 50, hit - Rate / 50 - Rate / 500)) < -80, "silent until the next beat");
        Assert.True(Rms(heard.AsSpan(hit, Rate / 10)) > -40, "heard on it");
    }

    /// <summary>A16: a rhythm added midway joins the scene's beat; its first hit waits for the next beat.</summary>
    [Fact]
    public void A_rhythm_added_midway_keeps_the_scenes_beat()
    {
        var scene = new Scene("Beat", TimeKind.Journey, TimeSpan.FromMinutes(10), []) { RoomSeconds = 0.5f, Tempo = 60 };
        var renderer = new SceneRenderer(scene, Rate);
        Render(renderer, seconds: 0.6);
        renderer.AddLayer(new Layer("Heart", RecipeLibrary.Get("rhythm-heart"), Curve.Steady(1f)));

        var waiting = Render(renderer, seconds: 0.35);
        var beat = Render(renderer, seconds: 0.2);

        Assert.True(Rms(waiting) < -70, $"no hit before the beat: {Rms(waiting):0.0} dBFS");
        Assert.True(Rms(beat) > -45, $"a hit on it: {Rms(beat):0.0} dBFS");
    }

    private static float[] Play(Sound sound, int frames)
    {
        var left = new float[frames];
        var right = new float[frames];
        for (var at = 0; at < frames; at += 512)
        {
            var n = Math.Min(512, frames - at);
            sound.Render(left.AsSpan(at, n), right.AsSpan(at, n), 1f);
        }
        return left;
    }

    [Theory]
    [InlineData("""{ "type": "sample", "files": [] }""")]
    [InlineData("""{ "type": "sample", "files": ["../secret"] }""")]
    [InlineData("""{ "type": "sample", "files": ["bowl mid"] }""")]
    public void A_sample_part_names_only_files_in_the_samples_folder(string part)
    {
        var json = $$"""{ "id": "x", "name": "X", "family": "bells", "colour": "#FFFFFF", "parts": [ {{part}} ] }""";

        Assert.Contains("'files'", Assert.Throws<RecipeException>(() => Recipe.Parse(json)).Message);
    }

    /// <summary>A bowl on a ring of strikes rings out by itself, and is silent until struck.</summary>
    [Fact]
    public void A_struck_sound_sounds_at_its_strikes_and_rings_on()
    {
        var scene = new Scene("Bowl", TimeKind.Journey, TimeSpan.FromMinutes(10),
            [new Layer("Bowl", RecipeLibrary.Get("bowl"), Curve.Steady(1f)) { Play = new EveryPlay(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30)) }])
        {
            RoomSeconds = 0.5f
        };
        var renderer = new SceneRenderer(scene, Rate);

        var before = Render(renderer, seconds: 29);
        var after = Render(renderer, seconds: 12);

        Assert.True(Rms(before.AsSpan(before.Length - Rate * 2)) < -80);
        Assert.True(Rms(after.AsSpan(Rate * 2, Rate * 2)) > -40, "heard after the strike");
        Assert.True(Rms(after.AsSpan(after.Length - Rate * 2)) > -55, "still ringing ten seconds on");
        Assert.True(RecipeLibrary.Get("bowl").TakesStrikes);
        Assert.False(RecipeLibrary.Get("bowl-sung").TakesStrikes);
    }

    public static TheoryData<string> Struck => new(RecipeLibrary.All.Where(r => r.TakesStrikes).Select(r => r.Id));

    /// <summary>A sound of strikes added midway and struck at once is heard at once, not at its first strike minutes away.</summary>
    [Theory]
    [MemberData(nameof(Struck))]
    public void A_struck_sound_struck_now_is_heard_now(string id)
    {
        var scene = new Scene("Quiet", TimeKind.Journey, TimeSpan.FromMinutes(45), []) { RoomSeconds = 0.5f };
        var renderer = new SceneRenderer(scene, Rate);
        renderer.Seek(TimeSpan.FromMinutes(18));
        Render(renderer, seconds: 1);
        var layer = renderer.AddLayer(new Layer(id, RecipeLibrary.Get(id), Curve.Steady(0.6f))
        {
            Play = new EveryPlay(TimeSpan.FromMinutes(5), TimeSpan.Zero)
        });

        renderer.StrikeNow(layer);
        var heard = Render(renderer, seconds: 3);

        Assert.True(Rms(heard) > -45, $"{id} at {Rms(heard):0.0} dBFS");
    }

    [Fact]
    public void Playing_samples_does_not_allocate()
    {
        var scene = BuiltInScenes.Sit() with
        {
            Layers = [.. BuiltInScenes.Sit().Layers, new Layer("Sung", RecipeLibrary.Get("bowl-sung"), Curve.Steady(0.6f))]
        };
        var renderer = new SceneRenderer(scene, Rate) { Speed = 120 };
        var block = new float[1024 * 2];
        renderer.Render(block);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 2000; i++) renderer.Render(block);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static byte[] Wave(int format, int bits, int channels, float[] values)
    {
        using var data = new MemoryStream();
        foreach (var v in values)
            for (var c = 0; c < channels; c++)
            {
                var x = c == 1 ? -v : v;
                switch (format, bits)
                {
                    case (3, _): data.Write(BitConverter.GetBytes(x)); break;
                    case (_, 16): data.Write(BitConverter.GetBytes((short)(x * 32767))); break;
                    case (_, 24):
                        var i = (int)(x * 8388607);
                        data.Write([(byte)i, (byte)(i >> 8), (byte)(i >> 16)]);
                        break;
                    default: data.WriteByte((byte)(x * 127 + 128)); break;
                }
            }
        using var file = new MemoryStream();
        using var w = new BinaryWriter(file, Encoding.ASCII);
        w.Write("RIFF"u8);
        w.Write((int)(36 + data.Length));
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)format);
        w.Write((short)channels);
        w.Write(44100);
        w.Write(44100 * channels * bits / 8);
        w.Write((short)(channels * bits / 8));
        w.Write((short)bits);
        w.Write("data"u8);
        w.Write((int)data.Length);
        w.Write(data.ToArray());
        return file.ToArray();
    }

    private static float[] Render(IAudioSource source, double seconds)
    {
        var samples = new float[(int)(seconds * Rate) * 2];
        source.Render(samples);
        return samples;
    }

    private static double Rms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        foreach (var s in samples) sum += s * s;
        var rms = Math.Sqrt(sum / samples.Length);
        return rms <= 1e-9 ? -180 : 20 * Math.Log10(rms);
    }
}
