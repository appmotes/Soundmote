using System.Globalization;

namespace Soundmote.Sandbox;

internal sealed record Options(double Speed, TimeSpan At, int Seconds, ulong Seed, string? Solo, float? Hold, string? Recipe, string? Positional)
{
    public static Options Parse(string[] args)
    {
        var o = new Options(10, TimeSpan.Zero, 60, 1, null, null, null, null);
        for (var i = 0; i < args.Length; i++)
        {
            string Value() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
            o = args[i] switch
            {
                "--speed" => o with { Speed = double.Parse(Value(), CultureInfo.InvariantCulture) },
                "--at" => o with { At = TimeSpan.ParseExact(Value(), @"m\:ss", CultureInfo.InvariantCulture) },
                "--seconds" => o with { Seconds = int.Parse(Value(), CultureInfo.InvariantCulture) },
                "--seed" => o with { Seed = ulong.Parse(Value(), CultureInfo.InvariantCulture) },
                "--solo" => o with { Solo = Value() },
                "--recipe" => o with { Recipe = Value() },
                "--hold" => o with { Hold = Math.Clamp(float.Parse(Value(), CultureInfo.InvariantCulture), 0f, 1f) },
                var s when !s.StartsWith("--") && o.Positional is null => o with { Positional = s },
                var s => throw new ArgumentException($"Unknown option '{s}'.")
            };
        }
        return o;
    }
}
