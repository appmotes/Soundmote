using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;

namespace Soundmote.Sandbox;

/// <summary>Measures each recipe and the demo mix, so loudness can be tuned without guessing (S4).</summary>
internal static class Levels
{
    private const int Rate = 48000;

    public static void Print()
    {
        Console.WriteLine("Each recipe alone at its trim, 10 s after 2 s to settle. dBFS; 'zc' is zero crossings per second (brightness).");
        Console.WriteLine("A sound that takes strikes is struck once at the given strength; its loudest 4 s within 20 s count (S12).");
        Console.WriteLine("'suggested' is the trim that brings it to the target at full amount, averaged over five seeds of 30 s.");
        Console.WriteLine($"{"recipe",-12}{"0.25",8}{"0.5",8}{"1.0",8}{"peak",8}{"zc",8}{"trim",8}{"suggested",11}");
        foreach (var recipe in RecipeLibrary.All)
        {
            var row = new[] { 0.25f, 0.5f, 1f }.Select(a => Measure(RecipeSound.Create(recipe, Rate, 7), a, 10)).ToArray();
            var raw = recipe with { Trim = 1f };
            var power = Enumerable.Range(1, 5).Average(seed => Math.Pow(10, Measure(RecipeSound.Create(raw, Rate, (ulong)seed), 1f, 30).Rms / 10));
            var suggested = Math.Pow(10, (RecipeSound.TargetDb - 10 * Math.Log10(power)) / 20);
            Console.WriteLine($"{recipe.Id,-12}{row[0].Rms,8:0.0}{row[1].Rms,8:0.0}{row[2].Rms,8:0.0}{row[2].Peak,8:0.0}{row[2].Zc,8:0}" +
                              $"{recipe.Trim?.ToString("0.000") ?? "auto",8}{suggested,11:0.000}");
        }

        Console.WriteLine();
        Console.WriteLine("Demo mix with the hand held still, reverb and limiter included.");
        Console.WriteLine($"{"at",-8}{"rms",8}{"peak",8}{"zc",8}");
        foreach (var minute in new[] { 2, 12, 22, 32, 42 })
        {
            var renderer = new SceneRenderer(BuiltInScenes.FallAsleep(7), Rate) { Speed = 0 };
            renderer.Seek(TimeSpan.FromMinutes(minute));
            var interleaved = new float[512 * 2];
            var mix = new MixSound(renderer, interleaved);
            var (rms, peak, zc) = Measure(mix, 1f, 10);
            Console.WriteLine($"{minute + " min",-8}{rms,8:0.0}{peak,8:0.0}{zc,8:0}");
        }
    }

    private static (double Rms, double Peak, double Zc) Measure(Core.Sounds.Sound sound, float amount, int seconds)
    {
        var left = new float[512];
        var right = new float[512];
        var blocks = new List<double>();
        double peak = 0;
        long count = 0, crossings = 0;
        var last = 0f;
        var struck = sound.TakesStrikes;
        if (struck) sound.Strike(amount);
        var settle = struck ? 0 : 2;
        if (struck) seconds = RecipeSound.StrikeSearchSeconds;
        for (var b = 0; b < (seconds + settle) * Rate / 512; b++)
        {
            sound.Render(left, right, struck ? 1f : amount);
            if (b < settle * Rate / 512) continue;
            double sum = 0;
            for (var i = 0; i < left.Length; i++)
            {
                sum += left[i] * left[i] + right[i] * right[i];
                peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i])));
                if ((left[i] >= 0) != (last >= 0)) crossings++;
                last = left[i];
            }
            blocks.Add(sum / (2 * left.Length));
            count += left.Length;
        }
        var power = struck ? RecipeSound.LoudestPower(blocks, RecipeSound.StrikeSeconds * Rate / 512) : blocks.Average();
        return (Db(Math.Sqrt(power)), Db(peak), crossings / ((double)count / Rate));
    }

    private static double Db(double x) => x <= 1e-9 ? -180 : 20 * Math.Log10(x);

    /// <summary>The whole renderer seen as one sound, so the mix is measured the same way.</summary>
    private sealed class MixSound(SceneRenderer renderer, float[] interleaved) : Core.Sounds.Sound(Rate)
    {
        public override void Render(Span<float> left, Span<float> right, float amount)
        {
            renderer.Render(interleaved);
            for (var i = 0; i < left.Length; i++)
            {
                left[i] = interleaved[i * 2];
                right[i] = interleaved[i * 2 + 1];
            }
        }
    }
}
