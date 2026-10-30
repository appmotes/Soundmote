using Avalonia.Media;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Soundmote.App.ViewModels;

/// <summary>One option in a row of options where one is chosen: a chip, a card or a swatch.</summary>
public sealed partial class ChoiceViewModel(object? value, string label) : ObservableObject
{
    public object? Value { get; } = value;

    public string Label { get; } = label;

    public string Note { get; init; } = "";

    public Geometry? Icon { get; init; }

    /// <summary>For a ground swatch: the ground it shows.</summary>
    public ThemeVariant? Ground { get; init; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
