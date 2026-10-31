using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Soundmote.App.Formatting;
using Soundmote.Core.Output;
using Soundmote.Core.Scenes;

namespace Soundmote.App.ViewModels;

/// <summary>
/// The Export sheet (D24): a format, how much of the scene, then a file from the system's save dialog. The scene is
/// rendered off the UI thread by a renderer of its own (A17), with progress and Cancel.
/// </summary>
public sealed partial class ExportViewModel : ObservableObject
{
    private readonly Scene _scene;
    private readonly IAudioFiles _files;
    private readonly Action _close;
    private readonly Action<string>? _reveal;
    private CancellationTokenSource? _cancel;
    private string _path = "";

    /// <param name="close">Takes the sheet away.</param>
    /// <param name="reveal">Shows the written file in the system's file browser.</param>
    /// <param name="clock">For a clock scene, the hour it starts from is this clock's.</param>
    public ExportViewModel(Scene scene, IAudioFiles files, Action close, Action<string>? reveal = null, TimeProvider? clock = null)
    {
        _scene = scene;
        _files = files;
        _close = close;
        _reveal = reveal;
        Formats = files.Formats.Select(f => new ChoiceViewModel(f, f.ToString().ToUpperInvariant())
        {
            Note = f == AudioFormat.M4a ? "AAC at 192 kbps · plays everywhere" : "Lossless · ten times larger"
        }).ToArray();
        Lengths = scene.Kind switch
        {
            TimeKind.Loop => new[] { 1, 2, 4 }.Select(n => new ChoiceViewModel(scene.Length * n, n == 1 ? "1 lap" : $"{n} laps")).ToArray(),
            TimeKind.Clock => new[] { 1, 2, 4 }.Select(n => new ChoiceViewModel(TimeSpan.FromHours(n), $"{n} h")).ToArray(),
            _ => []
        };
        StartOfDay = TimeSpan.FromHours((clock ?? TimeProvider.System).GetLocalNow().Hour);
        Formats[0].IsChecked = true;
        if (Lengths.Count > 0) Lengths[0].IsChecked = true;
    }

    public string Title => $"Export {_scene.Name}";

    public IReadOnlyList<ChoiceViewModel> Formats { get; }

    /// <summary>Laps for a loop, hours for a clock scene; none for a journey, which is exported whole.</summary>
    public IReadOnlyList<ChoiceViewModel> Lengths { get; }

    public bool HasLengths => Lengths.Count > 0;

    public bool IsClock => _scene.Kind == TimeKind.Clock;

    public AudioFormat Format => (AudioFormat)Formats.Single(f => f.IsChecked).Value!;

    public TimeSpan Length => Lengths.FirstOrDefault(l => l.IsChecked)?.Value as TimeSpan? ?? _scene.Length;

    /// <summary>Where a clock scene's file starts in the day, on the hour.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartText))]
    public partial TimeSpan StartOfDay { get; private set; }

    public string StartText => SceneText.Clock24(StartOfDay);

    /// <summary>"AAC at 192 kbps · plays everywhere", or why WAV is the only choice here.</summary>
    public string FormatNote => Formats.Count == 1 ? "This system has no AAC encoder, so the file is WAV: lossless and large."
        : Formats.Single(f => f.IsChecked).Note;

    /// <summary>"The whole journey, 45 min", "2 laps, 2 h 0 min", "From 22:00 for 1 h".</summary>
    public string LengthText => _scene.Kind switch
    {
        TimeKind.Journey => $"The whole journey, {Duration(Length)}",
        TimeKind.Loop => $"{Duration(Length)} in all",
        _ => $"From {StartText} to {SceneText.Clock24(StartOfDay + Length)}"
    };

    /// <summary>"About 65 MB · fades out over the last 5 s".</summary>
    public string SizeText
    {
        get
        {
            var bytes = Format == AudioFormat.Wav ? Length.TotalSeconds * SceneExport.SampleRate * 4 : Length.TotalSeconds * 24_000;
            var size = bytes >= 1e9 ? $"{bytes / 1e9:0.0} GB" : $"{Math.Max(1, bytes / 1e6):0} MB";
            return $"About {size} · fades out over the last {SceneExport.FadeOutSeconds:0} s";
        }
    }

