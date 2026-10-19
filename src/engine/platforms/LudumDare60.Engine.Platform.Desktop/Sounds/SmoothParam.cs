using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform.Desktop.Sounds;

/// <summary>
/// A mixer parameter that changes per sample, like a Web Audio <c>AudioParam</c>: it either glides towards its target
/// exponentially (<c>setTargetAtTime</c>) or ramps to it linearly over a fixed number of samples
/// (<c>linearRampToValueAtTime</c>).
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal struct SmoothParam
{
    private float _step;

    public float Value { get; private set; }

    public float Target { get; private set; }

    /// <summary>
    /// Samples left in the current linear ramp; 0 while gliding.
    /// </summary>
    public int RampLeft { get; private set; }

    /// <summary>
    /// Jumps to <paramref name="value"/>.
    /// </summary>
    public void Set(float value)
    {
        Value = value;
        Target = value;
        RampLeft = 0;
    }

    /// <summary>
    /// Glides to <paramref name="target"/>, cancelling a ramp.
    /// </summary>
    public void Glide(float target)
    {
        Target = target;
        RampLeft = 0;
    }

    /// <summary>
    /// Ramps linearly from the current value to <paramref name="target"/> over <paramref name="samples"/> samples
    /// (at least one).
    /// </summary>
    public void Ramp(float target, int samples)
    {
        samples = Math.Max(1, samples);
        Target = target;
        RampLeft = samples;
        _step = (target - Value) / samples;
    }

    /// <summary>
    /// Advances one sample.
    /// </summary>
    /// <param name="smoothing">How far a glide moves towards the target per sample, in (0, 1].</param>
    /// <returns>The new value.</returns>
    public float Next(float smoothing)
    {
        if (RampLeft > 0)
        {
            Value = --RampLeft == 0 ? Target : Value + _step;
        }
        else if (Value != Target)
        {
            var value = Value + ((Target - Value) * smoothing);

            // Land exactly once the difference is inaudible, so a settled parameter costs nothing.
            Value = Math.Abs(Target - value) < 1e-6f ? Target : value;
        }

        return Value;
    }
}
