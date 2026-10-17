using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Engine.Input;

/// <summary>
/// Feeds a frame's raw input events into two <see cref="InputState"/>s: <see cref="Frame"/> for
/// <see cref="IGame.Update"/>, which gets every event of the frame at once, and <see cref="Fixed"/> for
/// <see cref="IGame.FixedUpdate"/>, which gets each event in the fixed step whose time span contains it.
/// </summary>
/// <remarks>
/// An event's <see cref="RawInputEvent.Time"/> is relative to the start of the frame that delivers it. The fixed step
/// that ends at frame time <c>b</c> gets the queued events with time at most <c>b</c>. Events later than the last step
/// of the frame wait for the next frame's steps, with their times moved back by that frame's delta time. A press thus
/// reaches the step it happened in: never a frame late, never lost in a frame without steps, never seen twice.
/// <para>Events are taken in arrival order: an event waits while an earlier-arrived one waits, so a key's down never
/// overtakes its up.</para>
/// </remarks>
internal sealed class InputRouter
{
    /// <summary>
    /// Events that can wait for a later step. When full, the oldest ones go to <see cref="Fixed"/> early.
    /// </summary>
    public const int PendingCapacity = 4096;

    private readonly RawInputEvent[] _pending = new RawInputEvent[PendingCapacity];
    private readonly double[] _pendingTimes = new double[PendingCapacity];
    private int _head;

    public InputState Frame { get; } = new();

    public InputState Fixed { get; } = new();

    /// <summary>
    /// Events waiting for a fixed step.
    /// </summary>
    public int PendingCount { get; private set; }

    /// <summary>
    /// Starts a run: no events, nothing down.
    /// </summary>
    public void Reset(in PixelViewport viewport)
    {
        _head = 0;
        PendingCount = 0;
        Frame.Reset(in viewport);
        Fixed.Reset(in viewport);
    }

    /// <summary>
    /// Applies the frame's events to <see cref="Frame"/> and queues them for the fixed steps.
    /// </summary>
    /// <param name="events">The frame's events, in arrival order.</param>
    /// <param name="deltaTime">The frame's clamped delta time: how far the times of waiting events move back.</param>
    /// <param name="viewport">The frame's viewport, after its resize events.</param>
    public void BeginFrame(ReadOnlySpan<RawInputEvent> events, double deltaTime, in PixelViewport viewport)
    {
        Frame.ClearEdges();
        foreach (ref readonly var e in events)
        {
            Frame.Apply(in e, in viewport);
        }

        // Compact the queue and rebase the waiting events onto this frame.
        if (_head > 0)
        {
            Array.Copy(_pending, _head, _pending, 0, PendingCount);
            Array.Copy(_pendingTimes, _head, _pendingTimes, 0, PendingCount);
            _head = 0;
        }

        for (var i = 0; i < PendingCount; i++)
        {
            _pendingTimes[i] -= deltaTime;
        }

        foreach (ref readonly var e in events)
        {
            if (PendingCount == PendingCapacity)
            {
                // Full (the game ran no fixed steps for a long time): the oldest event goes in early.
                Fixed.Apply(in _pending[0], in viewport);
                PendingCount--;
                Array.Copy(_pending, 1, _pending, 0, PendingCount);
                Array.Copy(_pendingTimes, 1, _pendingTimes, 0, PendingCount);
            }

            _pending[PendingCount] = e;
            _pendingTimes[PendingCount] = e.Time;
            PendingCount++;
        }
    }

    /// <summary>
    /// Starts a fixed step that ends at frame time <paramref name="end"/>: clears the edges of the previous step and
    /// applies the queued events up to <paramref name="end"/>.
    /// </summary>
    /// <param name="end">The step's end in seconds relative to the frame start, at most 0.</param>
    /// <param name="viewport">The frame's viewport.</param>
    public void BeginFixedStep(double end, in PixelViewport viewport)
    {
        Fixed.ClearEdges();
        while (PendingCount > 0 && _pendingTimes[_head] <= end)
        {
            Fixed.Apply(in _pending[_head], in viewport);
            _head++;
            PendingCount--;
        }
    }
}
