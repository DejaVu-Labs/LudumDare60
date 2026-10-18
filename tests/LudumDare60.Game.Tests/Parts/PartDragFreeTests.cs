using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Parts;

public sealed class PartDragFreeTests
{
    private const float Dt = 1f / 60;

    // Pulled out to the left, like a base pin: seated at x = 100, out at x = 60, then loose on a 300x200 surface.
    private static readonly LinePath s_path = new(new Vector2(100, 50), new Vector2(60, 50));
    private static readonly Vector2 s_min = Vector2.Zero;
    private static readonly Vector2 s_max = new(300, 200);

    private readonly PartDrag _part = new(s_path, 6, DragFeel.Default, PartStop.Start, new FreeMove(s_min, s_max, DockRadius: 5, DockSpan: 40));

    private static PointerSample Press(Vector2 at) => new(at, IsDown: true, IsPressed: true);

    private static PointerSample Hold(Vector2 at) => new(at, IsDown: true, IsPressed: false);

    private static PointerSample Release(Vector2 at) => new(at, IsDown: false, IsPressed: false);

    /// <summary>
    /// Steps holding the pointer at <paramref name="at"/>, and returns every event seen.
    /// </summary>
    private static DragEvents HoldAt(PartDrag part, Vector2 at, int steps, bool canDock = true)
    {
        var seen = DragEvents.None;
        for (var i = 0; i < steps; i++)
        {
            seen |= part.Step(Hold(at), Dt, canGrab: true, canDock);
        }

        return seen;
    }

    /// <summary>
    /// Pulls the seated part out to the end of its rail; it stays in the hand.
    /// </summary>
    private DragEvents PullOut()
    {
        var seen = _part.Step(Press(s_path.Start), Dt);
        return seen | HoldAt(_part, s_path.End, 120);
    }

    /// <summary>
    /// Pulls the part out, carries it to <paramref name="at"/> and lets go.
    /// </summary>
    private void PutDownAt(Vector2 at)
    {
        PullOut();
        HoldAt(_part, at, 60);
        _part.Step(Release(at), Dt);
    }

    [Fact]
    public void Step_PulledToTheEnd_ClicksOutAndStaysInTheHand()
    {
        var events = PullOut();

        Assert.True((events & DragEvents.SnappedEnd) != DragEvents.None);
        Assert.True(_part.IsFree);
        Assert.True(_part.IsHeld);
        Assert.Equal(PartStop.End, _part.Stop);
    }

    [Fact]
    public void Step_Loose_FollowsTheHandAnywhereOnTheSurface()
    {
        PullOut();

        HoldAt(_part, new Vector2(150, 120), 60);
        var carried = _part.Position;
        HoldAt(_part, new Vector2(500, -40), 60);

        Assert.Equal(150, carried.X, 0.1f);
        Assert.Equal(120, carried.Y, 0.1f);
        Assert.Equal(300, _part.Position.X, 0.01f);
        Assert.Equal(0, _part.Position.Y, 0.01f);
    }

    [Fact]
    public void Step_LetGoLoose_LiesWhereItWasPutDown()
    {
        PullOut();
        HoldAt(_part, new Vector2(150, 120), 60);
        var there = _part.Position;

        var events = _part.Step(Release(new Vector2(150, 120)), Dt);
        HoldAt(_part, new Vector2(20, 20), 1);

        Assert.Equal(DragEvents.Dropped, events);
        Assert.True(_part.IsFree);
        Assert.False(_part.IsHeld);
        Assert.Equal(there, _part.Position);
    }

    [Fact]
    public void Step_PutDownLoose_IsPickedUpWhereItLies()
    {
        PutDownAt(new Vector2(150, 120));

        var events = _part.Step(Press(new Vector2(152, 121)), Dt);
        HoldAt(_part, new Vector2(202, 141), 60);

        Assert.Equal(DragEvents.Grabbed, events);
        Assert.Equal(200, _part.Position.X, 0.2f);
        Assert.Equal(140, _part.Position.Y, 0.2f);
    }

    [Fact]
    public void Step_BroughtBackNearTheRail_SticksAndPushesIn()
    {
        PutDownAt(new Vector2(150, 120));
        _part.Step(Press(_part.Position), Dt);

        var docking = HoldAt(_part, new Vector2(70, 53), 60);
        var pushing = HoldAt(_part, new Vector2(140, 50), 120);

        Assert.True((docking & DragEvents.Docked) != DragEvents.None);
        Assert.True((pushing & DragEvents.SnappedStart) != DragEvents.None);
        Assert.False(_part.IsFree);
        Assert.Equal(PartStop.Start, _part.Stop);
    }

    [Fact]
    public void Step_DockingRefused_ReportsOncePerApproachAndStaysLoose()
    {
        PutDownAt(new Vector2(150, 120));
        _part.Step(Press(_part.Position), Dt, canGrab: true, canDock: false);

        var first = HoldAt(_part, new Vector2(70, 52), 60, canDock: false);
        var away = HoldAt(_part, new Vector2(150, 120), 60, canDock: false);
        var again = HoldAt(_part, new Vector2(70, 52), 60, canDock: false);

        Assert.Equal(DragEvents.DockRefused, first);
        Assert.Equal(DragEvents.None, away);
        Assert.Equal(DragEvents.DockRefused, again);
        Assert.True(_part.IsFree);
    }

