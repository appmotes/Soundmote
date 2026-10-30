using Soundmote.Core.Recipes;

namespace Soundmote.Core.Tests;

public class RecipeTests
{
    private const int Rate = 48000;

    /// <summary>A sound none of the built-ins makes: crickets as chirps on a beat with a night hum. Written as data only.</summary>
    private const string Crickets = """
        {
          "id": "crickets", "name": "Crickets", "family": "night", "colour": "#8C6BB8",
          "character": "A field at night",
          "signals": [ { "name": "chorus", "walk": 6 } ],
          "parts": [
            { "type": "events", "source": "white", "bpm": 150, "jitter": 0.01, "burst": [3, 5], "spacing": 0.012,
              "level": [0.6, 1], "decay": 0.004,
              "filters": [ { "type": "bandpass", "freq": "4300 + 150 * ear", "q": 12 } ],
              "gain": "amount * (0.8 + 0.2 * chorus)" },
            { "type": "noise", "source": "pink", "filters": [ { "type": "lowpass", "freq": 400 } ], "gain": "0.05 * amount" }
          ]
        }
        """;

    [Fact]
    public void Every_built_in_recipe_reads_and_has_a_trim()
    {
        Assert.Equal(new[] {
            "rain", "rain-window", "rain-skylight", "rain-tent", "rain-jungle", "rain-heavy", "thunder-far",
            "thunder-roll", "thunder-close", "thunder-mountain", "thunder-rumble", "sea", "sea-sand", "sea-pebbles",
            "sea-lake", "sea-rocks", "sea-far", "sea-lapping", "stream", "stream-forest", "stream-river",
            "stream-trickle", "stream-rapids", "stream-fall", "wind", "wind-grass", "wind-howl", "wind-mountain",
            "wind-strong", "wind-whistle", "fire", "fire-stove", "fire-small", "fire-crackle", "fire-camp",
            "fire-heater", "birds-dawn", "birds-blackbird", "birds-gulls", "birds-woodpecker", "birds-pond",
            "birds-morning", "birds-nightsong", "forest-morning", "forest-deep", "forest-pines", "forest-breeze",
            "forest-alps", "crickets", "night-woods", "night-frogs", "night-cicadas", "night-treefrogs", "choir",
            "voices-temple", "voices-monks", "voices-tibetan", "voices-pilgrims", "voices-throat", "bowl", "bowl-small",
            "bowl-sung", "temple-bell", "gong", "chimes", "pad-warm", "pad-glass", "pad-drone", "pad-breath",
            "pad-voices", "pad-shimmer", "pad-strings", "pad-tide", "pad-organ", "pad-space", "rhythm-heart",
            "rhythm-pulse", "rhythm-downtempo", "rhythm-mystic", "rhythm-shaker", "rhythm-hands", "rhythm-kalimba",
            "rhythm-bass", "rhythm-ritual", "town-night", "town-cafe", "town-train", "town-street", "town-sbahn",
            "brown", "noise-washer", "noise-purr", "noise-fan", "noise-fan-ceiling", "noise-purifier", "noise-clock" }, RecipeLibrary.All.Select(r => r.Id));
        Assert.All(RecipeLibrary.All, r => Assert.NotNull(r.Trim));
    }

    [Fact]
    public void A_new_recipe_plays_without_code_and_is_trimmed_to_the_shared_loudness()
    {
        var recipe = Recipe.Parse(Crickets);

        var trim = RecipeSound.Measure(recipe);
        var sound = RecipeSound.Create(recipe, Rate, 1);
        var (rms, peak) = Measure(sound, 1f, seconds: 6);

        Assert.Null(recipe.Trim);
        Assert.InRange(trim, 0.01f, 50f);
        Assert.InRange(rms, RecipeSound.TargetDb - 4, RecipeSound.TargetDb + 4);
        Assert.True(peak <= 12f);
    }

