using Avalonia.Controls;
using Soundmote.App.ViewModels;
using Soundmote.Core.Output;

namespace Soundmote.App;

/// <summary>What the head supplies (A9): its window and the OS's audio output.</summary>
public interface IAppHost
{
    Window CreateWindow(ShellViewModel shell);

    /// <summary>The default output device, or null where the OS has no live output yet (N1).</summary>
    IAudioOutput? CreateOutput();

    /// <summary>The files a scene can be exported to here (A17).</summary>
    IAudioFiles Files { get; }

    /// <summary>The system has a menu bar or tray the app can live in (D16).</summary>
    bool SupportsMenuBar { get; }

    /// <summary>The icon in the menu bar or tray (D16), with Open, Settings and Quit in its menu.</summary>
    IDisposable CreateMenuBar(MenuBarViewModel menu, Action open, Action settings, Action quit);

    /// <summary>Closing the window hides it instead, so the scene plays on (D16, D18).</summary>
    void SetHideOnClose(Window window, bool hide);

    /// <summary>Shows a file, selected, or a folder in the system's file browser.</summary>
    void Reveal(string path);
}
