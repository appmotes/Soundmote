using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Controls;
using Soundmote.Core.Recipes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// The add panel (D9, D20): the families first, a family opened into its sounds, both narrowed by a place and a mood;
/// a search finds sounds across every family. Clicking a sound tries it as a ring in the playing scene; "Add as a
/// ring" keeps it, closing the panel drops it.
/// </summary>
public sealed partial class AddSoundViewModel : ObservableObject
{
    private const float TrialLevel = 0.6f;
    private const int GoesWithCount = 4;
    private readonly PlayerViewModel _player;
    private string? _place, _mood;

    internal AddSoundViewModel(PlayerViewModel player)
    {
        _player = player;
        Tiles = RecipeLibrary.All.Select(r => new SoundTileViewModel(r, player.Layers.Any(l => l.Layer.Recipe.Id == r.Id))).ToArray();
        Families = Family.All.Select(f => new FamilyTileViewModel(f, Tiles.Where(t => t.Recipe.Family == f.Id).ToArray())).ToArray();
        Shapes = ShapePreset.All.Select((p, i) => new ShapeChoiceViewModel(p) { IsChecked = i == 0 }).ToArray();
        Shape = Shapes[0].Preset;
        Refresh();
    }

    public string Title => Opened?.Name ?? "Add a sound";

    public string Subtitle => Opened is { } family
        ? $"{family.Sounds.Count} sounds · all mix with each other"
        : $"It becomes a new ring in {_player.Name}";

    public string SearchHint => Opened is { } family ? $"Search {family.Name.ToLowerInvariant()}" : $"Search {Tiles.Count} sounds, places and moods";

    public IReadOnlyList<SoundTileViewModel> Tiles { get; }

    public IReadOnlyList<FamilyTileViewModel> Families { get; }

    public IReadOnlyList<ShapeChoiceViewModel> Shapes { get; }

    /// <summary>The sounds already playing, shown above the families.</summary>
    public IReadOnlyList<LayerViewModel> InScene => _player.Layers;

    public bool HasInScene => Opened is null && InScene.Count > 0;

