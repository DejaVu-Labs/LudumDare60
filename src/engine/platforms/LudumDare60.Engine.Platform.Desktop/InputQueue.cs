namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// A ring buffer of raw input events with their SDL timestamps. The desktop twin of <c>createInputQueue</c> in the
/// browser platform's <c>input.js</c>: when it is full, events are dropped and one
/// <see cref="RawInputEventKind.Overflow"/> event takes their place.
/// </summary>
internal sealed class InputQueue
{
    private readonly RawInputEvent[] _ring;
    private readonly ulong[] _times;
    private int _head;
    private int _dropped;
    private ulong _droppedTimeNs;

    /// <param name="capacity">Events that fit, at least 2.</param>
    public InputQueue(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 2);
        _ring = new RawInputEvent[capacity];
        _times = new ulong[capacity];
    }

    /// <summary>
    /// Events waiting, not counting a pending <see cref="RawInputEventKind.Overflow"/>.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Appends an event.
    /// </summary>
    /// <param name="kind">The event's <see cref="RawInputEvent.Kind"/>.</param>
    /// <param name="detail">The event's <see cref="RawInputEvent.Detail"/>.</param>
    /// <param name="code">The event's <see cref="RawInputEvent.Code"/>.</param>
    /// <param name="id">The event's <see cref="RawInputEvent.Id"/>.</param>
    /// <param name="timeNs">When it happened, on the <c>SDL_GetTicksNS</c> timeline.</param>
    /// <param name="x">The event's <see cref="RawInputEvent.X"/>.</param>
    /// <param name="y">The event's <see cref="RawInputEvent.Y"/>.</param>
    /// <param name="z">The event's <see cref="RawInputEvent.Z"/>.</param>
    public void Push(RawInputEventKind kind, byte detail, ushort code, int id, ulong timeNs, float x, float y, float z)
    {
        if (_dropped > 0)
        {
            // The Overflow event and this one both need a slot; otherwise this one joins the dropped run.
            if (_ring.Length - Count < 2)
            {
                _dropped++;
                return;
            }

            WriteOverflow();
        }

        if (Count == _ring.Length)
        {
            _dropped = 1;
            _droppedTimeNs = timeNs;
            return;
        }

        Write(new RawInputEvent(kind, detail, code, id, 0, x, y, z), timeNs);
    }

    /// <summary>
    /// Moves up to <c>target.Length</c> events into <paramref name="target"/>, oldest first, with
    /// <see cref="RawInputEvent.Time"/> set to seconds relative to <paramref name="frameTimeNs"/> (never positive).
    /// Events that do not fit stay queued.
    /// </summary>
    /// <returns>Number of events written.</returns>
    public int Drain(Span<RawInputEvent> target, ulong frameTimeNs)
    {
        var written = 0;

        // Two rounds: once the first one empties the ring, a pending Overflow goes in behind the events before it.
        for (var round = 0; round < 2 && written < target.Length; round++)
        {
            if (round == 1 && _dropped > 0)
            {
                WriteOverflow();
            }

            while (Count > 0 && written < target.Length)
            {
                var e = _ring[_head];
                var time = (float)Math.Min(0, (double)(long)(_times[_head] - frameTimeNs) / 1e9);
                target[written++] = new RawInputEvent(e.Kind, e.Detail, e.Code, e.Id, time, e.X, e.Y, e.Z);
                _head = (_head + 1) % _ring.Length;
                Count--;
            }
        }

        return written;
    }

    private void WriteOverflow()
    {
        Write(new RawInputEvent(RawInputEventKind.Overflow, 0, 0, _dropped, 0, 0, 0, 0), _droppedTimeNs);
        _dropped = 0;
    }

    private void Write(RawInputEvent e, ulong timeNs)
    {
        var slot = (_head + Count) % _ring.Length;
        _ring[slot] = e;
        _times[slot] = timeNs;
        Count++;
    }
}
