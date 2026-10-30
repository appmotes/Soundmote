using System.Text;
using Soundmote.Audio;
using Soundmote.Core.Scenes;

namespace Soundmote.Sandbox;

/// <summary>Plays a scene and redraws a small text dial: the hand's place and each ring's amount.</summary>
internal static class LiveView
{
    private const int BarWidth = 36;

    public static int Run(SceneRenderer renderer)
    {
        if (!AudioOutputs.LivePlaybackSupported)
        {
            Console.Error.WriteLine("Live playback is macOS-only for now. Use 'render <file.wav>'.");
            return 2;
        }

        var quit = false;
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            quit = true;
        };

        using var output = AudioOutputs.CreateDefault();
        output.Start(renderer);

        var interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
        var lines = 0;
        if (interactive) Console.Write("\e[?25l");
        try
        {
            while (!quit)
            {
                if (interactive)
                {
                    while (Console.KeyAvailable) quit |= Handle(Console.ReadKey(intercept: true), renderer);
                    var frame = Draw(renderer);
                    if (lines > 0) Console.Write($"\e[{lines}A");
                    Console.Write(frame);
                    lines = frame.Count(c => c == '\n');
                }
                if (renderer.Finished && renderer.Elapsed.TotalSeconds > renderer.Scene.Length.TotalSeconds + 4 * renderer.Speed)
                    quit = true;
                Thread.Sleep(66);
            }
        }
        finally
        {
            renderer.Paused = true;
            Thread.Sleep(300);
            output.Stop();
            if (interactive) Console.Write("\e[?25h");
        }
        return 0;
    }

    private static bool Handle(ConsoleKeyInfo key, SceneRenderer r)
    {
        var minute = TimeSpan.FromMinutes(1);
        switch (key.Key)
        {
            case ConsoleKey.Q or ConsoleKey.Escape:
                return true;
            case ConsoleKey.Spacebar:
                r.Paused = !r.Paused;
                break;
            case ConsoleKey.RightArrow:
                r.Seek(r.Elapsed + minute);
                break;
            case ConsoleKey.LeftArrow:
                r.Seek(r.Elapsed > minute ? r.Elapsed - minute : TimeSpan.Zero);
                break;
            case ConsoleKey.UpArrow:
                r.Speed = Math.Min(240, r.Speed * 2);
                break;
            case ConsoleKey.DownArrow:
                r.Speed = Math.Max(0.25, r.Speed / 2);
                break;
            case >= ConsoleKey.D1 and <= ConsoleKey.D9:
                var layer = key.Key - ConsoleKey.D1;
                if (layer < r.LayerCount) r.SetMuted(layer, !r.IsMuted(layer));
                break;
        }
        return false;
    }

    private static string Draw(SceneRenderer r)
    {
        var scene = r.Scene;
        var sb = new StringBuilder();
        var state = r.Finished ? "ended" : r.Paused ? "paused" : $"×{r.Speed:0.##}";
        sb.Append($"\e[2K\e[1m{scene.Name}\e[0m · {scene.Kind.ToString().ToLowerInvariant()} · {scene.Length.TotalMinutes:0} min · {state}\n");

        var hand = (int)Math.Round(r.Position * BarWidth);
        sb.Append($"\e[2K  {Clock(r.Elapsed),5} / {Clock(scene.Length)}  ");
        sb.Append('│').Append(new string('━', hand)).Append('●').Append(new string('─', BarWidth - hand)).Append('│');
        sb.Append('\n');

        for (var i = 0; i < r.LayerCount; i++)
        {
            var layer = r.LayerAt(i);
            var amount = r.AmountOf(i);
            var filled = (int)Math.Round(amount * BarWidth);
            var (red, green, blue) = Rgb(layer.Colour);
            var muted = r.IsMuted(i);
            sb.Append($"\e[2K  {i + 1} {layer.Name,-6} ");
            sb.Append(muted ? "\e[2m" : $"\e[38;2;{red};{green};{blue}m");
            sb.Append(new string('█', filled)).Append("\e[2m").Append(new string('░', BarWidth - filled)).Append("\e[0m");
            sb.Append(muted ? "  muted\n" : $"  {amount * 100,3:0} %\n");
        }

        sb.Append("\e[2K\e[2m  space pause · ←→ ±1 min · ↑↓ speed · 1–9 mute · q quit\e[0m\n");
        return sb.ToString();
    }

    private static string Clock(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    private static (int R, int G, int B) Rgb(string hex)
    {
        var v = Convert.ToInt32(hex.TrimStart('#'), 16);
        return ((v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
    }
}
