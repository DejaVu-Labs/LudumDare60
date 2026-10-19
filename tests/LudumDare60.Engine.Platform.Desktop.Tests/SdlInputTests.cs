using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class SdlInputTests
{
    private readonly SdlInput _input = new();

    private RawInputEvent[] Drain(ulong frameTimeNs = 0)
    {
        var events = new RawInputEvent[SdlInput.QueueCapacity];
        var count = _input.Drain(events, frameTimeNs);
        return events[..count];
    }

    private SdlInput Started(int pixelWidth = 1280, int pixelHeight = 720, float density = 1)
    {
        _input.Start(0, pixelWidth, pixelHeight, density, hasKeyboardFocus: true, visible: true);
        Drain();
        return _input;
    }

    private RawInputEvent Single(SDL.Event e)
    {
        _input.Handle(in e);
        return Assert.Single(Drain());
    }

    [Fact]
    public void Start_QueuesResizeThenFocus()
    {
        _input.Start(0, 2560, 1440, 2, hasKeyboardFocus: true, visible: true);

        var events = Drain();

        Assert.Equal(
            [
                new RawInputEvent(RawInputEventKind.Resize, 0, 0, 0, 0, 1280, 720, 2),
                new RawInputEvent(RawInputEventKind.FocusGained, 0, 0, 0, 0, 0, 0, 0),
            ],
            events);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Start_WithoutFocusOrHidden_QueuesFocusLost(bool hasKeyboardFocus, bool visible)
    {
        _input.Start(0, 100, 100, 1, hasKeyboardFocus, visible);

        Assert.Equal(RawInputEventKind.FocusLost, Drain()[1].Kind);
    }

    [Fact]
    public void SetSize_Unchanged_QueuesNothing()
    {
        Started(1280, 720, 1).SetSize(5, 1280, 720, 1);

        Assert.Empty(Drain());
    }

    [Fact]
    public void SetSize_FractionalDensity_LogicalSizeTimesDensityGivesPixels()
    {
        Started().SetSize(0, 1601, 901, 1.25f);

        var resize = Assert.Single(Drain());

        Assert.Equal(1601, (int)MathF.Round(resize.X * resize.Z));
        Assert.Equal(901, (int)MathF.Round(resize.Y * resize.Z));
    }

    [Fact]
    public void SetSize_DensityNotPositive_CountsAsOne()
    {
        Started().SetSize(0, 800, 600, 0);

        Assert.Equal(new RawInputEvent(RawInputEventKind.Resize, 0, 0, 0, 0, 800, 600, 1), Assert.Single(Drain()));
    }

    [Fact]
    public void Handle_KeyDownAndUp_QueuesKeyByScancode()
    {
        Started();
        _input.Handle(SdlEvents.Key(SDL.Scancode.W, down: true));
        _input.Handle(SdlEvents.Key(SDL.Scancode.W, down: false));

        Assert.Equal(
            [
                new RawInputEvent(RawInputEventKind.KeyDown, 0, (ushort)Key.W, 0, 0, 0, 0, 0),
                new RawInputEvent(RawInputEventKind.KeyUp, 0, (ushort)Key.W, 0, 0, 0, 0, 0),
            ],
            Drain());
    }

    [Fact]
    public void Handle_KeyRepeat_IsDropped()
    {
        Started();
        _input.Handle(SdlEvents.Key(SDL.Scancode.Space, down: true, repeat: true));

        Assert.Empty(Drain());
    }

    [Fact]
    public void Handle_KeyOutsideKeyEnum_IsDropped()
    {
        Started();
        _input.Handle(SdlEvents.Key(SDL.Scancode.Mute, down: true));

        Assert.Empty(Drain());
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    public void Handle_MouseButton_NumbersButtonsLikeTheBrowser(byte sdlButton, byte expected)
    {
        Started();

        var e = Single(SdlEvents.MouseButton(sdlButton, down: true, 10.5f, 20));

        Assert.Equal(
            new RawInputEvent(RawInputEventKind.PointerDown, expected, (ushort)PointerType.Mouse, SdlInput.MousePointerId, 0, 10.5f, 20, 0),
            e);
    }

    [Fact]
    public void Handle_MouseButtonUp_QueuesPointerUp()
    {
        Started();

        Assert.Equal(RawInputEventKind.PointerUp, Single(SdlEvents.MouseButton(1, down: false, 0, 0)).Kind);
    }

    [Fact]
    public void Handle_MouseMotion_QueuesPointerMove()
    {
        Started();

        Assert.Equal(
            new RawInputEvent(RawInputEventKind.PointerMove, 0, (ushort)PointerType.Mouse, SdlInput.MousePointerId, 0, 3, 4, 0),
            Single(SdlEvents.MouseMotion(3, 4)));
    }

    [Fact]
    public void Handle_MouseEventsMadeUpFromTouch_AreDropped()
    {
        Started();
        _input.Handle(SdlEvents.MouseButton(1, down: true, 0, 0, which: SDL.TouchMouseID));
        _input.Handle(SdlEvents.MouseMotion(1, 1, which: SDL.TouchMouseID));
        _input.Handle(SdlEvents.Wheel(0, 1, which: SDL.TouchMouseID));

        Assert.Empty(Drain());
    }

    [Fact]
    public void PenMouseId_IsSdlPenMouseId()
        => Assert.Equal(0xFFFF_FFFEu, SdlInput.PenMouseId);

    [Fact]
    public void Handle_MouseEventsFromPen_AreAPenPointer()
    {
        Started();

        var e = Single(SdlEvents.MouseButton(1, down: true, 5, 6, which: SdlInput.PenMouseId));

        Assert.Equal((ushort)PointerType.Pen, e.Code);
        Assert.Equal(SdlInput.PenPointerId, e.Id);
    }

    [Theory]
    [InlineData(false, 0, 1, 0, -SdlInput.WheelLinePixels)]
    [InlineData(false, 0, -2, 0, 2 * SdlInput.WheelLinePixels)]
    [InlineData(false, 1, 0, SdlInput.WheelLinePixels, 0)]
    [InlineData(true, 0, 1, 0, SdlInput.WheelLinePixels)]
    public void Handle_Wheel_ConvertsNotchesToBrowserPixels(bool flipped, float x, float y, float expectedX, float expectedY)
    {
        Started();

        var e = Single(SdlEvents.Wheel(x, y, flipped));

        Assert.Equal(RawInputEventKind.Wheel, e.Kind);
        Assert.Equal(expectedX, e.X);
        Assert.Equal(expectedY, e.Y);
    }

    [Fact]
    public void Handle_Finger_ScalesToLogicalPixelsAndReusesIds()
    {
        Started(2560, 1440, 2);
        _input.Handle(SdlEvents.Finger(SDL.EventType.FingerDown, 100, 0.5f, 0.25f));
        _input.Handle(SdlEvents.Finger(SDL.EventType.FingerDown, 200, 0, 0));
        _input.Handle(SdlEvents.Finger(SDL.EventType.FingerMotion, 100, 1, 1));
        _input.Handle(SdlEvents.Finger(SDL.EventType.FingerUp, 100, 1, 1));
        _input.Handle(SdlEvents.Finger(SDL.EventType.FingerDown, 300, 0, 0));
        _input.Handle(SdlEvents.Finger(SDL.EventType.FingerCanceled, 200, 0, 0));

        var touch = (ushort)PointerType.Touch;
        var first = SdlInput.FirstFingerPointerId;
        Assert.Equal(
            [
                new RawInputEvent(RawInputEventKind.PointerDown, 0, touch, first, 0, 640, 180, 0),
                new RawInputEvent(RawInputEventKind.PointerDown, 0, touch, first + 1, 0, 0, 0, 0),
                new RawInputEvent(RawInputEventKind.PointerMove, 0, touch, first, 0, 1280, 720, 0),
                new RawInputEvent(RawInputEventKind.PointerUp, 0, touch, first, 0, 1280, 720, 0),
                new RawInputEvent(RawInputEventKind.PointerDown, 0, touch, first, 0, 0, 0, 0),
                new RawInputEvent(RawInputEventKind.PointerCancel, 0, touch, first + 1, 0, 0, 0, 0),
            ],
            Drain());
    }

    [Fact]
    public void Handle_FingerMotionWithoutDown_IsDropped()
    {
        Started();
        _input.Handle(SdlEvents.Finger(SDL.EventType.FingerMotion, 100, 0.5f, 0.5f));

        Assert.Empty(Drain());
    }

    [Fact]
    public void Handle_FingerMadeUpFromMouse_IsDropped()
    {
        Started();
        _input.Handle(SdlEvents.Finger(SDL.EventType.FingerDown, 1, 0.5f, 0.5f, touchId: SDL.MouseTouchID));

        Assert.Empty(Drain());
    }

    [Fact]
    public void Handle_FocusAndVisibility_QueueOnlyChangesOfTheCombinedState()
    {
        Started();
        _input.Handle(SdlEvents.Window(SDL.EventType.WindowFocusLost));
        _input.Handle(SdlEvents.Window(SDL.EventType.WindowMinimized));
        _input.Handle(SdlEvents.Window(SDL.EventType.WindowFocusGained));
        _input.Handle(SdlEvents.Window(SDL.EventType.WindowRestored));
        _input.Handle(SdlEvents.Window(SDL.EventType.WindowMinimized));

        Assert.Equal(
            [RawInputEventKind.FocusLost, RawInputEventKind.FocusGained, RawInputEventKind.FocusLost],
            Drain().Select(static e => e.Kind));
    }

    [Fact]
    public void Handle_Gamepads_GetTheLowestFreeIndex()
    {
        Started();
        _input.Handle(SdlEvents.Gamepad(SDL.EventType.GamepadAdded, 10));
        _input.Handle(SdlEvents.Gamepad(SDL.EventType.GamepadAdded, 11));
        _input.Handle(SdlEvents.Gamepad(SDL.EventType.GamepadRemoved, 10));
        _input.Handle(SdlEvents.Gamepad(SDL.EventType.GamepadAdded, 12));

        Assert.Equal(
            [
                new RawInputEvent(RawInputEventKind.GamepadConnected, 1, 0, 0, 0, 0, 0, 0),
                new RawInputEvent(RawInputEventKind.GamepadConnected, 1, 0, 1, 0, 0, 0, 0),
                new RawInputEvent(RawInputEventKind.GamepadDisconnected, 0, 0, 0, 0, 0, 0, 0),
                new RawInputEvent(RawInputEventKind.GamepadConnected, 1, 0, 0, 0, 0, 0, 0),
            ],
            Drain());
    }

    [Theory]
    [InlineData(SDL.GamepadButton.South, 0)]
    [InlineData(SDL.GamepadButton.North, 3)]
    [InlineData(SDL.GamepadButton.LeftShoulder, 4)]
    [InlineData(SDL.GamepadButton.Back, 8)]
    [InlineData(SDL.GamepadButton.Start, 9)]
    [InlineData(SDL.GamepadButton.DPadRight, 15)]
    [InlineData(SDL.GamepadButton.Guide, 16)]
    public void Handle_GamepadButton_UsesStandardLayoutIndex(SDL.GamepadButton button, ushort expected)
    {
        Started();
        _input.Handle(SdlEvents.Gamepad(SDL.EventType.GamepadAdded, 10));
        Drain();

        var e = Single(SdlEvents.GamepadButton(10, button, down: true));

        Assert.Equal(new RawInputEvent(RawInputEventKind.GamepadButton, 1, expected, 0, 0, 1, 0, 0), e);
    }

    [Fact]
    public void Handle_GamepadButtonOutsideStandardLayout_IsDropped()
    {
        Started();
        _input.Handle(SdlEvents.Gamepad(SDL.EventType.GamepadAdded, 10));
        Drain();
        _input.Handle(SdlEvents.GamepadButton(10, SDL.GamepadButton.Misc1, down: true));

        Assert.Empty(Drain());
    }

    [Fact]
    public void Handle_GamepadOfUnknownDevice_IsDropped()
    {
        Started();
        _input.Handle(SdlEvents.GamepadButton(99, SDL.GamepadButton.South, down: true));

        Assert.Empty(Drain());
    }

    [Fact]
    public void Handle_GamepadAxis_ScalesAndSkipsSmallChanges()
    {
        Started();
        _input.Handle(SdlEvents.Gamepad(SDL.EventType.GamepadAdded, 10));
        Drain();
        _input.Handle(SdlEvents.GamepadAxis(10, SDL.GamepadAxis.LeftY, short.MinValue));
        _input.Handle(SdlEvents.GamepadAxis(10, SDL.GamepadAxis.LeftY, short.MinValue + 10));
        _input.Handle(SdlEvents.GamepadAxis(10, SDL.GamepadAxis.RightX, short.MaxValue));

        Assert.Equal(
            [
                new RawInputEvent(RawInputEventKind.GamepadAxis, 0, 1, 0, 0, -1, 0, 0),
                new RawInputEvent(RawInputEventKind.GamepadAxis, 0, 2, 0, 0, 1, 0, 0),
            ],
            Drain());
    }

    [Fact]
    public void Handle_GamepadTrigger_IsAnAnalogStandardButton()
    {
        Started();
        _input.Handle(SdlEvents.Gamepad(SDL.EventType.GamepadAdded, 10));
        Drain();
        _input.Handle(SdlEvents.GamepadAxis(10, SDL.GamepadAxis.LeftTrigger, 1000));
        _input.Handle(SdlEvents.GamepadAxis(10, SDL.GamepadAxis.RightTrigger, short.MaxValue));

        var events = Drain();

        Assert.Equal(2, events.Length);
        Assert.Equal((RawInputEventKind.GamepadButton, (byte)0, (ushort)SdlInput.LeftTriggerButton), (events[0].Kind, events[0].Detail, events[0].Code));
        Assert.Equal(1000 / 32767f, events[0].X);
        Assert.Equal(new RawInputEvent(RawInputEventKind.GamepadButton, 1, SdlInput.RightTriggerButton, 0, 0, 1, 0, 0), events[1]);
    }

    [Fact]
    public void Drain_SetsTimeRelativeToFrameStart()
    {
        Started();
        _input.Handle(SdlEvents.Key(SDL.Scancode.A, down: true, timeNs: 1_000_000_000));
        _input.Handle(SdlEvents.Key(SDL.Scancode.A, down: false, timeNs: 1_016_000_000));

        var events = Drain(frameTimeNs: 1_020_000_000);

        Assert.Equal(-0.020f, events[0].Time, 1e-6f);
        Assert.Equal(-0.004f, events[1].Time, 1e-6f);
    }
}
