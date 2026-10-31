using System.Diagnostics;
using Avalonia.Controls;
using Soundmote.App;
using Soundmote.App.ViewModels;
using Soundmote.Audio;
using Soundmote.Core.Output;
using Soundmote.Desktop.Shell;

namespace Soundmote.Desktop;

public sealed class DesktopHost : IAppHost
{
    public Window CreateWindow(ShellViewModel shell) => new MainWindow(shell);

    /// <summary>
    /// Linux desktops without a tray would leave a hidden window no one can reach, so there is no menu bar there and
    /// closing the window quits (D16).
    /// </summary>
    public bool SupportsMenuBar => !OperatingSystem.IsLinux();

    public IDisposable CreateMenuBar(MenuBarViewModel menu, Action open, Action settings, Action quit) =>
        new MenuBar(menu, open, settings, quit);

    public void SetHideOnClose(Window window, bool hide)
    {
        if (window is MainWindow main) main.HideOnClose = hide;
    }

    public IAudioFiles Files { get; } = new AudioFiles();

    public IAudioOutput? CreateOutput() => AudioOutputs.LivePlaybackSupported ? AudioOutputs.CreateDefault() : null;

    public void Reveal(string path)
    {
        var file = File.Exists(path);
        var start = OperatingSystem.IsMacOS() ? new ProcessStartInfo("open", file ? ["-R", path] : [path])
            : OperatingSystem.IsWindows() ? new ProcessStartInfo("explorer.exe", file ? $"/select,\"{path}\"" : $"\"{path}\"")
            : new ProcessStartInfo("xdg-open", [file ? Path.GetDirectoryName(path)! : path]);
        start.UseShellExecute = false;
        using var _ = Process.Start(start);
    }
}