    /// <summary>The family whose sounds are shown, or none for the families themselves.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Subtitle), nameof(SearchHint), nameof(IsOpen), nameof(HasInScene),
        nameof(ShowsFamilies), nameof(ShowsSounds), nameof(ShowsShapes))]
    public partial FamilyTileViewModel? Opened { get; private set; }

    public bool IsOpen => Opened is not null;

    /// <summary>The families show until one is opened or something is searched for; then the sounds do.</summary>
    public bool ShowsFamilies => Opened is null && Query.Trim().Length == 0;

    public bool ShowsSounds => !ShowsFamilies;

    /// <summary>The starting shapes matter once there are sounds to try, or one is on trial.</summary>
    public bool ShowsShapes => ShowsSounds || Trying is not null;

    public string PlaceText => _place is null ? "Anywhere" : TagChipViewModel.Capital(_place);

    public string MoodText => _mood is null ? "Any mood" : TagChipViewModel.Capital(_mood);

    public bool IsPlaceSet => _place is not null;

    public bool IsMoodSet => _mood is not null;

    /// <summary>The place menu: its chips in a pop-up under its button, closed by a choice or a click elsewhere.</summary>
    [ObservableProperty]
    public partial bool IsPlaceMenuOpen { get; set; }

    [ObservableProperty]
    public partial bool IsMoodMenuOpen { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<FamilyTileViewModel> VisibleFamilies { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<SoundTileViewModel> Visible { get; private set; } = [];

    /// <summary>"Any" and the places the sounds in view have; one is checked.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<TagChipViewModel> Places { get; private set; } = [];

    /// <summary>"Any" and the moods the sounds in view have; one is checked.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<TagChipViewModel> Moods { get; private set; } = [];

    /// <summary>Sounds of other families that share a place or a mood with the one on trial, one per family.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGoesWith))]
    public partial IReadOnlyList<SoundTileViewModel> GoesWith { get; private set; } = [];

    public bool HasGoesWith => GoesWith.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsFamilies), nameof(ShowsSounds), nameof(ShowsShapes))]
    public partial string Query { get; set; } = "";

    [ObservableProperty]
    public partial ShapePreset Shape { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAdd), nameof(TrialName), nameof(ShowsShapes))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial SoundTileViewModel? Trying { get; private set; }

    public bool CanAdd => Trying is not null;

    public string TrialName => Trying?.Name ?? "";

    partial void OnQueryChanged(string value) => Refresh();

    [RelayCommand]
    private void OpenFamily(FamilyTileViewModel family)
    {
        if (family.IsComing) return;
        Opened = family;
        Query = "";
        Refresh();
    }

    /// <summary>Back from a family to all of them.</summary>
    [RelayCommand]
    private void Back()
    {
        Opened = null;
        Refresh();
    }

    [RelayCommand]
    private void TogglePlaceMenu() => IsPlaceMenuOpen = !IsPlaceMenuOpen;

    [RelayCommand]
    private void ToggleMoodMenu() => IsMoodMenuOpen = !IsMoodMenuOpen;

    [RelayCommand]
    private void SetPlace(TagChipViewModel chip)
    {
        _place = chip.Word;
        IsPlaceMenuOpen = false;
        Refresh();
    }

    [RelayCommand]
    private void SetMood(TagChipViewModel chip)
    {
        _mood = chip.Word;
        IsMoodMenuOpen = false;
        Refresh();
    }

    /// <summary>Plays the sound as a dashed ring in the scene, in place of the one tried before.</summary>
    [RelayCommand]
    private void Try(SoundTileViewModel tile)
    {
        foreach (var t in Tiles) t.IsTrying = t == tile;
        Trying = tile;
        GoesWith = Partners(tile);
        _player.Try(tile.Recipe, Shape.Shape(TrialLevel));
    }

    [RelayCommand]
    private void ChooseShape(ShapeChoiceViewModel choice)
    {
        foreach (var c in Shapes) c.IsChecked = c == choice;
        Shape = choice.Preset;
        if (Trying is not null) _player.ReshapeTrial(Shape.Shape(TrialLevel));
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add() => _player.KeepTrial();

    [RelayCommand]
    private void Cancel() => _player.CloseAdd();

    /// <summary>
    /// Rebuilds the chips for the sounds in view (a place or mood no sound in view has is dropped, and with it a choice
    /// of it), then what shows: the families with a sound that matches, or the matching sounds.
    /// </summary>
    private void Refresh()
    {
        IReadOnlyList<SoundTileViewModel> scope = Opened?.Sounds ?? Tiles;
        Places = Chips(Family.Places.Where(p => scope.Any(t => t.Recipe.Places.Contains(p))), ref _place);
        Moods = Chips(Family.Moods.Where(m => scope.Any(t => t.Recipe.Moods.Contains(m))), ref _mood);
        OnPropertyChanged(nameof(PlaceText));
        OnPropertyChanged(nameof(MoodText));
        OnPropertyChanged(nameof(IsPlaceSet));
        OnPropertyChanged(nameof(IsMoodSet));

        var query = Query.Trim();
        var filtered = _place is not null || _mood is not null;
        bool Matches(SoundTileViewModel t) =>
            (_place is null || t.Recipe.Places.Contains(_place))
            && (_mood is null || t.Recipe.Moods.Contains(_mood))
            && (query.Length == 0 || Words(t.Recipe).Any(w => w.Contains(query, StringComparison.OrdinalIgnoreCase)));

        foreach (var family in Families) family.Show(family.Sounds.Where(Matches).ToArray());
        VisibleFamilies = Families.Where(f => f.IsComing ? !filtered : f.Matching > 0).ToArray();
        Visible = scope.Where(Matches).ToArray();
    }

    private static IEnumerable<string> Words(Recipe r) =>
        [r.Name, Family.Get(r.Family).Name, r.Character, .. r.Places, .. r.Moods];

    private static TagChipViewModel[] Chips(IEnumerable<string> words, ref string? chosen)
    {
        var list = words.ToArray();
        if (chosen is not null && !list.Contains(chosen)) chosen = null;
        var on = chosen;
        return [new TagChipViewModel(null) { IsChecked = on is null }, .. list.Select(w => new TagChipViewModel(w) { IsChecked = w == on })];
    }

    private SoundTileViewModel[] Partners(SoundTileViewModel tile)
    {
        var recipe = tile.Recipe;
        return Tiles
            .Where(t => t.Recipe.Family != recipe.Family && !t.InScene)
            .Select(t => (Tile: t, Shared: t.Recipe.Moods.Intersect(recipe.Moods).Count() + t.Recipe.Places.Intersect(recipe.Places).Count()))
            .Where(x => x.Shared > 0)
            .GroupBy(x => x.Tile.Recipe.Family)
            .Select(g => g.MaxBy(x => x.Shared))
            .OrderByDescending(x => x.Shared)
            .Take(GoesWithCount)
            .Select(x => x.Tile)
            .ToArray();
    }
}

