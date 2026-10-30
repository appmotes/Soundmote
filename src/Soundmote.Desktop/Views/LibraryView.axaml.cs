using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Soundmote.App.ViewModels;
using Soundmote.Core.Scenes;

namespace Soundmote.Desktop.Views;

/// <summary>The library (D13). A card's menu lives in a popup outside the view's tree, so its items act from here.</summary>
public partial class LibraryView : UserControl
{
    public LibraryView() => InitializeComponent();

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    private static SceneCardViewModel Card(object? sender) => (SceneCardViewModel)((Control)sender!).DataContext!;

    private void OnOpen(object? sender, RoutedEventArgs e) => Shell?.OpenCommand.Execute(Card(sender));

    private void OnRename(object? sender, RoutedEventArgs e) => Shell?.StartRenameCommand.Execute(Card(sender));

    private void OnDuplicate(object? sender, RoutedEventArgs e) => Shell?.DuplicateCommand.Execute(Card(sender));

    private void OnReveal(object? sender, RoutedEventArgs e) => Shell?.RevealCommand.Execute(Card(sender));

    private void OnDelete(object? sender, RoutedEventArgs e) => Shell?.DeleteCommand.Execute(Card(sender));

    private void OnReset(object? sender, RoutedEventArgs e) => Shell?.ResetCommand.Execute(Card(sender));

    private void OnGround(object? sender, RoutedEventArgs e) =>
        Shell?.SetGroundCommand.Execute(new GroundChoice(Card(sender), Enum.Parse<Ground>((string)((MenuItem)sender!).Tag!)));

    /// <summary>The … button opens the card's own menu under it.</summary>
    private void OnMore(object? sender, RoutedEventArgs e)
    {
        var button = (Control)sender!;
        button.GetVisualAncestors().OfType<Control>().FirstOrDefault(c => c.ContextMenu is not null)?.ContextMenu!.Open(button);
    }

    private void OnRenameShown(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != IsVisibleProperty || e.NewValue is not true) return;
        var box = (TextBox)sender!;
        box.Focus();
        box.SelectAll();
    }

    /// <summary>Return keeps the name, Escape leaves it as it was.</summary>
    private void OnRenameKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Shell?.CommitRenameCommand.Execute(Card(sender));
        else if (e.Key == Key.Escape) Shell?.CancelRenameCommand.Execute(Card(sender));
        else return;
        e.Handled = true;
    }

    private void OnRenameLeft(object? sender, RoutedEventArgs e) => Shell?.CommitRenameCommand.Execute(Card(sender));
}
