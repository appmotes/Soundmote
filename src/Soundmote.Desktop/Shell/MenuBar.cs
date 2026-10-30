using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Controls;
using Soundmote.App.ViewModels;

namespace Soundmote.Desktop.Shell;

/// <summary>
/// The icon in the menu bar, notification area or tray (D16) and its menu, kept in step with
/// <see cref="MenuBarViewModel"/>. The tray cannot say where it is on screen, so the menu is the system's own.
/// </summary>
public sealed class MenuBar : IDisposable
{
    /// <summary>The icon's size in pixels: 22 points at twice the density.</summary>
    private const int IconPixels = 44;

    private const int ThumbPixels = 32;

    private readonly MenuBarViewModel _menu;
    private readonly TrayIcon _tray;
    private readonly NativeMenu _root = new();
    private readonly NativeMenuItem _title = new() { IsEnabled = false };
    private readonly NativeMenuItem _status = new() { IsEnabled = false };
    private readonly NativeMenuItem _play = new();
    private readonly NativeMenuItem _next = new();
    private readonly NativeMenuItem _stopLap;
    private readonly NativeMenuItem[] _stops;
    private readonly int _scenesAt;
    private SceneCardViewModel[] _listed = [];

    /// <param name="open">Shows the window; <paramref name="settings"/> shows it with Settings open; <paramref name="quit"/> ends the app.</param>
    public MenuBar(MenuBarViewModel menu, Action open, Action settings, Action quit)
    {
        _menu = menu;
        _play.Command = menu.TogglePlayCommand;
        _next.Command = menu.SkipToNextCommand;
        var stopMenu = new NativeMenu();
        _stops =
        [
            Stop(stopMenu, "Never", StopAfter.Never),
            _stopLap = Stop(stopMenu, "After this lap", StopAfter.ThisLap),
            Stop(stopMenu, "In 30 minutes", StopAfter.HalfHour),
            Stop(stopMenu, "In 1 hour", StopAfter.Hour)
        ];

        _root.Items.Add(_title);
        _root.Items.Add(_status);
        _root.Items.Add(_play);
        _root.Items.Add(_next);
        _root.Items.Add(new NativeMenuItemSeparator());
        _scenesAt = _root.Items.Count;
        _root.Items.Add(new NativeMenuItemSeparator());
        _root.Items.Add(new NativeMenuItem("Stop after") { Menu = stopMenu });
        _root.Items.Add(new NativeMenuItemSeparator());
        _root.Items.Add(new NativeMenuItem("Open Soundmote") { Command = new RelayCommand(open), Gesture = Key(Avalonia.Input.Key.O) });
        _root.Items.Add(new NativeMenuItem("Settings…") { Command = new RelayCommand(settings), Gesture = Key(Avalonia.Input.Key.OemComma) });
        _root.Items.Add(new NativeMenuItem("Quit Soundmote") { Command = new RelayCommand(quit), Gesture = Key(Avalonia.Input.Key.Q) });

        _tray = new TrayIcon { Menu = _root };
        // macOS tints a template icon for a light or dark menu bar; elsewhere the ink is white for a dark taskbar.
        if (OperatingSystem.IsMacOS()) MacOSProperties.SetIsTemplateIcon(_tray, true);
        // A left click opens the window where the system shows the menu on a right click; macOS shows the menu on any click.
        else _tray.Clicked += (_, _) => open();
        _tray.IsVisible = true;

        menu.PropertyChanged += OnChanged;
        Apply();
    }

    private static Color Ink => OperatingSystem.IsMacOS() ? Colors.Black : Colors.White;

    private static KeyGesture Key(Key key) => new(key, OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);

    private NativeMenuItem Stop(NativeMenu menu, string header, StopAfter when)
    {
        var item = new NativeMenuItem(header) { ToggleType = MenuItemToggleType.Radio, Command = _menu.SetStopAfterCommand, CommandParameter = when };
        menu.Items.Add(item);
        return item;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Apply(e.PropertyName);

    /// <summary>Copies what changed into the native items; the icon is drawn again only when its state changed.</summary>
    private void Apply(string? changed = null)
    {
        _title.Header = _menu.Title;
        _status.Header = _menu.Status;
        _play.Header = _menu.PlayText;
        _play.IsEnabled = _menu.CanPlay;
        _next.Header = _menu.NextText;
        _next.IsVisible = _menu.NextText.Length > 0;
        _stopLap.IsVisible = _menu.IsLoop;
        foreach (var item in _stops) item.IsChecked = Equals(item.CommandParameter, _menu.StopAfter);
        _tray.ToolTipText = $"{_menu.Title} · {_menu.Status}";
        if (changed is null or nameof(MenuBarViewModel.Glyph))
            _tray.Icon = new WindowIcon(MenuBarGlyph.Render(_menu.Glyph, Ink, IconPixels));
        ListScenes();
    }

    /// <summary>One radio item per scene, with its small dial; rebuilt when the scenes or the open one change.</summary>
    private void ListScenes()
    {
        var scenes = _menu.Scenes;
        if (!scenes.SequenceEqual(_listed))
        {
            for (var i = 0; i < _listed.Length; i++) _root.Items.RemoveAt(_scenesAt);
            _listed = scenes.ToArray();
            for (var i = 0; i < _listed.Length; i++)
            {
                var card = _listed[i];
                _root.Items.Insert(_scenesAt + i, new NativeMenuItem(card.Name)
                {
                    ToggleType = MenuItemToggleType.Radio,
                    Command = _menu.PlayCommand,
                    CommandParameter = card,
                    Icon = Thumb(card),
                    Gesture = i < 9 ? Key(Avalonia.Input.Key.D1 + i) : null
                });
            }
        }
        for (var i = 0; i < _listed.Length; i++)
        {
            var item = (NativeMenuItem)_root.Items[_scenesAt + i];
            item.Header = _listed[i].Name;
            item.IsChecked = _listed[i] == _menu.Current;
        }
    }

    /// <summary>The scene's rings as a small picture for its menu item.</summary>
    private static Bitmap Thumb(SceneCardViewModel card)
    {
        var thumb = new SceneThumb
        {
            Rings = card.Rings,
            Position = 1,
            GuideBrush = new SolidColorBrush(Colors.Gray, 0.3),
            Width = ThumbPixels,
            Height = ThumbPixels
        };
        var size = new Size(ThumbPixels, ThumbPixels);
        thumb.Measure(size);
        thumb.Arrange(new Rect(size));
        var bitmap = new RenderTargetBitmap(new PixelSize(ThumbPixels, ThumbPixels));
        bitmap.Render(thumb);
        return bitmap;
    }

    public void Dispose()
    {
        _menu.PropertyChanged -= OnChanged;
        _tray.Dispose();
    }
}