    [Fact]
    public void A_burst_of_pings_is_one_tone_repeated_at_its_spacing()
    {
        var recipe = Recipe.Parse("""
            {"id":"x","name":"X","family":"noise","colour":"#112233","trim":1,
             "parts":[{"type":"pings","bpm":30,"burst":4,"spacing":0.05,"pitch":[3000,5000],"decay":0.003,"place":-1,"pan":0}]}
            """);
        var sound = RecipeSound.Create(recipe, Rate, 1);
        var left = new float[Rate];
        var right = new float[Rate];
        sound.Render(left, right, 1f);

        // The first beat falls a quarter beat in (0.5 s at 30 bpm); count where 5 ms windows rise out of silence.
        var onsets = new List<int>();
        var wasQuiet = true;
        for (var w = 0; w < Rate / 240; w++)
        {
            var peak = 0f;
            for (var i = w * 240; i < (w + 1) * 240; i++) peak = MathF.Max(peak, MathF.Abs(left[i]));
            if (peak > 0.05f && wasQuiet) onsets.Add(w * 240);
            wasQuiet = peak < 0.01f;
        }

        Assert.Equal(4, onsets.Count);
        Assert.All(onsets.Zip(onsets.Skip(1), (a, b) => b - a), gap => Assert.InRange(gap, 0.045 * Rate, 0.055 * Rate));
        Assert.True(right.Max(MathF.Abs) < 1e-6f, "placed hard left");
    }

    [Fact]
    public void Rendering_a_recipe_does_not_allocate()
    {
        var sound = RecipeSound.Create(RecipeLibrary.Get("fire"), Rate, 1);
        var left = new float[512];
        var right = new float[512];
        sound.Render(left, right, 0.7f);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++) sound.Render(left, right, 0.7f);

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>S17: a pad reads the ring's Waver knob, and turning it moves the pitch; a rain does not read it.</summary>
    [Fact]
    public void Waver_moves_a_pads_pitch_and_only_a_sound_that_reads_it_shows_the_knob()
    {
        Assert.True(RecipeLibrary.Get("pad-warm").Wavers);
        Assert.False(RecipeLibrary.Get("rain").Wavers);

        float[] Play(float waver)
        {
            var sound = RecipeSound.Create(RecipeLibrary.Get("pad-warm"), 48000, 5);
            sound.Waver = waver;
            var left = new float[512 * 288];
            var right = new float[left.Length];
            for (var at = 0; at < left.Length; at += 512) sound.Render(left.AsSpan(at, 512), right.AsSpan(at, 512), 0.8f);
            return left[^48000..];
        }

        var still = Play(0);
        Assert.Equal(still, Play(0));
        var moved = Play(1).Zip(still, (a, b) => (double)(a - b) * (a - b)).Average();
        Assert.True(moved > 1e-4 * still.Average(x => (double)x * x), "full waver changes the sound");
    }

    [Theory]
    [InlineData("{ not json", "Not valid JSON")]
    [InlineData("""{"id":"x","name":"X","family":"noise","colour":"#112233"}""", "Missing 'parts'")]
    [InlineData("""{"id":"x","name":"X","family":"noise","colour":"blue","parts":[{"type":"noise"}]}""", "must look like #6FA8DC")]
    [InlineData("""{"id":"x","name":"X","family":"noise","colour":"#112233","parts":[{"type":"organ"}]}""", "Unknown part type 'organ'")]
    [InlineData("""{"id":"x","name":"X","family":"noise","colour":"#112233","parts":[{"type":"noise","gain":"loud * 2"}]}""", "unknown name 'loud'")]
    [InlineData("""{"id":"x","name":"X","family":"noise","colour":"#112233","signals":[{"name":"a","expr":"b"},{"name":"b","walk":2}],"parts":[{"type":"noise"}]}""", "unknown name 'b'")]
    [InlineData("""{"id":"x","name":"X","family":"noise","colour":"#112233","parts":[{"type":"noise","filters":[{"type":"notch","freq":100}]}]}""", "Unknown filter 'notch'")]
    [InlineData("""{"id":"x","name":"X","family":"noise","strikes":"often","colour":"#112233","parts":[{"type":"noise"}]}""", "'strikes' is every or random")]
    [InlineData("""{"id":"x","name":"X","family":"jazz","colour":"#112233","parts":[{"type":"noise"}]}""", "Unknown family 'jazz'")]
    [InlineData("""{"id":"x","name":"X","family":"noise","places":["moon"],"colour":"#112233","parts":[{"type":"noise"}]}""", "Unknown word 'moon' in 'places'")]
    [InlineData("""{"id":"x","name":"X","family":"noise","moods":["sad"],"colour":"#112233","parts":[{"type":"noise"}]}""", "Unknown word 'sad' in 'moods'")]
    [InlineData("""{"id":"x","name":"X","family":"noise","colour":"#112233","signals":[{"name":"waver","walk":2}],"parts":[{"type":"noise"}]}""", "'waver' is used twice")]
    [InlineData("""{"id":"x","name":"X","family":"bells","colour":"#112233","parts":[{"type":"sample","files":["a"],"loop":false,"tune":"2"}]}""", "'tune' is for a loop")]
    [InlineData("""{"id":"x","name":"X","family":"rhythm","colour":"#112233","parts":[{"type":"sample","files":["a"],"pattern":{"every":4,"at":[0]}}]}""", "'pattern' is for one-shots")]
    [InlineData("""{"id":"x","name":"X","family":"rhythm","colour":"#112233","parts":[{"type":"sample","files":["a"],"loop":false,"pattern":{"every":4,"at":[0,4]}}]}""", "'at' must list")]
    [InlineData("""{"id":"x","name":"X","family":"rhythm","colour":"#112233","parts":[{"type":"sample","files":["a"],"loop":false,"pattern":{"every":4,"at":[0,1],"levels":[1]}}]}""", "one level per beat")]
    public void A_broken_recipe_says_what_is_wrong(string json, string message)
    {
        var e = Assert.Throws<RecipeException>(() => Recipe.Parse(json));
        Assert.Contains(message, e.Message);
    }

