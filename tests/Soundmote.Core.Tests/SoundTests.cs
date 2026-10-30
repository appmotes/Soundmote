using Soundmote.Core.Dsp;
using Soundmote.Core.Recipes;
using Soundmote.Core.Sounds;

namespace Soundmote.Core.Tests;

public class SoundTests
{
    private const int Rate = 48000;

    public static TheoryData<string> Recipes => new(RecipeLibrary.All.Select(r => r.Id));

    private static Sound Create(string id) => RecipeSound.Create(RecipeLibrary.Get(id), Rate, 3);

    [Theory]
    [MemberData(nameof(Recipes))]
    public void Amount_zero_is_silence(string id)
    {
        var (rms, _) = Measure(Create(id), 0f, seconds: 3);

        Assert.True(rms < -90, $"{id} at 0: {rms:0.0} dBFS");
    }

    /// <summary>Guards the balance measured with the sandbox's 'levels' command (sound-design.md S4).</summary>
    [Theory]
    [MemberData(nameof(Recipes))]
    public void Full_amount_lands_in_the_shared_loudness_window(string id)
    {
        var (rms, peak) = Measure(Create(id), 1f, seconds: 8);

        Assert.InRange(rms, -27, -19);
        Assert.True(peak < -1, $"{id} peaks at {peak:0.0} dBFS");
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void More_amount_is_louder(string id)
    {
        var (quiet, _) = Measure(Create(id), 0.25f, seconds: 6);
        var (loud, _) = Measure(Create(id), 0.75f, seconds: 6);

        Assert.True(loud > quiet + 6, $"{id}: {quiet:0.0} → {loud:0.0} dBFS");
    }

    [Fact]
    public void Reverb_tail_dies_away()
    {
        var reverb = new Reverb(Rate, seconds: 2f);
        var input = new float[Rate];
        input[0] = 1f;
        var silence = new float[Rate];
        var left = new float[Rate];
        var right = new float[Rate];

        reverb.Process(input, input, left, right);
        var early = left.Max(MathF.Abs);
        for (var s = 0; s < 4; s++) reverb.Process(silence, silence, left, right);
        var late = left.Concat(right).Max(MathF.Abs);

        Assert.True(early > 0.01f);
        Assert.True(late < early * 1e-3f, $"after 5 s: {late}");
    }

    /// <summary>
    /// RMS and peak in dBFS after a second to settle. A sound that takes strikes is struck once at
    /// <paramref name="amount"/>, its RMS taken over its loudest 4 s and its peak over all it plays, as the levels command
    /// does (S12).
    /// </summary>
    private static (double Rms, double Peak) Measure(Sound sound, float amount, int seconds)
    {
        var left = new float[512];
        var right = new float[512];
        var blocks = new List<double>();
        double peak = 0;
        var settle = Rate / 512;
        var struck = sound.TakesStrikes;
        if (struck)
        {
            sound.Strike(amount);
            amount = 1f;
            seconds = RecipeSound.StrikeSearchSeconds;
            settle = 0;
        }
        for (var b = 0; b < seconds * Rate / 512; b++)
        {
            sound.Render(left, right, amount);
            if (b < settle) continue;
            double sum = 0;
            for (var i = 0; i < left.Length; i++)
            {
                Assert.True(float.IsFinite(left[i]) && float.IsFinite(right[i]));
                sum += left[i] * left[i] + right[i] * right[i];
                peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i])));
            }
            blocks.Add(sum / (2 * left.Length));
        }
        var power = struck ? RecipeSound.LoudestPower(blocks, RecipeSound.StrikeSeconds * Rate / 512) : blocks.Average();
        return (Db(Math.Sqrt(power)), Db(peak));
    }

    private static double Db(double x) => x <= 1e-9 ? -180 : 20 * Math.Log10(x);
}
