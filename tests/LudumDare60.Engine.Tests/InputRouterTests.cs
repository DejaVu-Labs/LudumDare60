using LudumDare60.Engine.Input;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Engine.Tests;

public sealed class InputRouterTests
{
    private static readonly PixelViewport s_viewport = PixelViewport.Fit(640, 360, 640, 360, 1);

    private readonly InputRouter _router = new();

    public InputRouterTests() => _router.Reset(in s_viewport);

    [Fact]
    public void BeginFrame_AppliesEveryEventToFrameState()
    {
        _router.BeginFrame([InputStateTests.KeyDown(Key.A, -0.01f)], 0.01, in s_viewport);

        Assert.True(_router.Frame.IsPressed(Key.A));
        Assert.False(_router.Fixed.IsDown(Key.A));
        Assert.Equal(1, _router.PendingCount);
    }

    [Fact]
    public void BeginFixedStep_TakesEventsUpToItsEnd()
    {
        _router.BeginFrame([InputStateTests.KeyDown(Key.A, -0.02f), InputStateTests.KeyDown(Key.B, -0.005f)], 0.03, in s_viewport);

        _router.BeginFixedStep(-0.01, in s_viewport);

        Assert.True(_router.Fixed.IsPressed(Key.A));
        Assert.False(_router.Fixed.IsDown(Key.B));

        _router.BeginFixedStep(0, in s_viewport);

        Assert.False(_router.Fixed.IsPressed(Key.A));
        Assert.True(_router.Fixed.IsPressed(Key.B));
    }

    [Fact]
    public void BeginFixedStep_KeepsArrivalOrder()
    {
        // B arrived first but happened later: A waits behind it.
        _router.BeginFrame([InputStateTests.KeyDown(Key.B, -0.005f), InputStateTests.KeyDown(Key.A, -0.02f)], 0.03, in s_viewport);

        _router.BeginFixedStep(-0.01, in s_viewport);

        Assert.False(_router.Fixed.IsDown(Key.A));
        Assert.Equal(2, _router.PendingCount);
    }

    [Fact]
    public void BeginFrame_WaitingEventsMoveBackByDeltaTime()
    {
        _router.BeginFrame([InputStateTests.KeyDown(Key.A, -0.004f)], 0.01, in s_viewport);

        _router.BeginFrame([], 0.01, in s_viewport);
        _router.BeginFixedStep(-0.015, in s_viewport);

        Assert.False(_router.Fixed.IsDown(Key.A));

        _router.BeginFixedStep(-0.012, in s_viewport);

        Assert.True(_router.Fixed.IsPressed(Key.A));
    }

    [Fact]
    public void BeginFrame_QueueFull_AppliesOldestEarly()
    {
        var events = new RawInputEvent[InputRouter.PendingCapacity + 1];
        events[0] = InputStateTests.KeyDown(Key.A);
        for (var i = 1; i < events.Length; i++)
        {
            events[i] = new RawInputEvent(RawInputEventKind.Wheel, 0, 0, 0, 0, 0, 1, 0);
        }

        _router.BeginFrame(events, 0.01, in s_viewport);

        Assert.True(_router.Fixed.IsDown(Key.A));
        Assert.Equal(InputRouter.PendingCapacity, _router.PendingCount);
    }

    [Fact]
    public void Reset_DropsWaitingEventsAndState()
    {
        _router.BeginFrame([InputStateTests.KeyDown(Key.A)], 0.01, in s_viewport);

        _router.Reset(in s_viewport);

        Assert.Equal(0, _router.PendingCount);
        Assert.False(_router.Frame.IsDown(Key.A));
    }
}