    [Theory]
    [InlineData("1 + 2 * 3", 7f)]
    [InlineData("(1 + 2) * 3", 9f)]
    [InlineData("2 ^ 3 ^ 2", 512f)]
    [InlineData("-2 ^ 2", -4f)]
    [InlineData("max(0, amount - 0.5) * 2", 0.4f)]
    [InlineData("step(amount, 0.5) + clamp(5, 0, 1)", 2f)]
    [InlineData("sqrt(-1) + (-8) ^ 0.5 + 1 / 0", 0f)]
    [InlineData("amount * ear + 1e-3", -0.699f)]
    public void Expressions_follow_the_usual_rules_and_never_produce_nan(string text, float expected)
    {
        var expr = Expr.Parse(text, ["amount", "ear"]);

        Assert.Equal(expected, expr.Eval([0.7f, -1f]), 3);
    }

    [Theory]
    [InlineData("1 +", "ends too early")]
    [InlineData("min(1)", "min takes 2 arguments")]
    [InlineData("speak(1)", "unknown function 'speak'")]
    [InlineData("1 2", "unexpected '2'")]
    public void Bad_expressions_are_refused_with_a_reason(string text, string message)
    {
        var e = Assert.Throws<RecipeException>(() => Expr.Parse(text, ["amount"]));
        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void Numbers_outside_their_bounds_are_held_at_the_bound()
    {
        var recipe = Recipe.Parse("""
            {"id":"x","name":"X","family":"noise","colour":"#112233",
             "signals":[{"name":"w","walk":0.0001}],
             "parts":[{"type":"pings","rate":1,"pitch":[5,99999],"decay":9,"voices":500}]}
            """);

        var walk = Assert.IsType<WalkSignal>(recipe.Signals[0]);
        var pings = Assert.IsType<PingsPart>(recipe.Parts[0]);
        Assert.Equal(0.02f, walk.Period);
        Assert.Equal((20f, 12000f), pings.Pitch);
        Assert.Equal(4f, pings.Decay.Max);
        Assert.Equal(Recipe.MaxVoices, pings.Voices);
    }

    private static (double Rms, double Peak) Measure(RecipeSound sound, float amount, int seconds)
    {
        var left = new float[512];
        var right = new float[512];
        double sum = 0, peak = 0;
        long count = 0;
        for (var b = 0; b < (seconds + 1) * Rate / 512; b++)
        {
            sound.Render(left, right, amount);
            if (b < Rate / 512) continue;
            for (var i = 0; i < 512; i++)
            {
                sum += left[i] * left[i] + right[i] * right[i];
                peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i])));
            }
            count += 1024;
        }
        return (20 * Math.Log10(Math.Sqrt(sum / count) + 1e-12), peak);
    }
}
