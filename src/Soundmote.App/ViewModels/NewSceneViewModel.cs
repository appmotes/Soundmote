using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Assets;
using Soundmote.App.Controls;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// The New scene sheet (D14): what it is for, how time moves, how long, what it starts from, its ground and name.
/// What it is for picks the kind of time, the ground and the phases until the user picks them; the preview follows.
/// </summary>
public sealed partial class NewSceneViewModel : ObservableObject
{
    private static readonly int[] JourneyMinutes = [10, 20, 30, 45, 60, 90];
    private static readonly int[] LoopMinutes = [30, 60, 90];

    private readonly IReadOnlyList<Scene> _scenes;
    private readonly Action<Scene?> _done;
    private bool _ready, _kindChosen, _groundChosen, _lengthChosen;

    /// <param name="scenes">Every scene, for Start from and for a name not yet taken.</param>
    /// <param name="done">Called with the new scene, or null when cancelled.</param>
    /// <param name="ground">The ground from Settings (D18), or null to take it from what the scene is for.</param>
    public NewSceneViewModel(IReadOnlyList<Scene> scenes, Action<Scene?> done, Ground? ground = null)
    {
        _scenes = scenes;
        _done = done;
        Intents = Enum.GetValues<Intent>().Select(i => new ChoiceViewModel(i, i.ToString())).ToArray();
        Kinds =
        [
            Kind(TimeKind.Journey, "Has an end. The rings settle and the sound fades out with it."),
            Kind(TimeKind.Loop, "Goes round until you stop. A lap and a break, again and again."),
            Kind(TimeKind.Clock, "Follows the real day. The hand is the time of day, 24 hours a turn.")
        ];
        Grounds = Enum.GetValues<Ground>().Select(g => new ChoiceViewModel(g, g.ToString()) { Ground = Assets.Grounds.For(g) }).ToArray();
        var name = "New scene";
        for (var n = 2; scenes.Any(s => s.Name == name); n++) name = $"New scene {n}";
        Name = name;
        if (ground is { } chosen)
        {
            _groundChosen = true;
            Check(Grounds, Grounds.Single(g => (Ground)g.Value! == chosen));
        }
        ChooseIntent(Intents[0]);
        _ready = true;
        Update();
    }

    public IReadOnlyList<ChoiceViewModel> Intents { get; }

    public IReadOnlyList<ChoiceViewModel> Kinds { get; }

    /// <summary>Minutes for a journey or a loop; empty for the clock, which is a day.</summary>
    public ObservableCollection<ChoiceViewModel> Lengths { get; } = [];

    /// <summary>Empty, or the sounds of a scene with the same purpose.</summary>
    public ObservableCollection<ChoiceViewModel> Starts { get; } = [];

    public IReadOnlyList<ChoiceViewModel> Grounds { get; }

    public bool HasLengths => Lengths.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string Name { get; set; }

    /// <summary>The scene as chosen so far; Create makes this one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewRings), nameof(PreviewGround), nameof(PreviewText), nameof(PhasesText), nameof(IsEmpty))]
    public partial Scene Preview { get; private set; } = new("", TimeKind.Journey, TimeSpan.FromMinutes(30), []);

    public IReadOnlyList<DialRing> PreviewRings => Preview.Layers.Select((_, i) => DialRing.Of(Preview, i)).ToArray();

    public ThemeVariant PreviewGround => Assets.Grounds.For(Preview.Ground);

    /// <summary>"A 30-minute journey".</summary>
    public string PreviewText => Preview.Kind switch
    {
        TimeKind.Journey => $"A {Preview.Length.TotalMinutes:0}-minute journey",
        TimeKind.Loop => $"A {Preview.Length.TotalMinutes:0}-minute loop",
        _ => "Follows the clock, a day a turn"
    };

    public string PhasesText => Preview.Phases.Count == 0 ? "No phases" : string.Join(" · ", Preview.Phases.Select(p => p.Name));

    public bool IsEmpty => Preview.Layers.Count == 0;

    partial void OnNameChanged(string value) => Update();

    [RelayCommand]
    private void ChooseIntent(ChoiceViewModel choice)
    {
        Check(Intents, choice);
        var intent = (Intent)choice.Value!;
        if (!_kindChosen) Check(Kinds, Kinds.Single(k => (TimeKind)k.Value! == KindFor(intent)));
        if (!_groundChosen) Check(Grounds, Grounds.Single(g => (Ground)g.Value! == GroundFor(intent)));
        FillLengths();
        Starts.Clear();
        Starts.Add(new ChoiceViewModel(null, "Empty") { IsChecked = true });
        foreach (var scene in _scenes.Where(s => s.Intent == intent && s.Layers.Count > 0))
            Starts.Add(new ChoiceViewModel(scene, $"{scene.Name}'s sounds"));
        Update();
    }

