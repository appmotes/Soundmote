using CommunityToolkit.Mvvm.ComponentModel;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>A filter above the library: one intent, or every scene when <see cref="Intent"/> is null.</summary>
public sealed partial class IntentChipViewModel(Intent? intent) : ObservableObject
{
    public Intent? Intent { get; } = intent;

    public string Label => Intent?.ToString() ?? "All";

    [ObservableProperty]
    public partial bool IsChecked { get; set; }
}
