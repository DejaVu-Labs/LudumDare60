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

    private Vector2 SpurAtRest => _test.Hammer.Path.Start;

    /// <summary>
    /// A point beyond full cock along the spur's way back.
    /// </summary>
    private Vector2 PastFullCock => _test.Hammer.Path.End + (_test.Hammer.Path.Direction * 10);

    /// <summary>
    /// Presses at <paramref name="from"/>, holds at <paramref name="to"/> for 60 steps and lets go; returns every event.
    /// </summary>
    private TestEvents Drag(Vector2 from, Vector2 to)
    {
        _test.Step(Press(from), Dt);
        var seen = _test.Events;
        for (var i = 0; i < 60; i++)
        {
            _test.Step(Hold(to), Dt);
            seen |= _test.Events;
        }

        _test.Step(Release(to), Dt);
        return seen | _test.Events;
    }

    private TestEvents Click(Vector2 at)
    {
        _test.Step(Press(at), Dt);
        var seen = _test.Events;
        _test.Step(Release(at), Dt);
        return seen | _test.Events;
    }

    [Fact]
    public void Constructor_HammerDownAtItsSpur()
    {
        Assert.False(_test.IsCocked);
        Assert.False(_test.IsFired);
        Assert.Equal(0, _test.GetHammerAngle(1));
        Assert.Equal(BenchLayout.HammerPivot + PlaceholderParts.HammerSpur, SpurAtRest);
    }

    [Fact]
    public void Constructor_SpurPathIsTheChordOfTheCockingArc()
    {
        var cocked = _test.Hammer.Path.End - BenchLayout.HammerPivot;

        Assert.Equal(PlaceholderParts.HammerSpur.Length(), cocked.Length(), 0.01f);
        Assert.True(cocked.X > PlaceholderParts.HammerSpur.X, "Cocking takes the spur back, towards the grip.");
    }

    [Fact]
    public void Step_PullTheHammerBack_ClicksAtHalfCockThenCocksAndTurnsTheCylinder()
    {
        var events = Drag(SpurAtRest, PastFullCock);

        Assert.Equal(TestEvents.HalfCock | TestEvents.Cocked, events);
        Assert.True(_test.IsCocked);
        Assert.Equal(1, _test.Chamber);
        Assert.Equal(ActionTest.CockAngle, _test.GetHammerAngle(0));
    }

    [Fact]
    public void Step_HammerLetGoBeforeFullCock_SpringsBackDown()
    {
        var halfway = Vector2.Lerp(SpurAtRest, _test.Hammer.Path.End, 0.6f);

        var events = Drag(SpurAtRest, halfway);

        Assert.True((events & TestEvents.HammerEased) != TestEvents.None);
        Assert.Equal(PartStop.Start, _test.Hammer.Stop);
        Assert.Equal(0, _test.GetHammerAngle(1));
        Assert.Equal(0, _test.Chamber);
    }

    [Fact]
    public void Step_TriggerWithTheHammerDown_DoesNothing()
    {
        var events = Click(ActionTest.TriggerCenter);

        Assert.Equal(TestEvents.TriggerRefused, events);
        Assert.False(_test.IsFired);
        Assert.Equal(0, _test.TriggerPull);
    }

    [Fact]
    public void Step_TriggerWhenCocked_DropsTheHammerWithADryFire()
    {
        Drag(SpurAtRest, PastFullCock);

        _test.Step(Press(ActionTest.TriggerCenter), Dt);

        Assert.Equal(TestEvents.DryFire, _test.Events);
        Assert.True(_test.IsFired);
        Assert.False(_test.IsCocked);
        Assert.Equal(0, _test.GetHammerAngle(0));
        Assert.Equal(1, _test.TriggerPull);
    }

    [Fact]
    public void Step_AfterFiring_TheTriggerReturns()
    {
        Drag(SpurAtRest, PastFullCock);
        Click(ActionTest.TriggerCenter);

        for (var i = 0; i < 30; i++)
        {
            _test.Step(Release(Vector2.Zero), Dt);
        }

        Assert.Equal(0, _test.TriggerPull);
    }

    [Fact]
    public void Step_CockedHammer_StaysCockedWhenPushed()
    {
        Drag(SpurAtRest, PastFullCock);

        Drag(_test.Hammer.Path.End, SpurAtRest);

        Assert.True(_test.IsCocked);
    }

    [Fact]
    public void Step_PointerOverTheTrigger_IsHovered()
    {
        _test.Step(Release(ActionTest.TriggerCenter + new Vector2(2, 2)), Dt);

        Assert.True(_test.IsTriggerHovered);
    }

    [Fact]
    public void Step_TwoCocks_TurnTheCylinderTwoChambers()
    {
        Drag(SpurAtRest, PastFullCock);
        Click(ActionTest.TriggerCenter);

        Drag(SpurAtRest, PastFullCock);

        Assert.Equal(2, _test.Chamber);
    }
}