    /// <summary>The scene's name as a file name, without the extension.</summary>
    public string FileName => string.Concat(_scene.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or ':' ? '-' : c));

    public string Extension => Format == AudioFormat.M4a ? "m4a" : "wav";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsRendering { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsDone { get; private set; }

    /// <summary>Choosing a format and length and pressing Export; also after a cancel or a failure.</summary>
    public bool IsIdle => !IsRendering && !IsDone;

    /// <summary>How far the render is, 0 to 1.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>"34 % · about 2 min left".</summary>
    [ObservableProperty]
    public partial string ProgressText { get; private set; } = "";

    /// <summary>What happened last: "Saved Fall Asleep.m4a", "Cancelled", or why it failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; private set; } = "";

    public bool HasMessage => Message.Length > 0;

    public string RevealText => ShellViewModel.RevealText;

    [RelayCommand]
    private void ChooseFormat(ChoiceViewModel choice)
    {
        if (!IsIdle) return;
        foreach (var f in Formats) f.IsChecked = f == choice;
        OnPropertyChanged(nameof(Format));
        OnPropertyChanged(nameof(FormatNote));
        OnPropertyChanged(nameof(Extension));
        OnPropertyChanged(nameof(SizeText));
    }

    [RelayCommand]
    private void ChooseLength(ChoiceViewModel choice)
    {
        if (!IsIdle) return;
        foreach (var l in Lengths) l.IsChecked = l == choice;
        OnPropertyChanged(nameof(Length));
        OnPropertyChanged(nameof(LengthText));
        OnPropertyChanged(nameof(SizeText));
    }

    [RelayCommand]
    private void Earlier() => MoveStart(-1);

    [RelayCommand]
    private void Later() => MoveStart(1);

    private void MoveStart(int hours)
    {
        if (!IsIdle) return;
        StartOfDay = TimeSpan.FromHours(((int)StartOfDay.TotalHours + hours + 24) % 24);
        OnPropertyChanged(nameof(LengthText));
    }

    /// <summary>
    /// Renders the scene into <paramref name="path"/>, chosen in the save dialog. A cancelled or failed file is
    /// deleted, and the sheet stays for another try.
    /// </summary>
    public async Task ExportTo(string path)
    {
        if (IsRendering) return;
        using var cancel = _cancel = new CancellationTokenSource();
        var (scene, length, start, format, files) = (_scene, Length, StartOfDay, Format, _files);
        var watch = Stopwatch.StartNew();
        var progress = new Progress<double>(p => Show(p, watch.Elapsed));
        IsRendering = true;
        Progress = 0;
        ProgressText = "Starting";
        Message = "";
        try
        {
            await Task.Run(() =>
            {
                using var file = files.Create(format, path, SceneExport.SampleRate);
                SceneExport.Render(scene, length, file, start, progress, cancel.Token);
            }, cancel.Token);
            _path = path;
            Message = $"Saved {Path.GetFileName(path)}";
            IsDone = true;
        }
        catch (OperationCanceledException)
        {
            Delete(path);
            Message = "Cancelled";
        }
        catch (Exception e)
        {
            Delete(path);
            Message = $"Could not export: {e.Message}";
        }
        finally
        {
            _cancel = null;
            IsRendering = false;
        }
    }

    private void Show(double progress, TimeSpan elapsed)
    {
        if (!IsRendering) return;
        Progress = progress;
        var left = progress > 0.02 ? elapsed * ((1 - progress) / progress) : (TimeSpan?)null;
        ProgressText = left is not { } l ? $"{progress:0 %}"
            : l.TotalSeconds < 60 ? $"{progress:0 %} · under a minute left"
            : $"{progress:0 %} · about {Math.Ceiling(l.TotalMinutes):0} min left";
    }

    /// <summary>Stops a render that is running; otherwise closes the sheet. Escape does the same.</summary>
    [RelayCommand]
    private void Cancel()
    {
        if (_cancel is { } running) running.Cancel();
        else _close();
    }

    [RelayCommand]
    private void Close()
    {
        _cancel?.Cancel();
        _close();
    }

    [RelayCommand]
    private void Reveal()
    {
        if (_path.Length > 0) _reveal?.Invoke(_path);
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>"45 min", "2 h", "3 h 30 min".</summary>
    private static string Duration(TimeSpan t) => t.TotalHours < 1 ? $"{t.TotalMinutes:0} min"
        : t.Minutes == 0 ? $"{(int)t.TotalHours} h" : $"{(int)t.TotalHours} h {t.Minutes} min";
}
