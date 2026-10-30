using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Settings;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.App;

public partial class App : Application
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(33);

    private ShellViewModel? _shell;
    private MenuBarViewModel? _menu;
    private IDisposable? _menuBar;
    private DispatcherTimer? _timer;

    /// <summary>The head's window and audio output; required under the desktop lifetime.</summary>
    public IAppHost? Host { get; init; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // The app menu on macOS, set before the platform looks for one; without it the system's About would be the
        // toolkit's. The system adds Hide and Quit. Elsewhere it is ignored.
        var openSettings = new RelayCommand(OpenSettings);
        NativeMenu.SetMenu(this, new NativeMenu
        {
            Items =
            {
                new NativeMenuItem("About Soundmote") { Command = openSettings },
                new NativeMenuItemSeparator(),
                new NativeMenuItem("Settings…") { Command = openSettings, Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta) }
            }
        });
    }

    /// <summary>About and Settings both open the Settings sheet, whose top is About (D18).</summary>
    private void OpenSettings()
    {
        if (_shell is null || (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } window) return;
        Show(window);
        _shell.OpenSettingsCommand.Execute(null);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var host = Host ?? throw new InvalidOperationException("App needs a Host to create its window.");
            // A new seed per launch: the same scenes, never the same sound twice (A6).
            var seed = (ulong)Random.Shared.NextInt64();
            var shell = _shell = new ShellViewModel(BuiltInScenes.All(seed), new SceneStore(SceneStore.DefaultFolder()),
                host.CreateOutput, seed: seed, reveal: host.Reveal, preferences: new PreferencesStore(PreferencesStore.DefaultPath()))
            {
                SupportsMenuBar = host.SupportsMenuBar
            };
            var window = host.CreateWindow(shell);
            desktop.MainWindow = window;
            var menu = _menu = new MenuBarViewModel(shell);
            if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
                activatable.Activated += (_, e) =>
                {
                    if (e.Kind == ActivationKind.Reopen) Show(window);
                };
            // The menu bar and what closing the window does follow Settings (D16, D18).
            void Apply()
            {
                var wanted = host.SupportsMenuBar && shell.Preferences.ShowMenuBar;
                if (wanted && _menuBar is null)
                    _menuBar = host.CreateMenuBar(menu, () => Show(window), () =>
                    {
                        Show(window);
                        shell.OpenSettingsCommand.Execute(null);
                    }, () => desktop.Shutdown());
                else if (!wanted && _menuBar is not null)
                {
                    _menuBar.Dispose();
                    _menuBar = null;
                }
                var keep = _menuBar is not null && shell.Preferences.KeepPlaying;
                host.SetHideOnClose(window, keep);
                desktop.ShutdownMode = keep ? ShutdownMode.OnExplicitShutdown : ShutdownMode.OnLastWindowClose;
            }
            shell.PreferencesChanged += Apply;
            Apply();
            _timer = new DispatcherTimer(Frame, DispatcherPriority.Background, (_, _) =>
            {
                _shell?.Update();
                _menu?.Update();
            });
            _timer.Start();
            desktop.ShutdownRequested += (_, _) => Shutdown();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void Show(Window window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private void Shutdown()
    {
        _timer?.Stop();
        _menuBar?.Dispose();
        _menuBar = null;
        _shell?.Dispose();
        _timer = null;
        _shell = null;
    }
}
