using Avalonia.Headless.XUnit;
using Soundmote.App.ViewModels;
using Soundmote.Core.Output;
using Soundmote.Core.Scenes;

namespace Soundmote.App.Tests;

public sealed class ExportTests : IDisposable
{
    private readonly SceneStore _store = new(Path.Combine(Path.GetTempPath(), "soundmote-tests", Guid.NewGuid().ToString("N")));
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "soundmote-export-tests", Guid.NewGuid().ToString("N"));
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 28, 22, 40, 0, TimeSpan.Zero));

    public ExportTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_store.Folder)) Directory.Delete(_store.Folder, recursive: true);
        Directory.Delete(_folder, recursive: true);
    }

    private ShellViewModel Create(params AudioFormat[] formats) =>
        new(BuiltInScenes.All(), _store, () => new FakeOutput(), _clock, files: new TestFiles(formats.Length > 0 ? formats : [AudioFormat.M4a, AudioFormat.Wav]));

    private static ExportViewModel Open(ShellViewModel shell, string name)
    {
        shell.ExportSceneCommand.Execute(shell.Library.All.Single(c => c.Name == name));
        return shell.Export!;
    }

    [AvaloniaFact]
    public void A_journey_is_exported_whole_as_m4a_by_default()
    {
        var export = Open(Create(), "Fall Asleep");

        Assert.Equal("Export Fall Asleep", export.Title);
        Assert.Equal(["M4A", "WAV"], export.Formats.Select(f => f.Label));
        Assert.Equal(AudioFormat.M4a, export.Format);
        Assert.False(export.HasLengths);
        Assert.Equal(TimeSpan.FromMinutes(45), export.Length);
        Assert.Equal("The whole journey, 45 min", export.LengthText);
        Assert.Equal("About 65 MB · fades out over the last 5 s", export.SizeText);
        Assert.Equal("Fall Asleep.m4a", $"{export.FileName}.{export.Extension}");
    }

    [AvaloniaFact]
    public void Wav_is_larger_and_named_so()
    {
        var export = Open(Create(), "Fall Asleep");

        export.ChooseFormatCommand.Execute(export.Formats[1]);

        Assert.Equal(AudioFormat.Wav, export.Format);
        Assert.Equal("wav", export.Extension);
        Assert.StartsWith("About 518 MB", export.SizeText);
    }

    [AvaloniaFact]
    public void A_system_without_an_aac_encoder_offers_wav_and_says_why()
    {
        var export = Open(Create(AudioFormat.Wav), "Sit");

        Assert.Equal(AudioFormat.Wav, Assert.Single(export.Formats).Value);
        Assert.Contains("no AAC encoder", export.FormatNote);
    }

    [AvaloniaFact]
    public void A_loop_is_exported_in_laps()
    {
        var export = Open(Create(), "Deep Work");

        Assert.Equal(["1 lap", "2 laps", "4 laps"], export.Lengths.Select(l => l.Label));
        export.ChooseLengthCommand.Execute(export.Lengths[1]);

        Assert.Equal(TimeSpan.FromHours(2), export.Length);
        Assert.Equal("2 h in all", export.LengthText);
    }

    [AvaloniaFact]
    public void A_clock_scene_is_exported_from_an_hour_of_the_day()
    {
        var export = Open(Create(), "All Day");

        Assert.Equal(["1 h", "2 h", "4 h"], export.Lengths.Select(l => l.Label));
        Assert.Equal("From 22:00 to 23:00", export.LengthText);
        export.LaterCommand.Execute(null);
        export.LaterCommand.Execute(null);

        Assert.Equal(TimeSpan.Zero, export.StartOfDay);
        Assert.Equal("From 00:00 to 01:00", export.LengthText);
    }

    [AvaloniaFact]
    public async Task Exporting_writes_the_file_and_says_where()
    {
        var shell = Create();
        shell.Library.All.Single(c => c.Name == "Sit").Scene = BuiltInScenes.Sit() with { Length = TimeSpan.FromSeconds(3) };
        var export = Open(shell, "Sit");
        var path = Path.Combine(_folder, "Sit.m4a");

        await export.ExportTo(path);

        Assert.True(export.IsDone);
        Assert.False(export.IsRendering);
        Assert.Equal("Saved Sit.m4a", export.Message);
        Assert.Equal(44 + 3 * SceneExport.SampleRate * 4, new FileInfo(path).Length);
        export.CloseCommand.Execute(null);
        Assert.False(shell.IsExportOpen);
    }

    [AvaloniaFact]
    public async Task Cancelling_stops_the_render_deletes_the_file_and_keeps_the_sheet()
    {
        var shell = Create();
        var export = Open(shell, "Fall Asleep");
        var path = Path.Combine(_folder, "Fall Asleep.m4a");

        var running = export.ExportTo(path);
        Assert.True(export.IsRendering);
        export.CancelCommand.Execute(null);
        await running;

        Assert.False(File.Exists(path));
        Assert.Equal("Cancelled", export.Message);
        Assert.True(export.IsIdle);
        Assert.True(shell.IsExportOpen);
        export.CancelCommand.Execute(null);
        Assert.False(shell.IsExportOpen);
    }

    [AvaloniaFact]
    public async Task A_file_that_cannot_be_written_is_said_and_the_sheet_stays()
    {
        var export = Open(Create(), "Sit");

        await export.ExportTo(Path.Combine(_folder, "missing", "Sit.m4a"));

        Assert.True(export.IsIdle);
        Assert.StartsWith("Could not export:", export.Message);
    }

    [AvaloniaFact]
    public void Without_files_there_is_no_export()
    {
        var shell = new ShellViewModel(BuiltInScenes.All(), _store, () => new FakeOutput(), _clock);

        shell.ExportSceneCommand.Execute(shell.Library.All[0]);

        Assert.False(shell.IsExportOpen);
    }

    /// <summary>Every format is written as WAV, so the flow can be tested on any system.</summary>
    private sealed class TestFiles(AudioFormat[] formats) : IAudioFiles
    {
        public IReadOnlyList<AudioFormat> Formats => formats;

        public IAudioFileWriter Create(AudioFormat format, string path, int sampleRate) => new WavWriter(path, sampleRate);
    }
}
