using System.Numerics;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Bench;

public sealed class ActionTestTests
{
    private const float Dt = 1f / 60;

    private readonly ActionTest _test = new();

    private static PointerSample Press(Vector2 at) => new(at, IsDown: true, IsPressed: true);

    private static PointerSample Hold(Vector2 at) => new(at, IsDown: true, IsPressed: false);

    private static PointerSample Release(Vector2 at) => new(at, IsDown: false, IsPressed: false);

    /// <summary>
    /// A point beyond full cock along the spur's way back.
    /// </summary>
    private Vector2 PastFullCock => _test.SpurCocked + (_test.CockDirection * 10);

    /// <summary>
    /// Takes the spur where it is, holds the pointer at <paramref name="to"/> for 60 steps and lets go; returns every
    /// event.
    /// </summary>
    private TestEvents Drag(Vector2 to, bool canCock)
    {
        _test.Step(Press(_test.Spur), Dt, canCock, canGrab: true);
        var seen = _test.Events;
        for (var i = 0; i < 60; i++)
        {
            _test.Step(Hold(to), Dt, canCock, canGrab: true);
            seen |= _test.Events;
        }

        _test.Step(Release(to), Dt, canCock, canGrab: true);
        return seen | _test.Events;
    }

    private TestEvents ClickTrigger()
    {
        _test.Step(Press(ActionTest.TriggerCenter), Dt, canCock: true, canGrab: true);
        var seen = _test.Events;
        _test.Step(Release(ActionTest.TriggerCenter), Dt, canCock: true, canGrab: true);
        return seen | _test.Events;
    }

    [Fact]
    public void Step_PressOnTheBottomOfTheFingerPiece_HoversAndTriesTheTrigger()
    {
        // The lowest drawn pixel of the trigger: its hook, far from the pivot.
        var hook = BenchLayout.TriggerPivot - PlaceholderParts.TriggerOrigin + new Vector2(2.5f, PlaceholderParts.Trigger.Height - 1.5f);

        _test.Step(Press(hook), Dt, canCock: true, canGrab: true);

        Assert.True(_test.IsTriggerHovered);
        Assert.Equal(TestEvents.TriggerRefused, _test.Events);
    }

    [Fact]
    public void Step_PressOnTheHammerScrew_DoesNotTakeTheHammer()
    {
        _test.Step(Press(BenchLayout.HammerScrewSeated), Dt, canCock: true, canGrab: true);

        Assert.False(_test.IsHammerHeld);
    }

    [Fact]
    public void Constructor_HammerDown()
    {
        Assert.Equal(HammerState.Down, _test.State);
        Assert.False(_test.IsOpen);
        Assert.False(_test.IsFired);
        Assert.Equal(0, _test.GetHammerAngle(1));
        Assert.Equal(BenchLayout.HammerPivot + PlaceholderParts.HammerSpur, _test.Spur);
    }

    [Fact]
    public void Constructor_SpurPathIsTheChordOfTheCockingArc()
    {
        var cocked = _test.SpurCocked - BenchLayout.HammerPivot;

        Assert.Equal(PlaceholderParts.HammerSpur.Length(), cocked.Length(), 0.01f);
        Assert.True(cocked.X > PlaceholderParts.HammerSpur.X, "Cocking takes the spur back, towards the grip.");
    }

    [Fact]
    public void Step_PullBackWhileRepairing_StopsInTheHalfCockNotchAndStays()
    {
        var events = Drag(PastFullCock, canCock: false);

        Assert.Equal(TestEvents.HalfCock, events);
        Assert.Equal(HammerState.HalfCock, _test.State);
        Assert.True(_test.IsOpen);
        Assert.Equal(ActionTest.HalfCock, _test.Share);
        Assert.Equal(ActionTest.HalfCock * ActionTest.CockAngle, _test.GetHammerAngle(1), 1e-5f);
        Assert.Equal(0, _test.Chamber);
    }

    [Fact]
    public void Step_LetGoBeforeHalfCock_FallsBackDown()
    {
        var nearRest = Vector2.Lerp(_test.SpurAtRest, _test.SpurCocked, ActionTest.HalfCock / 2);

        var events = Drag(nearRest, canCock: false);

        Assert.Equal(TestEvents.HammerEased, events);
        Assert.Equal(HammerState.Down, _test.State);
        Assert.Equal(0, _test.Share);
    }

