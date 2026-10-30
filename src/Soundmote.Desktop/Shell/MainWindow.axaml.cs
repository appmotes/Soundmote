using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Soundmote.App.ViewModels;

namespace Soundmote.Desktop.Shell;

/// <summary>How the top of the window looks on each system (D2).</summary>
public enum TitleStrip
{
    /// <summary>The system's buttons at the left over the strip; the brand after them.</summary>
    Mac,

    /// <summary>The brand at the left edge; the system's three buttons over the strip at the right.</summary>
    Windows,

    /// <summary>The desktop's own title bar carries the name; the strip keeps only its links, and is lower.</summary>
    Linux
}

public partial class MainWindow : Window
{
    /// <summary>Room left of the brand for the macOS window buttons (D2).</summary>
    private const double MacButtonsWidth = 96;

    /// <summary>Room at the right for the three Windows caption buttons (D2).</summary>
    private const double WindowsButtonsWidth = 150;

    public MainWindow() : this(null, Current)
    {
    }

    /// <param name="strip">The system's look; tests ask for each one.</param>
    public MainWindow(ShellViewModel? shell, TitleStrip strip)
    {
        InitializeComponent();
        DataContext = shell;
        switch (strip)
        {
            case TitleStrip.Mac:
                ExtendClientAreaToDecorationsHint = true;
                ExtendClientAreaTitleBarHeightHint = 52;
                Strip.Margin = new Thickness(MacButtonsWidth, 0, 24, 0);
                break;
            case TitleStrip.Windows:
                ExtendClientAreaToDecorationsHint = true;
                ExtendClientAreaTitleBarHeightHint = 52;
                Strip.Margin = new Thickness(32, 0, WindowsButtonsWidth, 0);
                break;
            default:
                // The system's own title bar shows the name and the icon.
                Brand.IsVisible = BrandIcon.IsVisible = false;
                Strip.Height = 44;
                break;
        }
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        // Any pointer movement or press wakes the bedside clock and starts its wait again (D17).
        AddHandler(PointerMovedEvent, (_, _) => Shell?.Touch(), RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
    }

    public MainWindow(ShellViewModel shell) : this(shell, Current)
    {
    }

    public static TitleStrip Current => OperatingSystem.IsMacOS() ? TitleStrip.Mac
        : OperatingSystem.IsWindows() ? TitleStrip.Windows : TitleStrip.Linux;

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    /// <summary>Set while the menu bar exists (D16): closing the window hides it, and the scene plays on.</summary>
    public bool HideOnClose { get; set; }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (!HideOnClose || e.CloseReason != WindowCloseReason.WindowClosing || e.IsProgrammatic) return;
        e.Cancel = true;
        Hide();
    }

    /// <summary>A click on the dark bedside window only wakes it.</summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Shell?.Touch() == true) e.Handled = true;
    }

    /// <summary>
    /// Keys (D7): ⌘N (Ctrl+N) opens New scene, ⌘D duplicates the open scene and ⌘, opens Settings, where Escape closes.
    /// On the New scene sheet, Return creates and Escape cancels. Single keys: space plays, arrows move the hand a
    /// minute, digits mute a ring, Escape closes, clears, then goes back to the library. In the library only space
    /// works, on the open scene.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (Shell is not { } shell) return;
        // The first key on the dark bedside window only wakes it (D17).
        if (shell.Touch())
        {
            e.Handled = true;
            return;
        }
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (shell.Settings is { } settings)
        {
            if (e.Key == Key.Escape) settings.CloseCommand.Execute(null);
            else if (!(e.KeyModifiers == command && e.Key == Key.OemComma)) return;
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers == command && e.Key == Key.OemComma)
        {
            shell.OpenSettingsCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (shell.NewScene is { } sheet)
        {
            if (e.Key == Key.Escape) sheet.CancelCommand.Execute(null);
            else if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None) sheet.CreateCommand.Execute(null);
            else return;
            e.Handled = true;
            return;
        }
        if (e.KeyModifiers == command && e.Key is Key.N or Key.D)
        {
            if (e.Key == Key.N) shell.StartNewSceneCommand.Execute(null);
            else if (shell.OpenCard is { } open) shell.DuplicateCommand.Execute(open);
            e.Handled = true;
            return;
        }
        if (shell.Player is not { } player || e.KeyModifiers != KeyModifiers.None)
            return;
        // Typing a name or a search keeps its keys (D7).
        if (FocusManager?.GetFocusedElement() is TextBox && e.Key != Key.Escape)
            return;
        if (shell.IsLibraryOpen)
        {
            if (e.Key != Key.Space) return;
            player.TogglePlayCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape && player.AddSound is { } adding)
        {
            // An open family goes back to all of them first (D20).
            if (adding.IsOpen) adding.BackCommand.Execute(null);
            else adding.CancelCommand.Execute(null);
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            case Key.Space:
                player.TogglePlayCommand.Execute(null);
                break;
            case Key.Right:
                player.SkipCommand.Execute(1);
                break;
            case Key.Left:
                player.SkipCommand.Execute(-1);
                break;
            case Key.Escape when player.SelectedIndex >= 0:
                player.SelectedIndex = -1;
                break;
            case Key.Escape:
                shell.ShowLibraryCommand.Execute(null);
                break;
            case >= Key.D1 and <= Key.D9 when e.Key - Key.D1 < player.Layers.Count:
                player.ToggleMuteCommand.Execute(player.Layers[e.Key - Key.D1]);
                break;
            default:
                return;
        }
        e.Handled = true;
    }
}
