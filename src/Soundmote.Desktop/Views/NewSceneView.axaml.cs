using Avalonia;
using Avalonia.Controls;

namespace Soundmote.Desktop.Views;

public partial class NewSceneView : UserControl
{
    public NewSceneView() => InitializeComponent();

    /// <summary>The name is selected when the sheet opens, so typing replaces it.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsVisibleProperty && change.Property != DataContextProperty) return;
        if (!IsEffectivelyVisible || DataContext is null) return;
        NameBox.Focus();
        NameBox.SelectAll();
    }
}