    [Fact]
    public void Step_AtHalfCock_CannotBePushedLower()
    {
        Drag(PastFullCock, canCock: false);

        Drag(_test.SpurAtRest - (_test.CockDirection * 10), canCock: false);

        Assert.Equal(HammerState.HalfCock, _test.State);
        Assert.Equal(ActionTest.HalfCock, _test.Share);
    }

    [Fact]
    public void Step_TestFromHalfCock_ReachesFullCockAndTurnsTheCylinder()
    {
        Drag(PastFullCock, canCock: false);

        var events = Drag(PastFullCock, canCock: true);

        Assert.Equal(TestEvents.Cocked, events);
        Assert.Equal(HammerState.FullCock, _test.State);
        Assert.False(_test.IsHammerHeld);
        Assert.Equal(1, _test.Chamber);
        Assert.Equal(ActionTest.CockAngle, _test.GetHammerAngle(0));
    }

    [Fact]
    public void Step_TestLetGoBeforeFullCock_SettlesBackInTheNotch()
    {
        Drag(PastFullCock, canCock: false);

        Drag(Vector2.Lerp(_test.SpurAtRest, _test.SpurCocked, 0.8f), canCock: true);

        Assert.Equal(HammerState.HalfCock, _test.State);
        Assert.Equal(ActionTest.HalfCock, _test.Share);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Step_TriggerBelowFullCock_DoesNothing(bool halfCocked)
    {
        if (halfCocked)
        {
            Drag(PastFullCock, canCock: false);
        }

        var events = ClickTrigger();

        Assert.Equal(TestEvents.TriggerRefused, events);
        Assert.False(_test.IsFired);
        Assert.Equal(0, _test.TriggerPull);
    }

    [Fact]
    public void Step_TriggerAtFullCock_DropsTheHammerWithADryFire()
    {
        Drag(PastFullCock, canCock: false);
        Drag(PastFullCock, canCock: true);

        _test.Step(Press(ActionTest.TriggerCenter), Dt, canCock: true, canGrab: true);

        Assert.Equal(TestEvents.DryFire, _test.Events);
        Assert.True(_test.IsFired);
        Assert.Equal(HammerState.Down, _test.State);
        Assert.Equal(0, _test.GetHammerAngle(0));
        Assert.Equal(1, _test.TriggerPull);
    }

    [Fact]
    public void Step_AfterFiring_TheTriggerReturns()
    {
        Drag(PastFullCock, canCock: true);
        ClickTrigger();

        for (var i = 0; i < 30; i++)
        {
            _test.Step(Release(Vector2.Zero), Dt, canCock: true, canGrab: true);
        }

        Assert.Equal(0, _test.TriggerPull);
    }

    [Fact]
    public void Step_FromDownWithTheTestOn_ClicksHalfCockOnTheWayToFullCock()
    {
        var events = Drag(PastFullCock, canCock: true);

        Assert.Equal(TestEvents.HalfCock | TestEvents.Cocked, events);
    }

    [Fact]
    public void Step_FullCock_CannotBeTakenAgain()
    {
        Drag(PastFullCock, canCock: true);

        _test.Step(Press(_test.Spur), Dt, canCock: true, canGrab: true);

        Assert.False(_test.IsHammerHeld);
        Assert.Equal(HammerState.FullCock, _test.State);
    }

    [Fact]
    public void Step_CannotGrab_NeitherHammerNorTrigger()
    {
        _test.Step(Press(_test.Spur), Dt, canCock: false, canGrab: false);
        Assert.False(_test.IsHammerHeld);

        _test.Step(Press(ActionTest.TriggerCenter), Dt, canCock: false, canGrab: false);

        Assert.False(_test.IsTriggerHovered);
        Assert.Equal(TestEvents.None, _test.Events);
    }

    [Fact]
    public void Step_TwoCocks_TurnTheCylinderTwoChambers()
    {
        Drag(PastFullCock, canCock: true);
        ClickTrigger();

        Drag(PastFullCock, canCock: true);

        Assert.Equal(2, _test.Chamber);
    }
}
