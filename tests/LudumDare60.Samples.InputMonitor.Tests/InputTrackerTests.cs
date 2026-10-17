using System.Text;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Samples.InputMonitor.Tests;

public sealed class InputTrackerTests
{
    private readonly InputTracker _tracker = new();

    private static RawInputEvent Event(RawInputEventKind kind, byte detail = 0, ushort code = 0, int id = 0, float x = 0, float y = 0, float z = 0)
    {
        return new(kind, detail, code, id, 0, x, y, z);
    }

    private static RawInputEvent Key(RawInputEventKind kind, Key key) => Event(kind, code: (ushort)key);

    private static RawInputEvent Pointer(RawInputEventKind kind, int id, byte button = 0, float x = 0, float y = 0, PointerType type = PointerType.Mouse)
    {
        return Event(kind, button, (ushort)type, id, x, y);
    }

    private void Apply(params RawInputEvent[] events) => _tracker.Apply(events, 0);

    private string Report(int capacity = InputMonitorGame.ReportCapacity)
    {
        var buffer = new byte[capacity];
        return Encoding.UTF8.GetString(buffer, 0, _tracker.WriteReport(buffer));
    }

    [Fact]
    public void Apply_KeyDownThenUp_TracksKeyState()
    {
        Apply(Key(RawInputEventKind.KeyDown, Engine.Platform.Key.W), Key(RawInputEventKind.KeyDown, Engine.Platform.Key.ShiftLeft));
        Apply(Key(RawInputEventKind.KeyUp, Engine.Platform.Key.ShiftLeft));

        Assert.True(_tracker.IsKeyDown(Engine.Platform.Key.W));
        Assert.False(_tracker.IsKeyDown(Engine.Platform.Key.ShiftLeft));
    }

    [Theory]
    [InlineData(RawInputEventKind.FocusLost)]
    [InlineData(RawInputEventKind.Overflow)]
    public void Apply_FocusLostOrOverflow_ReleasesEverything(RawInputEventKind kind)
    {
        Apply(
            Key(RawInputEventKind.KeyDown, Engine.Platform.Key.Space),
            Pointer(RawInputEventKind.PointerDown, 1, button: 2, x: 5, y: 6),
            Event(RawInputEventKind.GamepadConnected, detail: 1),
            Event(RawInputEventKind.GamepadButton, detail: 1, code: 3));

        Apply(Event(kind, id: 4));

        Assert.False(_tracker.IsKeyDown(Engine.Platform.Key.Space));
        Assert.True(_tracker.TryGetPointer(1, out var x, out _, out var buttons));
        Assert.Equal((5f, 0), (x, buttons));
        Assert.False(_tracker.IsGamepadButtonDown(0, 3));
    }

    [Fact]
    public void Apply_Overflow_CountsDroppedEvents()
    {
        Apply(Event(RawInputEventKind.Overflow, id: 3), Event(RawInputEventKind.Overflow, id: 2));

        Assert.Equal(5, _tracker.DroppedEvents);
    }

    [Fact]
    public void Apply_FocusEvents_TrackFocus()
    {
        Apply(Event(RawInputEventKind.FocusGained));
        Assert.True(_tracker.Focused);

        Apply(Event(RawInputEventKind.FocusLost));
        Assert.False(_tracker.Focused);
    }

    [Fact]
    public void Apply_PointerButtons_AreTrackedPerButton()
    {
        Apply(Pointer(RawInputEventKind.PointerDown, 1, button: 0, x: 10, y: 20));
        Apply(Pointer(RawInputEventKind.PointerMove, 1, x: 11, y: 21), Pointer(RawInputEventKind.PointerDown, 1, button: 2, x: 11, y: 21));
        Apply(Pointer(RawInputEventKind.PointerUp, 1, button: 0, x: 12, y: 22));

        Assert.True(_tracker.TryGetPointer(1, out var x, out var y, out var buttons));
        Assert.Equal((12f, 22f, 1 << 2), (x, y, buttons));
    }

    [Fact]
    public void Apply_TouchLifted_ForgetsPointerButMouseStays()
    {
        Apply(Pointer(RawInputEventKind.PointerDown, 7, type: PointerType.Touch), Pointer(RawInputEventKind.PointerUp, 7, type: PointerType.Touch));
        Apply(Pointer(RawInputEventKind.PointerDown, 1), Pointer(RawInputEventKind.PointerUp, 1));

        Assert.False(_tracker.TryGetPointer(7, out _, out _, out _));
        Assert.True(_tracker.TryGetPointer(1, out _, out _, out _));
    }

    [Fact]
    public void Apply_PointerCancel_ForgetsPointer()
    {
        Apply(Pointer(RawInputEventKind.PointerDown, 3, type: PointerType.Touch), Event(RawInputEventKind.PointerCancel, code: 1, id: 3));

        Assert.False(_tracker.TryGetPointer(3, out _, out _, out _));
    }

    [Fact]
    public void Apply_MorePointersThanSlots_IgnoresExtraPointers()
    {
        for (var id = 1; id <= InputTracker.MaxPointers + 2; id++)
        {
            Apply(Pointer(RawInputEventKind.PointerMove, id));
        }

        Assert.True(_tracker.TryGetPointer(InputTracker.MaxPointers, out _, out _, out _));
        Assert.False(_tracker.TryGetPointer(InputTracker.MaxPointers + 1, out _, out _, out _));
    }

