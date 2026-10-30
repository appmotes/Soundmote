using Soundmote.Core.Output;

namespace Soundmote.App.Tests;

/// <summary>An output with no device: blocks are pulled by the test, as the audio thread would.</summary>
public sealed class FakeOutput : IAudioOutput
{
    private IAudioSource? _source;

    public float Volume { get; set; } = 1f;

    public bool Started => _source is not null;

    public void Start(IAudioSource source) => _source = source;

    public void Stop() => _source = null;

    public void Dispose() => Stop();

    public void Pull(double seconds)
    {
        if (_source is null) return;
        var block = new float[1024];
        for (var frames = (int)(seconds * _source.SampleRate); frames > 0; frames -= 512)
            _source.Render(block);
    }
}
