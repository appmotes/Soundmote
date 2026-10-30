using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Soundmote.Desktop.Tests.TestAppBuilder))]

namespace Soundmote.Desktop.Tests;

public static class TestAppBuilder
{
    // Real Skia drawing, so frames can be captured.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Soundmote.App.App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