    [Fact]
    public void Apply_MotionAndWheel_Accumulate()
    {
        Apply(Event(RawInputEventKind.PointerMotion, x: 3, y: -4), Event(RawInputEventKind.PointerMotion, x: 1, y: 1));
        Apply(Event(RawInputEventKind.Wheel, y: 100), Event(RawInputEventKind.Wheel, x: 2, y: -40));

        Assert.Equal((4f, -3f), (_tracker.MotionX, _tracker.MotionY));
        Assert.Equal((2f, 60f), (_tracker.WheelX, _tracker.WheelY));
    }

    [Fact]
    public void Apply_ResizeAndPointerLock_AreStored()
    {
        Apply(Event(RawInputEventKind.Resize, x: 640, y: 360, z: 2), Event(RawInputEventKind.PointerLockChanged, detail: 1));

        Assert.Equal((640f, 360f, 2f), (_tracker.SurfaceWidth, _tracker.SurfaceHeight, _tracker.PixelRatio));
        Assert.True(_tracker.PointerLocked);
    }

    [Fact]
    public void Apply_Gamepad_TracksButtonsAxesAndDisconnect()
    {
        Apply(
            Event(RawInputEventKind.GamepadConnected, detail: 1, id: 1),
            Event(RawInputEventKind.GamepadButton, detail: 1, code: 0, id: 1, x: 1),
            Event(RawInputEventKind.GamepadAxis, code: 1, id: 1, x: -0.5f));

        Assert.True(_tracker.IsGamepadConnected(1));
        Assert.True(_tracker.IsGamepadButtonDown(1, 0));
        Assert.Equal(-0.5f, _tracker.GetGamepadAxis(1, 1));

        Apply(Event(RawInputEventKind.GamepadDisconnected, id: 1));

        Assert.False(_tracker.IsGamepadConnected(1));
        Assert.False(_tracker.IsGamepadButtonDown(1, 0));
        Assert.Equal(0, _tracker.GetGamepadAxis(1, 1));
    }

    [Fact]
    public void Apply_CountsEventsAndKeepsLastOnesInHistory()
    {
        var events = new RawInputEvent[InputTracker.HistorySize + 3];
        for (var i = 0; i < events.Length; i++)
        {
            events[i] = Event(RawInputEventKind.Wheel, id: i);
        }

        _tracker.Apply(events, 5);
        Apply(Event(RawInputEventKind.FocusGained));

        Assert.Equal(events.Length + 1, _tracker.TotalEvents);
        Assert.Equal(events.Length, _tracker.MaxEventsPerFrame);
        Assert.Equal(InputTracker.HistorySize, _tracker.HistoryCount);
        Assert.Equal(4, _tracker.GetHistory(0).Id);
        Assert.Equal(RawInputEventKind.FocusGained, _tracker.GetHistory(InputTracker.HistorySize - 1).Kind);
    }

    [Fact]
    public void Apply_UnknownKind_IsIgnored()
    {
        Apply(Event((RawInputEventKind)200, id: 1));

        Assert.Equal(1, _tracker.TotalEvents);
    }

    [Fact]
    public void WriteReport_ListsStateAndRecentEvents()
    {
        Apply(
            Event(RawInputEventKind.Resize, x: 640, y: 360, z: 2),
            Event(RawInputEventKind.FocusGained),
            Key(RawInputEventKind.KeyDown, Engine.Platform.Key.W),
            Key(RawInputEventKind.KeyDown, Engine.Platform.Key.ShiftLeft),
            Pointer(RawInputEventKind.PointerDown, 1, button: 2, x: 12.5f, y: 40),
            Event(RawInputEventKind.GamepadConnected, detail: 1),
            Event(RawInputEventKind.GamepadButton, detail: 1, code: 3, x: 1));

        var report = Report();

        Assert.Contains("Focus      yes\n", report, StringComparison.Ordinal);
        Assert.Contains("Surface    640x360 @2x\n", report, StringComparison.Ordinal);
        Assert.Contains("Keys       W ShiftLeft \n", report, StringComparison.Ordinal);
        Assert.Contains("#1 Mouse 12.5, 40 buttons right", report, StringComparison.Ordinal);
        Assert.Contains("#0 standard buttons 3", report, StringComparison.Ordinal);
        Assert.Contains(" PointerDown ", report, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteReport_NothingHappened_ShowsEmptyLists()
    {
        var report = Report();

        Assert.Contains("Keys       -\n", report, StringComparison.Ordinal);
        Assert.Contains("Pointers   -\n", report, StringComparison.Ordinal);
        Assert.Contains("Gamepads   -\n", report, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteReport_BufferTooSmall_CutsOffAtWholePiece()
    {
        var full = Report();

        var cut = Report(capacity: 100);

        Assert.InRange(Encoding.UTF8.GetByteCount(cut), 1, 100);
        Assert.StartsWith(cut, full, StringComparison.Ordinal);
    }
}
