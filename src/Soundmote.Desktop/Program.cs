using Avalonia;

namespace Soundmote.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure(() => new App.App { Host = new DesktopHost() })
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
