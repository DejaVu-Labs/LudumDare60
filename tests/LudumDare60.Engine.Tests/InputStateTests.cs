using System.Numerics;
using LudumDare60.Engine.Input;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Engine.Tests;

public sealed class InputStateTests
{
    /// <summary>
    /// 1400x800 logical pixels at ratio 1: scale 2, with a 60 px bar left and a 40 px bar on top.
    /// </summary>
    private static readonly PixelViewport s_viewport = PixelViewport.Fit(640, 360, 1400, 800, 1);

    private readonly InputState _state = new();

    public InputStateTests() => _state.Reset(in s_viewport);

    internal static RawInputEvent KeyDown(Key key, float time = 0) => new(RawInputEventKind.KeyDown, 0, (ushort)key, 0, time, 0, 0, 0);

    internal static RawInputEvent KeyUp(Key key, float time = 0) => new(RawInputEventKind.KeyUp, 0, (ushort)key, 0, time, 0, 0, 0);

    private static RawInputEvent Pointer(RawInputEventKind kind, float x, float y, PointerButton button = PointerButton.Primary)
        => new(kind, (byte)button, (ushort)PointerType.Mouse, 1, 0, x, y, 0);

    private static RawInputEvent Event(RawInputEventKind kind, float x = 0, float y = 0)
        => new(kind, 0, 0, 0, 0, x, y, 0);

    private void Apply(params RawInputEvent[] events)
    {
        foreach (var e in events)
        {
            _state.Apply(in e, in s_viewport);
        }
    }

    [Fact]
    public void Reset_PointerAtScreenCenterNothingDown()
    {
        Assert.Equal(new Vector2(320, 180), _state.PointerPosition);
        Assert.True(_state.IsPointerOnScreen);
        Assert.False(_state.HasFocus);
        Assert.False(_state.IsDown(Key.A));
    }

    [Fact]
    public void KeyDown_IsDownAndPressedUntilEdgesClear()
    {
        Apply(KeyDown(Key.A));

        Assert.True(_state.IsDown(Key.A));
        Assert.True(_state.IsPressed(Key.A));

        _state.ClearEdges();

        Assert.True(_state.IsDown(Key.A));
        Assert.False(_state.IsPressed(Key.A));
    }

    [Fact]
    public void IsAnyPressed_KeyOrButtonDown_TrueUntilEdgesClear()
    {
        Assert.False(_state.IsAnyPressed);

        Apply(KeyDown(Key.Space));

        Assert.True(_state.IsAnyPressed);

        _state.ClearEdges();

        Assert.False(_state.IsAnyPressed);

        Apply(Pointer(RawInputEventKind.PointerDown, 100, 100));

        Assert.True(_state.IsAnyPressed);
    }

    [Fact]
    public void IsAnyPressed_OnlyReleaseOrMove_False()
    {
        Apply(KeyDown(Key.A));
        _state.ClearEdges();

        Apply(KeyUp(Key.A), Pointer(RawInputEventKind.PointerMove, 100, 100));

        Assert.False(_state.IsAnyPressed);
    }

    [Fact]
    public void KeyDownAndUpInOneStep_PressedAndReleasedButNotDown()
    {
        Apply(KeyDown(Key.A), KeyUp(Key.A));

        Assert.True(_state.IsPressed(Key.A));
        Assert.True(_state.IsReleased(Key.A));
        Assert.False(_state.IsDown(Key.A));
    }

    [Fact]
    public void KeyDownWhileDown_NoNewPressedEdge()
    {
        Apply(KeyDown(Key.A));
        _state.ClearEdges();

        Apply(KeyDown(Key.A));

        Assert.False(_state.IsPressed(Key.A));
    }

    [Fact]
    public void KeyUpWhileUp_NoReleasedEdge()
    {
        Apply(KeyUp(Key.A));

        Assert.False(_state.IsReleased(Key.A));
    }

    [Fact]
    public void KeyOutsideTable_IsIgnored()
    {
        Apply(KeyDown((Key)300));

        Assert.False(_state.IsDown((Key)300));
    }

