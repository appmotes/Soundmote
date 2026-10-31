using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Soundmote.App.ViewModels;

namespace Soundmote.Desktop.Views;

/// <summary>The Export sheet (D24). Export… asks the system's save dialog for a file, then the sheet renders into it.</summary>
public partial class ExportView : UserControl
{
    public ExportView() => InitializeComponent();

    private async void OnExport(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ExportViewModel export || TopLevel.GetTopLevel(this) is not { } top) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = export.Title,
            SuggestedFileName = $"{export.FileName}.{export.Extension}",
            DefaultExtension = export.Extension,
            FileTypeChoices = [new FilePickerFileType(export.Format.ToString().ToUpperInvariant()) { Patterns = [$"*.{export.Extension}"] }],
            ShowOverwritePrompt = true
        });
        if (file?.TryGetLocalPath() is { } path) await export.ExportTo(path);
    }
}
