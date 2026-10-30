using System.Globalization;
using Soundmote.Audio;
using Soundmote.Core.Output;
using Soundmote.Core.Recipes;
using Soundmote.Core.Scenes;
using Soundmote.Sandbox;
using Soundmote.Sandbox.Made;

const string Usage = """
    Soundmote sandbox: plays the engine without the app.

      play   [options]               Plays the demo journey live (macOS).
      render <file.wav> [options]    Writes it to a WAV file instead.
      levels                         Prints loudness per sound and amount, to tune the engine.
      prepare <in.wav> <out.ogg> [--seconds <n>] [--highpass <hz>] [--takes <n>]
                                     Cuts the steadiest n seconds (default 75) of a recording into a sample (S13),
                                     without rumble below the high-pass (default 35 Hz). With --takes, cuts up to n
                                     strikes into out-1.ogg, out-2.ogg… instead. --crest <dB> lets peaks stand that
                                     far above the loudness (default 16; more for crackling fire),
                                     --release <ms> how fast it lets go after one (default 6).
      make <wav-folder> <ogg-folder> [id…]
                                     Renders the made pads and rhythms (S16) to WAV, then to the Ogg samples.

    Options:
      --speed <x>     Scene minutes per real minute (default 10: the 45-minute journey takes 4½ minutes).
      --at <mm:ss>    Start this far into the journey.
      --seconds <n>   Length of a render (default 60).
      --seed <n>      Variation seed (default 1). The same seed renders the same sound.
      --solo <name>   Only this ring: rain, wind or chant.
      --hold <0-1>    Ignore the curves and hold every ring at this amount.
      --recipe <file> Play one recipe file alone instead of the demo, held at --hold (default 0.7).
                      Any file in src/Soundmote.Core/Recipes/Library works, or one of your own (S10).
    """;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(Usage);
    return 0;
}

try
{
    if (args[0] == "prepare")
    {
        if (args.Length < 3 || args.Length % 2 == 0) throw new ArgumentException("prepare needs a recording and an output file.");
        var seconds = 75;
        var takes = 0;
        double? crest = null, release = null;
        var highPass = 35f;
        for (var i = 3; i < args.Length; i += 2)
        {
            if (args[i] == "--seconds") seconds = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
            else if (args[i] == "--highpass") highPass = float.Parse(args[i + 1], CultureInfo.InvariantCulture);
            else if (args[i] == "--takes") takes = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
            else if (args[i] == "--crest") crest = double.Parse(args[i + 1], CultureInfo.InvariantCulture);
            else if (args[i] == "--release") release = double.Parse(args[i + 1], CultureInfo.InvariantCulture) / 1000;
            else throw new ArgumentException($"Unknown option '{args[i]}'.");
        }
        if (takes > 0) Prepare.Takes(args[1], args[2], takes, highPass);
        else Prepare.Run(args[1], args[2], seconds, highPass, crest, release);
        return 0;
    }
    if (args[0] == "make")
    {
        if (args.Length < 3) throw new ArgumentException("make needs a folder for the WAVs and one for the Ogg samples.");
        Make.Run(args[1], args[2], args[3..]);
        return 0;
    }
    var options = Options.Parse(args.Skip(1).ToArray());
    switch (args[0])
    {
        case "play":
            return LiveView.Run(CreateRenderer(options));
        case "render":
            var path = options.Positional ?? throw new ArgumentException("render needs a file name.");
            var renderer = CreateRenderer(options);
            Console.WriteLine($"Rendering {options.Seconds} s of {renderer.Scene.Name} at ×{renderer.Speed} to {path}…");
            WavWriter.Write(path, renderer, TimeSpan.FromSeconds(options.Seconds));
            Console.WriteLine($"Done. The journey reached {renderer.Elapsed:mm\\:ss}.");
            return 0;
        case "levels":
            Levels.Print();
            return 0;
        default:
            Console.Error.WriteLine($"Unknown command '{args[0]}'.\n");
            Console.Error.WriteLine(Usage);
            return 2;
    }
}
catch (Exception e) when (e is ArgumentException or FormatException or InvalidDataException or PlatformNotSupportedException or RecipeException or IOException)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}

static SceneRenderer CreateRenderer(Options o)
{
    var scene = BuiltInScenes.FallAsleep(o.Seed);
    if (o.Recipe is { } file)
    {
        var recipe = Recipe.Parse(File.ReadAllText(file));
        scene = scene with { Name = recipe.Name, Phases = [], Layers = [new Layer(recipe.Name, recipe, Curve.Steady(o.Hold ?? 0.7f))] };
    }
    if (o.Solo is { } solo)
    {
        var layers = scene.Layers.Where(l => l.Name.Equals(solo, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (layers.Length == 0) throw new ArgumentException($"No ring called '{solo}'. Try rain, wind or chant.");
        scene = scene with { Layers = layers };
    }
    if (o.Hold is { } hold)
        scene = scene with { Layers = scene.Layers.Select(l => l with { Curve = Curve.Steady(hold) }).ToArray() };

    var renderer = new SceneRenderer(scene) { Speed = o.Speed };
    renderer.Seek(o.At);
    return renderer;
}
