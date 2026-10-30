using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Assets;
using Soundmote.App.Settings;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>The settings sheet (D18). Every change is kept at once; there is nothing to confirm but Reset all.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly Action _close;

    internal SettingsViewModel(ShellViewModel shell, Action close)
    {
        _shell = shell;
        _close = close;
        Fades = new[] { 3, 10, 30 }.Select(s => new ChoiceViewModel((double)s, $"{s} s")).ToArray();
        Starts = [new(StartWith.Scenes, "Scenes"), new(StartWith.LastScene, "Last scene"), new(StartWith.PlayLastScene, "Play it")];
        Grounds = [new ChoiceViewModel(null, "What it is for"),
            .. Enum.GetValues<Ground>().Select(g => new ChoiceViewModel(g, g.ToString()) { Ground = Assets.Grounds.For(g) })];
        var command = OperatingSystem.IsMacOS() ? "⌘" : "Ctrl+";
        Keys =
        [
            new("Space", "Play, pause"), new("← →", "Back, on 1 min"), new("1–9", "Mute a ring"), new("Esc", "Let go"),
            new($"{command}N", "New scene"), new($"{command}D", "Duplicate"), new($"{command},", "Settings")
        ];
        Refresh();
    }

    private Preferences Preferences => _shell.Preferences;

    public IReadOnlyList<ChoiceViewModel> Fades { get; }
    public IReadOnlyList<ChoiceViewModel> Starts { get; }
    public IReadOnlyList<ChoiceViewModel> Grounds { get; }
    public IReadOnlyList<KeyItem> Keys { get; }

    /// <summary>"Soundmote 0.1.0": the name and the version the app was built as, without the build's source id.</summary>
    public string AboutTitle { get; } = "Soundmote " + (typeof(SettingsViewModel).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "");

    /// <summary>The menu bar exists on this system (D16); keeping the sound playing needs it.</summary>
    public bool SupportsMenuBar => _shell.SupportsMenuBar;

    public bool ShowMenuBar
    {
        get => Preferences.ShowMenuBar;
        set => Set(Preferences with { ShowMenuBar = value });
    }

    public bool KeepPlaying
    {
        get => Preferences.KeepPlaying;
        set => Set(Preferences with { KeepPlaying = value });
    }

    public bool CanKeepPlaying => SupportsMenuBar && Preferences.ShowMenuBar;

    public bool DimAtNight
    {
        get => Preferences.DimAtNight;
        set => Set(Preferences with { DimAtNight = value });
    }

    /// <summary>"Kept in Application Support", in the system's words.</summary>
    public string FolderText => OperatingSystem.IsMacOS() ? "Kept in Application Support"
        : OperatingSystem.IsWindows() ? "Kept in AppData" : "Kept in your data folder";

    public string RevealText => ShellViewModel.RevealText;

    /// <summary>Reset all asks once, with how many scenes it will change.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetText))]
    public partial bool IsConfirmingReset { get; private set; }

    public string ResetText => IsConfirmingReset
        ? $"Reset {_shell.ChangedCount} {(_shell.ChangedCount == 1 ? "scene" : "scenes")}"
        : "Reset all…";

    public bool CanReset => _shell.ChangedCount > 0;

    [RelayCommand]
    private void ChooseFade(ChoiceViewModel choice) => Set(Preferences with { FadeSeconds = (double)choice.Value! });

    [RelayCommand]
    private void ChooseStart(ChoiceViewModel choice) => Set(Preferences with { StartWith = (StartWith)choice.Value! });

    [RelayCommand]
    private void ChooseGround(ChoiceViewModel choice) => Set(Preferences with { NewGround = choice.Value as Ground? });

    [RelayCommand]
    private void RevealFolder() => _shell.RevealFolder();

    [RelayCommand]
    private void ResetAll()
    {
        if (!CanReset) return;
        if (!IsConfirmingReset)
        {
            IsConfirmingReset = true;
            return;
        }
        _shell.ResetAll();
        IsConfirmingReset = false;
        Refresh();
    }

    [RelayCommand]
    private void Close() => _close();

    private void Set(Preferences preferences)
    {
        _shell.SetPreferences(preferences);
        Refresh();
    }

    private void Refresh()
    {
        foreach (var c in Fades) c.IsChecked = (double)c.Value! == Preferences.FadeSeconds;
        foreach (var c in Starts) c.IsChecked = (StartWith)c.Value! == Preferences.StartWith;
        foreach (var c in Grounds) c.IsChecked = Equals(c.Value as Ground?, Preferences.NewGround);
        OnPropertyChanged(nameof(ShowMenuBar));
        OnPropertyChanged(nameof(KeepPlaying));
        OnPropertyChanged(nameof(CanKeepPlaying));
        OnPropertyChanged(nameof(DimAtNight));
        OnPropertyChanged(nameof(ResetText));
        OnPropertyChanged(nameof(CanReset));
    }
}

/// <summary>One key and what it does, in the settings' list of keys.</summary>
public sealed record KeyItem(string Key, string Text);
