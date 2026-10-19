namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class InputQueueTests
{
    private static void PushKey(InputQueue queue, int id, ulong timeNs = 0)
        => queue.Push(RawInputEventKind.KeyDown, 0, (ushort)Key.A, id, timeNs, 0, 0, 0);

    private static RawInputEvent[] Drain(InputQueue queue, int capacity = 16, ulong frameTimeNs = 0)
    {
        var events = new RawInputEvent[capacity];
        return events[..queue.Drain(events, frameTimeNs)];
    }

    [Fact]
    public void Drain_ReturnsEventsOldestFirstAndEmptiesTheQueue()
    {
        var queue = new InputQueue(4);
        PushKey(queue, 1);
        PushKey(queue, 2);

        Assert.Equal([1, 2], Drain(queue).Select(static e => e.Id));
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Drain_TargetTooSmall_KeepsTheRestQueued()
    {
        var queue = new InputQueue(4);
        PushKey(queue, 1);
        PushKey(queue, 2);
        PushKey(queue, 3);

        Assert.Equal([1, 2], Drain(queue, capacity: 2).Select(static e => e.Id));
        Assert.Equal([3], Drain(queue).Select(static e => e.Id));
    }

    [Fact]
    public void Drain_EventAfterFrameStart_HasTimeZero()
    {
        var queue = new InputQueue(4);
        PushKey(queue, 1, timeNs: 2_000);

        Assert.Equal(0, Drain(queue, frameTimeNs: 1_000)[0].Time);
    }

    [Fact]
    public void Push_Full_ReplacesDroppedEventsWithOneOverflow()
    {
        var queue = new InputQueue(3);
        PushKey(queue, 1);
        PushKey(queue, 2);
        PushKey(queue, 3);
        PushKey(queue, 4);
        PushKey(queue, 5);

        var events = Drain(queue);

        Assert.Equal(
            [(RawInputEventKind.KeyDown, 1), (RawInputEventKind.KeyDown, 2), (RawInputEventKind.KeyDown, 3), (RawInputEventKind.Overflow, 2)],
            events.Select(static e => (e.Kind, e.Id)));
    }

    [Fact]
    public void Push_AfterOverflowWithRoom_QueuesOverflowBeforeTheNewEvent()
    {
        var queue = new InputQueue(3);
        PushKey(queue, 1);
        PushKey(queue, 2);
        PushKey(queue, 3);
        PushKey(queue, 4);
        Drain(queue, capacity: 2);
        PushKey(queue, 5);

        Assert.Equal(
            [(RawInputEventKind.KeyDown, 3), (RawInputEventKind.Overflow, 1), (RawInputEventKind.KeyDown, 5)],
            Drain(queue).Select(static e => (e.Kind, e.Id)));
    }

    [Fact]
    public void Constructor_CapacityBelowTwo_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new InputQueue(1));
}
