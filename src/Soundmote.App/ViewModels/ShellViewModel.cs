using System.Text.RegularExpressions;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Assets;
using Soundmote.App.Formatting;
using Soundmote.App.Settings;
using Soundmote.Core.Output;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// The window's two places (D13): the library and the dial of the open scene. Owns the open scene's player; opening
/// another scene stops the first and plays the new one, with the same volume. Keeps every change as it is made (A12).
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    /// <summary>How long a scene must rest before it is written, so dragging a curve writes once.</summary>
    private static readonly TimeSpan SaveAfter = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan ToastFor = TimeSpan.FromSeconds(8);

    /// <summary>How long the dial is left alone at night before the window dims to the bedside clock (D17).</summary>
    private static readonly TimeSpan BedsideAfter = TimeSpan.FromSeconds(15);

    private readonly SceneStore _store;
    private readonly Func<IAudioOutput?> _outputs;
    private readonly TimeProvider _clock;
    private readonly ulong _seed;
    private readonly Action<string>? _reveal;
    private SceneCardViewModel? _open;
    private SceneCardViewModel? _unsaved;
    private DateTimeOffset _editedAt;
    private SceneCardViewModel? _deleted;
    private DateTimeOffset _toastAt;
    private DateTimeOffset _touchedAt;
    private bool _waking;
    private readonly PreferencesStore? _preferencesStore;

    /// <param name="builtIn">Soundmote's scenes; a stored scene with the same id is shown instead, as changed.</param>
    /// <param name="outputs">Makes the output for each opened scene; the player owns and disposes it.</param>
    /// <param name="seed">The seed for every scene this launch (A6).</param>
    /// <param name="reveal">Shows a file or folder in the system's file browser.</param>
    /// <param name="preferences">Where settings are kept; without one, settings live in memory and start as
    /// <see cref="Preferences.Seasoned"/>.</param>
    public ShellViewModel(IReadOnlyList<Scene> builtIn, SceneStore store, Func<IAudioOutput?> outputs,
        TimeProvider? clock = null, ulong seed = 1, Action<string>? reveal = null, PreferencesStore? preferences = null)
    {
        _preferencesStore = preferences;
        Preferences = preferences?.Load() ?? Preferences.Seasoned;
        _store = store;
        _outputs = outputs;
        _clock = clock ?? TimeProvider.System;
        _seed = seed;
        _reveal = reveal;
        _touchedAt = _clock.GetUtcNow();
        var stored = store.Load(Skipped);
        var cards = builtIn
            .Select(b => stored.FirstOrDefault(s => s.Id == b.Id) is { } changed
                ? new SceneCardViewModel(changed with { Seed = seed }, b) { IsChanged = true }
                : new SceneCardViewModel(b, b))
            .Concat(stored.Where(s => builtIn.All(b => b.Id != s.Id)).Select(s => new SceneCardViewModel(s with { Seed = seed })));
        Library = new LibraryViewModel(cards);
        if (Skipped.Count > 0) ShowToast(Skipped.Count == 1 ? "One scene file could not be read" : $"{Skipped.Count} scene files could not be read");
        IsFirstRun = !Preferences.Welcomed;
        if (!IsFirstRun && Preferences.StartWith != StartWith.Scenes && Library.All.FirstOrDefault(c => c.Scene.Id == Preferences.LastScene) is { } last)
        {
            Play(last, playing: Preferences.StartWith == StartWith.PlayLastScene);
            IsLibraryOpen = false;
        }
    }

    /// <summary>The user's settings (D18). Change them with <see cref="SetPreferences"/>.</summary>
    public Preferences Preferences { get; private set; }

    /// <summary>Settings changed; the head shows or hides the menu bar and decides what closing the window does.</summary>
    public event Action? PreferencesChanged;

    /// <summary>The head can show a menu bar here (D16); set by the app.</summary>
    public bool SupportsMenuBar { get; set; }

    public void SetPreferences(Preferences preferences)
    {
        if (preferences == Preferences) return;
        Preferences = preferences;
        _preferencesStore?.Save(preferences);
        if (Player is not null) Player.StopFadeSeconds = preferences.FadeSeconds;
        PreferencesChanged?.Invoke();
    }

    /// <summary>The settings sheet while it is open (D18).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    public partial SettingsViewModel? Settings { get; private set; }

    public bool IsSettingsOpen => Settings is not null;

    [RelayCommand]
    private void OpenSettings()
    {
        NewScene = null;
        Settings ??= new SettingsViewModel(this, () => Settings = null);
    }

    /// <summary>The changed built-in scenes, which Reset all brings back.</summary>
    public int ChangedCount => Library.All.Count(c => c.CanReset);

    /// <summary>Every changed built-in scene back to Soundmote's version.</summary>
    public void ResetAll()
    {
        foreach (var card in Library.All.Where(c => c.CanReset).ToArray()) Reset(card);
    }

    public void RevealFolder()
    {
        Try(() => Directory.CreateDirectory(_store.Folder));
        _reveal?.Invoke(_store.Folder);
    }

    /// <summary>The first launch: four scenes to pick from instead of the library (D19).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsLibrary), nameof(ShowsFirstRun))]
    public partial bool IsFirstRun { get; private set; }

    /// <summary>A first-run card's Play: the scene opens and plays quietly, and first run is over.</summary>
    [RelayCommand]
    private void Welcome(SceneCardViewModel card)
    {
        EndFirstRun();
        Open(card);
        if (Player is not null) Player.Volume = FirstVolume;
    }

    /// <summary>"Or begin with an empty dial": the New scene sheet, and first run is over.</summary>
    [RelayCommand]
    private void BeginEmpty()
    {
        EndFirstRun();
        StartNewScene();
    }

    /// <summary>The first scene starts quietly; the dock has the rest (D19).</summary>
    private const double FirstVolume = 0.4;

    private void EndFirstRun()
    {
        IsFirstRun = false;
        SetPreferences(Preferences with { Welcomed = true });
    }

    /// <summary>The next first-run hint on the dial, or empty when all have been used (D19).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    public partial string HintTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string HintNote { get; private set; } = "";

    public bool HasHint => HintTitle.Length > 0;

    /// <summary>"Got it" on a hint: it never comes back.</summary>
    [RelayCommand]
    private void DismissHint()
    {
        if (Hints.All.FirstOrDefault(h => !Preferences.HintsSeen.Contains(h)) is { } hint) Seen(hint);
        UpdateHint();
    }

    private void Seen(string hint)
    {
        if (!Preferences.HintsSeen.Contains(hint)) SetPreferences(Preferences with { HintsSeen = [.. Preferences.HintsSeen, hint] });
    }

    /// <summary>A hint is used up by doing what it says: selecting a ring, moving the hand, opening the add panel.</summary>
    private void UpdateHint()
    {
        if (Player is { } player)
        {
            if (player.SelectedIndex >= 0) Seen(Hints.Shape);
            if (player.HasSought) Seen(Hints.Seek);
            if (player.IsAdding) Seen(Hints.Add);
        }
        var next = IsDialOpen ? Hints.All.FirstOrDefault(h => !Preferences.HintsSeen.Contains(h)) : null;
        (HintTitle, HintNote) = next switch
        {
            Hints.Shape => ("Click a ring to shape it", "Drag its points to make it louder or softer over the turn."),
            Hints.Seek => ("Drag the hand to move in time", "Hear what the scene does at minute 30 without waiting."),
            Hints.Add => ("+ brings in another sound", "It joins as a trial ring; keep it or let it go."),
            _ => ("", "")
        };
    }

    public LibraryViewModel Library { get; }

    /// <summary>Scene files that did not read at start, with why.</summary>
    public List<string> Skipped { get; } = [];

    /// <summary>The open scene, or null before the first is opened.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlayer), nameof(Ground))]
    public partial PlayerViewModel? Player { get; private set; }

    public bool HasPlayer => Player is not null;

    /// <summary>The card of the open scene.</summary>
    public SceneCardViewModel? OpenCard => _open;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDialOpen), nameof(Ground), nameof(ShowsLibrary), nameof(ShowsFirstRun))]
    public partial bool IsLibraryOpen { get; private set; } = true;

    /// <summary>The library, unless first run stands in its place (D19).</summary>
    public bool ShowsLibrary => IsLibraryOpen && !IsFirstRun;

    public bool ShowsFirstRun => IsLibraryOpen && IsFirstRun;

    public bool IsDialOpen => !IsLibraryOpen;

    /// <summary>The New scene sheet while it is open (D14).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCreating))]
    public partial NewSceneViewModel? NewScene { get; private set; }

    public bool IsCreating => NewScene is not null;

    /// <summary>The bar along the bottom that says what just happened (D13).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasToast))]
    public partial string Toast { get; private set; } = "";

    public bool HasToast => Toast.Length > 0;

    [ObservableProperty]
    public partial bool CanUndo { get; private set; }

    /// <summary>The menu's words for showing a scene's file, in the system's own words.</summary>
    public static string RevealText => OperatingSystem.IsMacOS() ? "Show in Finder"
        : OperatingSystem.IsWindows() ? "Show in Explorer" : "Show in Files";

    /// <summary>The library is on Paper; a dial is on its scene's ground (D12), and turns to day as its morning rises (D17).</summary>
    public ThemeVariant Ground => IsLibraryOpen || _open is null ? Grounds.Paper
        : Player is { IsWaking: true } ? Grounds.Sky
        : Grounds.For(_open.Scene.Ground);

    /// <summary>The window is dark but for a faint ring and the hour (D17). Any key or pointer move ends it.</summary>
    [ObservableProperty]
    public partial bool IsBedside { get; private set; }

    /// <summary>The hour on the bedside clock, "23:31".</summary>
    [ObservableProperty]
    public partial string BedsideTime { get; private set; } = "";

    /// <summary>"21 min left", or "waking at 07:00" once the journey has ended.</summary>
    [ObservableProperty]
    public partial string BedsideCaption { get; private set; } = "";

    /// <summary>A key or the pointer: ends the bedside dimming and starts its wait again. True if it was dim.</summary>
    public bool Touch()
    {
        _touchedAt = _clock.GetUtcNow();
        var was = IsBedside;
        IsBedside = false;
        return was;
    }

    /// <summary>Shows a scene's dial. A scene that was not open starts playing at once.</summary>
    [RelayCommand]
    private void Open(SceneCardViewModel card)
    {
        if (_open != card) Play(card, playing: true);
        IsLibraryOpen = false;
    }

    /// <summary>Plays a scene from the menu bar (D16), leaving the window on the library or a dial as it was.</summary>
    [RelayCommand]
    private void PlayScene(SceneCardViewModel card)
    {
        if (_open == card)
        {
            if (Player is { CanPlay: true } player) player.IsPlaying = true;
            return;
        }
        var library = IsLibraryOpen;
        Play(card, playing: true);
        IsLibraryOpen = library;
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        if (Player is not null) Player.SelectedIndex = -1;
        IsLibraryOpen = true;
    }

    [RelayCommand]
    private void ShowDial()
    {
        if (Player is not null) IsLibraryOpen = false;
    }

    [RelayCommand]
    private void StartNewScene()
    {
        Settings = null;
        NewScene ??= new NewSceneViewModel(Library.All.Select(c => c.Scene).ToArray(), Created, Preferences.NewGround);
    }

    /// <summary>A new scene lands among Yours and opens; an empty one opens the add panel for its first sound.</summary>
    private void Created(Scene? scene)
    {
        NewScene = null;
        if (scene is null) return;
        var card = Keep(scene);
        Open(card);
        if (scene.Layers.Count == 0) Player?.OpenAddCommand.Execute(null);
    }

    /// <summary>A copy called "Sit 2" among Yours, opened at once.</summary>
    [RelayCommand]
    private void Duplicate(SceneCardViewModel card)
    {
        var stem = Regex.Replace(card.Name, @" \d+$", "");
        var name = stem;
        for (var n = 2; Library.All.Any(c => c.Name == name); n++) name = $"{stem} {n}";
        Open(Keep(card.Scene with { Name = name }));
    }

    [RelayCommand]
    private void StartRename(SceneCardViewModel card)
    {
        foreach (var other in Library.All) other.IsRenaming = false;
        card.EditName = card.Name;
        card.IsRenaming = true;
    }

    /// <summary>Keeps the edited name; an empty one leaves the name as it was.</summary>
    [RelayCommand]
    private void CommitRename(SceneCardViewModel card)
    {
        if (!card.IsRenaming) return;
        card.IsRenaming = false;
        var name = card.EditName.Trim();
        if (name.Length == 0 || name == card.Name) return;
        card.Scene = card.Scene with { Name = name };
        if (card == _open && Player is not null) Player.Name = name;
        if (!card.IsBuiltIn)
        {
            Library.Remove(card);
            Library.Add(card);
        }
        Changed(card);
        Save();
    }

    [RelayCommand]
    private void CancelRename(SceneCardViewModel card) => card.IsRenaming = false;

    [RelayCommand]
    private void SetGround(GroundChoice choice)
    {
        if (choice.Card.Scene.Ground == choice.Ground) return;
        choice.Card.Scene = choice.Card.Scene with { Ground = choice.Ground };
        Changed(choice.Card);
        OnPropertyChanged(nameof(Ground));
    }

    /// <summary>Takes one of the user's own scenes away; the bar offers Undo for a few seconds (D13).</summary>
    [RelayCommand]
    private void Delete(SceneCardViewModel card)
    {
        if (!card.CanDelete) return;
        if (card == _open) Close();
        if (card == _unsaved) _unsaved = null;
        Library.Remove(card);
        Try(() => _store.Delete(card.Scene.Id));
        _deleted = card;
        ShowToast($"{card.Name} deleted");
        CanUndo = true;
    }

    [RelayCommand]
    private void Undo()
    {
        if (_deleted is not { } card) return;
        _deleted = null;
        Library.Add(card);
        Try(() => _store.Save(card.Scene));
        HideToast();
    }

    /// <summary>A changed built-in scene back to Soundmote's version. An open one keeps playing, as the original.</summary>
    [RelayCommand]
    private void Reset(SceneCardViewModel card)
    {
        if (!card.CanReset) return;
        if (card == _unsaved) _unsaved = null;
        Try(() => _store.Delete(card.Scene.Id));
        card.Scene = card.Original! with { Seed = _seed };
        card.IsChanged = false;
        if (card == _open) Play(card, Player?.IsPlaying ?? false);
        OnPropertyChanged(nameof(Ground));
    }

    [RelayCommand]
    private void Reveal(SceneCardViewModel card)
    {
        if (_reveal is null) return;
        var path = _store.PathOf(card.Scene.Id);
        Try(() => Directory.CreateDirectory(_store.Folder));
        _reveal(File.Exists(path) ? path : _store.Folder);
    }

    /// <summary>Reads the engine into the open dial and its card, and writes a scene that has rested. Meant for a timer at frame rate.</summary>
    public void Update()
    {
        var now = _clock.GetUtcNow();
        if (_unsaved is not null && now - _editedAt >= SaveAfter) Save();
        if (HasToast && now - _toastAt >= ToastFor) HideToast();
        UpdateHint();
        if (Player is not { } player)
        {
            IsBedside = false;
            return;
        }
        player.Update();
        if (_open is { } card)
        {
            card.IsPlaying = player.IsPlaying;
            card.Position = player.Position;
        }
        if (player.IsWaking != _waking)
        {
            _waking = player.IsWaking;
            OnPropertyChanged(nameof(Ground));
        }
        UpdateBedside(player, now);
    }

    /// <summary>
    /// Dims a playing dial left alone for 15 seconds, when it is a sleep scene or the hour is between 22 and 7, until
    /// its morning rises (D17). Settings can turn it off (D18).
    /// </summary>
    private void UpdateBedside(PlayerViewModel player, DateTimeOffset now)
    {
        var local = _clock.GetLocalNow();
        var night = player.Scene.Intent == Intent.Sleep || local.Hour >= 22 || local.Hour < 7;
        IsBedside = Preferences.DimAtNight && IsDialOpen && !IsSettingsOpen && player.IsPlaying && !player.IsWaking && night
            && now - _touchedAt >= BedsideAfter;
        if (!IsBedside) return;
        BedsideTime = SceneText.TimeOfDay(local);
        var left = player.Length - player.Renderer.Elapsed;
        BedsideCaption = player.Scene.Kind != TimeKind.Journey ? player.Name
            : left > TimeSpan.Zero ? $"{Math.Ceiling(left.TotalMinutes):0} min left"
            : player.Wake is { } wake ? $"waking at {SceneText.Clock24(wake.At)}" : "";
    }

    public void Dispose()
    {
        Save();
        Player?.Dispose();
    }

    /// <summary>Plays a card's scene in a new player, with the volume of the one before.</summary>
    private void Play(SceneCardViewModel card, bool playing)
    {
        var volume = Player?.Volume ?? 0.8;
        Close();
        _open = card;
        Player = new PlayerViewModel(card.Scene, _outputs(), _clock) { Volume = volume, StopFadeSeconds = Preferences.FadeSeconds };
        if (Preferences.LastScene != card.Scene.Id) SetPreferences(Preferences with { LastScene = card.Scene.Id });
        Player.Edited += OnEdited;
        if (playing && Player.CanPlay) Player.IsPlaying = true;
    }

    private void Close()
    {
        if (Player is not null)
        {
            Player.Edited -= OnEdited;
            Player.Dispose();
        }
        if (_open is not null) _open.IsPlaying = false;
        _open = null;
        Player = null;
        IsLibraryOpen = true;
    }

    private void OnEdited()
    {
        if (_open is not { } card || Player is null) return;
        card.Scene = card.Scene with { Layers = Player.CurrentLayers(), Wake = Player.Wake, Tempo = Math.Round(Player.Tempo) };
        Changed(card);
    }

    /// <summary>Marks a scene to be written once it has rested; a different scene waiting is written now.</summary>
    private void Changed(SceneCardViewModel card)
    {
        if (_unsaved is not null && _unsaved != card) Save();
        if (card.IsBuiltIn) card.IsChanged = true;
        _unsaved = card;
        _editedAt = _clock.GetUtcNow();
    }

    private void Save()
    {
        if (_unsaved is not { } card) return;
        _unsaved = null;
        Try(() => _store.Save(card.Scene));
    }

    /// <summary>A new scene of the user's own, with an id from its name, written at once.</summary>
    private SceneCardViewModel Keep(Scene scene)
    {
        var id = _store.NewId(scene.Name, Library.All.Select(c => c.Scene.Id));
        var card = new SceneCardViewModel(scene with { Id = id, Seed = _seed }) { IsNew = true };
        Library.Add(card);
        Try(() => _store.Save(card.Scene));
        return card;
    }

    /// <summary>A file the system refuses is said in the bar; the scene stays as it is in the app.</summary>
    private void Try(Action write)
    {
        try
        {
            write();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowToast($"Could not write the scenes folder: {e.Message}");
        }
    }

    private void ShowToast(string text)
    {
        Toast = text;
        CanUndo = false;
        _toastAt = _clock.GetUtcNow();
    }

    private void HideToast()
    {
        Toast = "";
        CanUndo = false;
        _deleted = null;
    }
}