    [Fact]
    public void Step_JustPulledOut_DoesNotStickBackUntilItHasLeftTheRail()
    {
        PullOut();

        var lingering = HoldAt(_part, s_path.End, 30);
        var stillLoose = _part.IsFree;
        var away = HoldAt(_part, s_path.End + new Vector2(0, 30), 60);
        var back = HoldAt(_part, s_path.End + new Vector2(0, 2), 60);

        Assert.Equal(DragEvents.None, lingering);
        Assert.True(stillLoose);
        Assert.Equal(DragEvents.None, away);
        Assert.True((back & DragEvents.Docked) != DragEvents.None);
    }

    [Fact]
    public void Step_DockedAtTheEndPulledSideways_ComesLooseAgain()
    {
        PutDownAt(s_path.End + new Vector2(0, 40));
        _part.Step(Press(_part.Position), Dt);
        HoldAt(_part, s_path.End, 60);
        Assert.False(_part.IsFree);

        HoldAt(_part, s_path.End + new Vector2(0, 30), 60);

        Assert.True(_part.IsFree);
        Assert.Equal(s_path.End.Y + 30, _part.Position.Y, 0.5f);
    }

    [Fact]
    public void Step_SeatedPulledSideways_StaysOnItsRail()
    {
        _part.Step(Press(s_path.Start), Dt);

        HoldAt(_part, s_path.Start + new Vector2(0, 40), 60);

        Assert.False(_part.IsFree);
        Assert.Equal(PartStop.Start, _part.Stop);
    }

    [Fact]
    public void Step_TipFirst_SticksOnlyAtTheMouthOfTheRail()
    {
        var pin = new PartDrag(s_path, 6, DragFeel.Default, PartStop.End, new FreeMove(s_min, s_max, DockRadius: 3, DockSpan: 3));
        pin.Step(Press(s_path.End), Dt);
        HoldAt(pin, s_path.End + new Vector2(-30, 30), 60);
        Assert.True(pin.IsFree);

        var midRail = HoldAt(pin, new Vector2(80, 50), 60);
        var mouth = HoldAt(pin, s_path.End + new Vector2(-1, 1), 60);

        Assert.True((midRail & DragEvents.Docked) == DragEvents.None, "Brought onto the middle of the rail, it does not stick.");
        Assert.True((mouth & DragEvents.Docked) != DragEvents.None);
    }

    [Fact]
    public void Step_SeatsOnDock_NoRailBackOnlyItsSeat()
    {
        // A cylinder: out after a short pull, back only by being brought to its seat.
        var shortPath = new LinePath(new Vector2(100, 50), new Vector2(100, 60));
        var cylinder = new PartDrag(shortPath, 8, DragFeel.Default, PartStop.Start, new FreeMove(s_min, s_max, DockRadius: 6, DockSpan: 0, SeatsOnDock: true));
        cylinder.Step(Press(shortPath.Start), Dt);
        var outEvents = HoldAt(cylinder, new Vector2(100, 120), 60);
        Assert.True((outEvents & DragEvents.SnappedEnd) != DragEvents.None);
        Assert.True(cylinder.IsFree);
        Assert.Equal(120, cylinder.Position.Y, 0.5f);

        // Along where the rail was: no sticking.
        var near = HoldAt(cylinder, new Vector2(101, 64), 60);
        Assert.Equal(DragEvents.None, near);
        Assert.True(cylinder.IsFree);

        var seat = HoldAt(cylinder, new Vector2(103, 53), 60);

        Assert.True((seat & DragEvents.SnappedStart) != DragEvents.None);
        Assert.False(cylinder.IsFree);
        Assert.False(cylinder.IsHeld);
        Assert.Equal(PartStop.Start, cylinder.Stop);
        Assert.Equal(shortPath.Start, cylinder.Position);
    }

    [Fact]
    public void Step_SeatsOnDockRefused_StaysLooseAtTheSeat()
    {
        var shortPath = new LinePath(new Vector2(100, 50), new Vector2(100, 60));
        var cylinder = new PartDrag(shortPath, 8, DragFeel.Default, PartStop.Start, new FreeMove(s_min, s_max, DockRadius: 6, DockSpan: 0, SeatsOnDock: true));
        cylinder.Step(Press(shortPath.Start), Dt);
        HoldAt(cylinder, new Vector2(100, 120), 60);

        var refused = HoldAt(cylinder, shortPath.Start, 60, canDock: false);

        Assert.Equal(DragEvents.DockRefused, refused);
        Assert.True(cylinder.IsFree);
    }

    [Fact]
    public void GetRenderPosition_Loose_InterpolatesTheLastStep()
    {
        PullOut();
        HoldAt(_part, new Vector2(150, 120), 1);

        var from = _part.PreviousPosition;
        var to = _part.Position;

        Assert.Equal(Vector2.Lerp(from, to, 0.5f), _part.GetRenderPosition(0.5f));
    }

    [Fact]
    public void ReturnTo_LoosePart_PutsItBackOnItsRail()
    {
        PutDownAt(new Vector2(150, 120));

        _part.ReturnTo(PartStop.Start);

        Assert.False(_part.IsFree);
        Assert.Equal(s_path.Start, _part.Position);
    }
}
