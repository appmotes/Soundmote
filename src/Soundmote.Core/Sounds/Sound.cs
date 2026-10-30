namespace Soundmote.Core.Sounds;

/// <summary>
/// A procedural sound: one ring on the dial. The scene hands it an amount from 0 to 1 per block and the sound
/// decides what "more" means for it: denser rain, faster wind, more voices (A3). Render runs on the audio
/// thread, so it must not allocate, lock or throw (A5). Every sound in the app is a recipe (S10).
/// </summary>
public abstract class Sound(int sampleRate)
{
    public int SampleRate { get; } = sampleRate;

    /// <summary>The ring's Waver knob, 0–1: how much a sound that reads it wavers in pitch (S17). Set before each block.</summary>
    public float Waver { get; set; }

    /// <summary>
    /// Where the scene's beat stands at the start of the next block, in beats since the scene began (A16). The renderer
    /// sets it before each block; a sound alone moves it on itself.
    /// </summary>
    public double Beat { get; set; }

    /// <summary>The scene's tempo in beats a minute (A16).</summary>
    public double Tempo { get; set; } = Scenes.Scene.DefaultTempo;

    /// <summary>Writes (does not add) one block. Glides from the previous amount to <paramref name="amount"/>.</summary>
    public abstract void Render(Span<float> left, Span<float> right, float amount);

    /// <summary>
    /// The sound plays something whole of its own at each strike, such as a struck bowl, instead of the ring
    /// swelling and dying away (A11). The renderer then calls <see cref="Strike"/> and keeps the amount at full.
    /// </summary>
    public virtual bool TakesStrikes => false;

    /// <summary>A strike of the ring, 0 to 1. Called on the audio thread just before a block, so it must not allocate.</summary>
    public virtual void Strike(float strength)
    {
    }

}
