using CommunityToolkit.Mvvm.ComponentModel;

namespace Soundmote.App.ViewModels;

/// <summary>One step of the preview speed in the dock: ×1, ×10, ×60.</summary>
public sealed partial class SpeedViewModel(double speed) : ObservableObject
{
    public double Speed { get; } = speed;

    public string Label { get; } = $"×{speed:0}";

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