    [Theory]
    [InlineData(RawInputEventKind.FocusLost)]
    [InlineData(RawInputEventKind.Overflow)]
    public void FocusLostOrOverflow_ReleasesEverythingWithEdges(RawInputEventKind kind)
    {
        Apply(KeyDown(Key.A), Pointer(RawInputEventKind.PointerDown, 100, 100, PointerButton.Secondary));
        _state.ClearEdges();

        Apply(Event(kind));

        Assert.False(_state.IsDown(Key.A));
        Assert.True(_state.IsReleased(Key.A));
        Assert.False(_state.IsDown(PointerButton.Secondary));
        Assert.True(_state.IsReleased(PointerButton.Secondary));
        Assert.False(_state.IsReleased(Key.B));
    }

    [Fact]
    public void Focus_FollowsFocusEvents()
    {
        Apply(Event(RawInputEventKind.FocusGained));
        Assert.True(_state.HasFocus);

        Apply(Event(RawInputEventKind.FocusLost));
        Assert.False(_state.HasFocus);
    }

    [Fact]
    public void PointerDown_ConvertsToVirtualPixelsAndPressesButton()
    {
        Apply(Pointer(RawInputEventKind.PointerDown, 60 + 200, 40 + 100));

        Assert.Equal(new Vector2(100, 50), _state.PointerPosition);
        Assert.True(_state.IsPointerOnScreen);
        Assert.True(_state.IsPressed(PointerButton.Primary));
        Assert.True(_state.IsDown(PointerButton.Primary));
    }

    [Fact]
    public void PointerMove_HighDpi_ConvertsWithPixelRatio()
    {
        // 1024x768 logical at ratio 2: 2048x1536 device pixels, scale 3, bars of 64 and 228 device pixels.
        var viewport = PixelViewport.Fit(640, 360, 1024, 768, 2);
        var e = Pointer(RawInputEventKind.PointerMove, 300, 600);

        _state.Apply(in e, in viewport);

        Assert.Equal((600 - 64) / 3f, _state.PointerPosition.X, 3);
        Assert.Equal((1200 - 228) / 3f, _state.PointerPosition.Y, 3);
    }

    [Fact]
    public void PointerUp_ReleasesOnlyItsButton()
    {
        Apply(
            Pointer(RawInputEventKind.PointerDown, 100, 100, PointerButton.Primary),
            Pointer(RawInputEventKind.PointerDown, 100, 100, PointerButton.Secondary),
            Pointer(RawInputEventKind.PointerUp, 100, 100, PointerButton.Primary));

        Assert.False(_state.IsDown(PointerButton.Primary));
        Assert.True(_state.IsDown(PointerButton.Secondary));
    }

    [Fact]
    public void PointerOnBars_IsOffScreen()
    {
        Apply(Pointer(RawInputEventKind.PointerMove, 10, 10));

        Assert.False(_state.IsPointerOnScreen);
        Assert.True(_state.PointerPosition.X < 0);
    }

    [Fact]
    public void PointerMove_DeltaAccumulatesUntilEdgesClear()
    {
        Apply(Pointer(RawInputEventKind.PointerMove, 60, 40));
        _state.ClearEdges();

        Apply(Pointer(RawInputEventKind.PointerMove, 70, 40), Pointer(RawInputEventKind.PointerMove, 70, 60));

        Assert.Equal(new Vector2(5, 10), _state.PointerDelta);

        _state.ClearEdges();

        Assert.Equal(Vector2.Zero, _state.PointerDelta);
    }

    [Fact]
    public void PointerMotion_AddsScaledDeltaWithoutMoving()
    {
        Apply(Event(RawInputEventKind.PointerMotion, 4, -2));

        Assert.Equal(new Vector2(2, -1), _state.PointerDelta);
        Assert.Equal(new Vector2(320, 180), _state.PointerPosition);
    }

    [Fact]
    public void PointerCancel_ReleasesButtons()
    {
        Apply(Pointer(RawInputEventKind.PointerDown, 100, 100));

        Apply(Event(RawInputEventKind.PointerCancel));

        Assert.False(_state.IsDown(PointerButton.Primary));
        Assert.True(_state.IsReleased(PointerButton.Primary));
    }

    [Fact]
    public void Wheel_AccumulatesUntilEdgesClear()
    {
        Apply(Event(RawInputEventKind.Wheel, 0, 10), Event(RawInputEventKind.Wheel, 1, 5));

        Assert.Equal(new Vector2(1, 15), _state.Wheel);

        _state.ClearEdges();

        Assert.Equal(Vector2.Zero, _state.Wheel);
    }
}