    [RelayCommand]
    private void ChooseKind(ChoiceViewModel choice)
    {
        _kindChosen = true;
        Check(Kinds, choice);
        FillLengths();
        Update();
    }

    [RelayCommand]
    private void ChooseLength(ChoiceViewModel choice)
    {
        _lengthChosen = true;
        Check(Lengths, choice);
        Update();
    }

    [RelayCommand]
    private void ChooseStart(ChoiceViewModel choice)
    {
        Check(Starts, choice);
        Update();
    }

    [RelayCommand]
    private void ChooseGround(ChoiceViewModel choice)
    {
        _groundChosen = true;
        Check(Grounds, choice);
        Update();
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create() => _done(Preview with { Name = Name.Trim() });

    private bool CanCreate() => Name.Trim().Length > 0;

    [RelayCommand]
    private void Cancel() => _done(null);

    private void FillLengths()
    {
        var kind = Chosen<TimeKind>(Kinds);
        var minutes = kind switch { TimeKind.Journey => JourneyMinutes, TimeKind.Loop => LoopMinutes, _ => [] };
        var previous = Lengths.FirstOrDefault(l => l.IsChecked)?.Value as int?;
        var wanted = _lengthChosen && previous is { } p && minutes.Contains(p) ? p : MinutesFor(Chosen<Intent>(Intents), kind);
        Lengths.Clear();
        foreach (var m in minutes) Lengths.Add(new ChoiceViewModel(m, m.ToString()) { IsChecked = m == wanted });
        OnPropertyChanged(nameof(HasLengths));
    }

    private void Update()
    {
        if (!_ready) return;
        var intent = Chosen<Intent>(Intents);
        var kind = Chosen<TimeKind>(Kinds);
        var length = kind == TimeKind.Clock ? TimeSpan.FromDays(1) : TimeSpan.FromMinutes((int)Lengths.First(l => l.IsChecked).Value!);
        var start = Starts.FirstOrDefault(s => s.IsChecked)?.Value as Scene;
        Preview = new Scene(Name.Trim(), kind, length, start?.Layers ?? [])
        {
            Intent = intent,
            Ground = Chosen<Ground>(Grounds),
            Phases = PhasesFor(intent, kind)
        };
    }

    private static void Check(IEnumerable<ChoiceViewModel> row, ChoiceViewModel chosen)
    {
        foreach (var c in row) c.IsChecked = c == chosen;
    }

    private static T Chosen<T>(IEnumerable<ChoiceViewModel> row) => (T)row.First(c => c.IsChecked).Value!;

    private static ChoiceViewModel Kind(TimeKind kind, string note) =>
        new(kind, kind.ToString()) { Note = note, Icon = Geometry.Parse(SceneCardViewModel.KindPath(kind)) };

    private static TimeKind KindFor(Intent intent) => intent switch
    {
        Intent.Focus or Intent.Rhythm => TimeKind.Loop,
        Intent.Background => TimeKind.Clock,
        _ => TimeKind.Journey
    };

    private static Ground GroundFor(Intent intent) => intent switch
    {
        Intent.Sleep => Ground.Night,
        Intent.Meditate or Intent.Relax => Ground.Ember,
        Intent.Background => Ground.Sky,
        _ => Ground.Paper
    };

    private static int MinutesFor(Intent intent, TimeKind kind) => (intent, kind) switch
    {
        (_, TimeKind.Loop) => 60,
        (Intent.Sleep, _) => 45,
        (Intent.Meditate, _) => 20,
        _ => 30
    };

    private static Phase[] PhasesFor(Intent intent, TimeKind kind) => (intent, kind) switch
    {
        (_, TimeKind.Clock) => [new("Night", 0, 0.25), new("Morning", 0.25, 0.5), new("Afternoon", 0.5, 0.75), new("Evening", 0.75, 1)],
        (Intent.Focus, TimeKind.Loop) => [new("Work", 0, 5 / 6d), new("Break", 5 / 6d, 1)],
        (Intent.Sleep, TimeKind.Journey) => [new("Settle", 0, 2 / 9d), new("Drift", 2 / 9d, 2 / 3d), new("Sleep", 2 / 3d, 1)],
        (Intent.Meditate, TimeKind.Journey) => [new("Arrive", 0, 0.15), new("Sit", 0.15, 0.85), new("Return", 0.85, 1)],
        _ => []
    };
}
