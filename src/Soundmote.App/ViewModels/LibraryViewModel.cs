using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>Every scene as a card (D13): the user's own, then Soundmote's, filtered by what they are for.</summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly List<SceneCardViewModel> _all;
    private Intent? _filter;

    public LibraryViewModel(IEnumerable<SceneCardViewModel> cards)
    {
        _all = cards.ToList();
        Refresh();
    }

    /// <summary>"All" and every intent some scene has; an empty filter would be a dead end.</summary>
    public ObservableCollection<IntentChipViewModel> Chips { get; } = [];

    /// <summary>The user's own cards, then the New scene tile.</summary>
    public ObservableCollection<object> Yours { get; } = [];

    public ObservableCollection<SceneCardViewModel> FromSoundmote { get; } = [];

    /// <summary>Every card, whether or not the filter shows it.</summary>
    public IReadOnlyList<SceneCardViewModel> All => _all;

    [ObservableProperty]
    public partial string YoursTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string FromSoundmoteTitle { get; private set; } = "";

    /// <summary>Adds the user's own card in name order.</summary>
    public void Add(SceneCardViewModel card)
    {
        var at = _all.FindIndex(c => !c.IsBuiltIn && string.Compare(c.Name, card.Name, StringComparison.CurrentCultureIgnoreCase) > 0);
        _all.Insert(at < 0 ? _all.Count : at, card);
        Refresh();
    }

    public void Remove(SceneCardViewModel card)
    {
        _all.Remove(card);
        Refresh();
    }

    [RelayCommand]
    private void Filter(IntentChipViewModel chip)
    {
        _filter = chip.Intent;
        Refresh();
    }

    /// <summary>Shows the cards the filter lets through. A filter whose last scene went away falls back to All.</summary>
    public void Refresh()
    {
        var intents = _all.Select(c => c.Scene.Intent).Distinct().Order().ToArray();
        if (_filter is { } f && !intents.Contains(f)) _filter = null;
        Chips.Clear();
        foreach (var chip in intents.Select(i => (Intent?)i).Prepend(null))
            Chips.Add(new IntentChipViewModel(chip) { IsChecked = chip == _filter });

        Fill(Yours, _all.Where(c => !c.IsBuiltIn));
        YoursTitle = $"YOURS · {Yours.Count}";
        Yours.Add(NewSceneTile.Instance);
        Fill(FromSoundmote, _all.Where(c => c.IsBuiltIn));
        FromSoundmoteTitle = $"FROM SOUNDMOTE · {FromSoundmote.Count}";
    }

    private void Fill<T>(ObservableCollection<T> target, IEnumerable<SceneCardViewModel> cards) where T : class
    {
        target.Clear();
        foreach (var card in cards.Where(c => _filter is null || c.Scene.Intent == _filter)) target.Add((T)(object)card);
    }
}

/// <summary>The dashed card after the user's own scenes that opens the New scene sheet.</summary>
public sealed class NewSceneTile
{
    public static NewSceneTile Instance { get; } = new();
}
