using Soundmote.Core.Scenes;

namespace Soundmote.Core.Tests;

public sealed class SceneStoreTests : IDisposable
{
    private readonly SceneStore _store = new(Path.Combine(Path.GetTempPath(), "soundmote-tests", Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        if (Directory.Exists(_store.Folder)) Directory.Delete(_store.Folder, recursive: true);
    }

    [Fact]
    public void Saved_scenes_load_by_name_and_a_deleted_one_is_gone()
    {
        _store.Save(BuiltInScenes.Sit() with { Id = "b", Name = "Wind Down" });
        _store.Save(BuiltInScenes.Sit() with { Id = "a", Name = "Evening Sit" });
        _store.Save(BuiltInScenes.Sit() with { Id = "a", Name = "Afternoon Sit" });

        Assert.Equal(new[] { ("a", "Afternoon Sit"), ("b", "Wind Down") }, _store.Load().Select(s => (s.Id, s.Name)));
        Assert.Empty(Directory.GetFiles(_store.Folder, "*.tmp"));

        _store.Delete("a");
        Assert.Equal("b", Assert.Single(_store.Load()).Id);
    }

    [Fact]
    public void A_file_that_does_not_read_is_skipped_and_named()
    {
        _store.Save(BuiltInScenes.Sit());
        File.WriteAllText(Path.Combine(_store.Folder, "broken.json"), "{");
        var skipped = new List<string>();

        var scenes = _store.Load(skipped);

        Assert.Equal("sit", Assert.Single(scenes).Id);
        Assert.StartsWith("broken.json: Not valid JSON", Assert.Single(skipped));
    }

    [Fact]
    public void New_ids_are_words_from_the_name_and_never_taken()
    {
        _store.Save(BuiltInScenes.Sit() with { Id = "evening-sit" });

        Assert.Equal("evening-sit-2", _store.NewId("Evening Sit", []));
        Assert.Equal("evening-sit-3", _store.NewId("  Evening   sit!", ["evening-sit-2"]));
        Assert.Equal("scene", _store.NewId("☾", []));
    }

    [Fact]
    public void Without_a_folder_there_are_no_scenes() => Assert.Empty(_store.Load());
}