/// <summary>One family in the add panel: its glyph, how many of its sounds match, a dot for each in its colour.</summary>
public sealed partial class FamilyTileViewModel(Family family, IReadOnlyList<SoundTileViewModel> sounds) : ObservableObject
{
    private const int MaxDots = 9;

    public Family Family { get; } = family;

    public string Name => Family.Name;

    public IReadOnlyList<SoundTileViewModel> Sounds { get; } = sounds;

    public bool IsComing => Sounds.Count == 0;

    public Color Colour { get; } = Color.Parse(family.Colour);

    public Geometry Icon { get; } = SoundIcons.Get(family.Icon).Data;

    public double IconStroke { get; } = SoundIcons.Get(family.Icon).Stroke;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText), nameof(CountShort))]
    public partial int Matching { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<IBrush> Dots { get; private set; } = [];

    public string CountText => IsComing ? "Coming" : Matching == 1 ? "1 sound" : $"{Matching} sounds";

    /// <summary>The count as the tile shows it, beside the name: a number, or "Soon".</summary>
    public string CountShort => IsComing ? "Soon" : Matching.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal void Show(IReadOnlyList<SoundTileViewModel> matching)
    {
        Matching = matching.Count;
        Dots = matching.Take(MaxDots).Select(t => t.Swatch).ToArray();
    }
}

/// <summary>One sound in the add panel.</summary>
public sealed partial class SoundTileViewModel(Recipe recipe, bool inScene) : ObservableObject
{
    public Recipe Recipe { get; } = recipe;

    public string Name => Recipe.Name;

    public bool InScene { get; } = inScene;

    /// <summary>Where it belongs and how it feels ("Indoors · cosy"), or that it is already playing.</summary>
    public string Sub { get; } = inScene ? "Already in this scene" : Tags(recipe);

    public string Character => Recipe.Character;

    public Color Colour { get; } = Color.Parse(recipe.Colour);

    public IBrush Swatch { get; } = new SolidColorBrush(Color.Parse(recipe.Colour));

    public Geometry Icon { get; } = SoundIcons.For(recipe).Data;

    public double IconStroke { get; } = SoundIcons.For(recipe).Stroke;

    [ObservableProperty]
    public partial bool IsTrying { get; set; }

    private static string Tags(Recipe r)
    {
        var words = r.Places.Concat(r.Moods).ToArray();
        return words.Length == 0 ? r.Character : TagChipViewModel.Capital(string.Join(" · ", words));
    }
}

/// <summary>A place or a mood in the add panel, or "Any"; one in its row is checked.</summary>
public sealed partial class TagChipViewModel(string? word) : ObservableObject
{
    /// <summary>The word in recipes (S14), or null for "Any".</summary>
    public string? Word { get; } = word;

    public string Label { get; } = word is null ? "Any" : Capital(word);

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    internal static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}

/// <summary>A starting shape in the add panel; one is checked.</summary>
public sealed partial class ShapeChoiceViewModel(ShapePreset preset) : ObservableObject
{
    public ShapePreset Preset { get; } = preset;

    public string Name => Preset.Name;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
