using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Turns one browser frame into the <see cref="IFrameSink"/> calls for that frame: drains input, then updates and
/// renders.
/// </summary>
internal sealed class FrameDispatcher(IFrameSink frameSink, InputDrain drainInput)
{
    /// <summary>
    /// Events read per frame. Events beyond it stay queued in JS and arrive with the next frame.
    /// </summary>
    public const int InputCapacity = 512;

    private readonly RawInputEvent[] _events = new RawInputEvent[InputCapacity];

    /// <param name="dt">Seconds since the previous frame.</param>
    /// <param name="frameTimeMs">Timestamp of this frame, in milliseconds.</param>
    /// <exception cref="InvalidOperationException">The drain reported more events than fit.</exception>
    public void OnFrame(double dt, double frameTimeMs)
    {
        var count = drainInput(MemoryMarshal.AsBytes(_events.AsSpan()), frameTimeMs);
        if ((uint)count > InputCapacity)
        {
            throw new InvalidOperationException("The input drain reported more events than the buffer holds.");
        }

        frameSink.Update(dt, _events.AsSpan(0, count));
        frameSink.Render();
    }
}
