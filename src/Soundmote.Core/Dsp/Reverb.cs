namespace Soundmote.Core.Dsp;

/// <summary>
/// A room shared by every layer (A4): an eight-line feedback delay network with a Hadamard mix and damping in
/// each loop. Even lines feed the left output and odd lines the right, so the tail is wide.
/// </summary>
public sealed class Reverb
{
    private const int Lines = 8;
    private static readonly int[] LengthsAt48K = [2203, 2437, 2711, 2953, 3229, 3511, 3779, 4093];

    private readonly float[][] _lines = new float[Lines][];
    private readonly int[] _pos = new int[Lines];
    private readonly float[] _feedback = new float[Lines];
    private readonly float[] _damp = new float[Lines];
    private readonly float[] _y = new float[Lines];
    private readonly float[] _preDelay;
    private readonly float _dampCoeff;
    private OnePole _inputHighPass;
    private int _prePos;

    /// <param name="seconds">Time for the tail to fall 60 dB.</param>
    /// <param name="damping">0 keeps the highs; 1 makes the tail dark quickly.</param>
    public Reverb(int sampleRate, float seconds = 3.5f, float damping = 0.4f, float preDelaySeconds = 0.025f)
    {
        var scale = sampleRate / 48000f;
        for (var i = 0; i < Lines; i++)
        {
            var length = Math.Max(1, (int)(LengthsAt48K[i] * scale));
            _lines[i] = new float[length];
            _feedback[i] = MathF.Pow(10f, -3f * length / (sampleRate * seconds));
        }
        _preDelay = new float[Math.Max(1, (int)(preDelaySeconds * sampleRate))];
        _dampCoeff = 1f - Math.Clamp(damping, 0f, 0.95f);
        _inputHighPass.SetCutoff(180f, sampleRate);
    }

    public void Process(ReadOnlySpan<float> inLeft, ReadOnlySpan<float> inRight, Span<float> outLeft, Span<float> outRight)
    {
        var y = _y;
        for (var n = 0; n < inLeft.Length; n++)
        {
            var dry = _preDelay[_prePos];
            _preDelay[_prePos] = _inputHighPass.High((inLeft[n] + inRight[n]) * 0.5f);
            if (++_prePos == _preDelay.Length) _prePos = 0;

            for (var i = 0; i < Lines; i++)
            {
                var sample = _lines[i][_pos[i]];
                _damp[i] += _dampCoeff * (sample - _damp[i]);
                y[i] = _damp[i];
            }

            outLeft[n] = (y[0] + y[2] + y[4] + y[6]) * 0.5f;
            outRight[n] = (y[1] + y[3] + y[5] + y[7]) * 0.5f;

            Hadamard(y);
            for (var i = 0; i < Lines; i++)
            {
                var input = (i & 1) == 0 ? dry : -dry;
                _lines[i][_pos[i]] = input + y[i] * _feedback[i];
                if (++_pos[i] == _lines[i].Length) _pos[i] = 0;
            }
        }
    }

    /// <summary>Fast Walsh–Hadamard transform, scaled to keep energy (orthonormal).</summary>
    private static void Hadamard(float[] v)
    {
        for (var h = 1; h < Lines; h <<= 1)
        {
            for (var i = 0; i < Lines; i += h << 1)
            {
                for (var j = i; j < i + h; j++)
                {
                    var a = v[j];
                    var b = v[j + h];
                    v[j] = a + b;
                    v[j + h] = a - b;
                }
            }
        }
        const float norm = 0.35355339f; // 1 / sqrt(8)
        for (var i = 0; i < Lines; i++) v[i] *= norm;
    }
}
