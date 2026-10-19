using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Parts;

public sealed class PartDragTests
{
    private const float Dt = 1f / 60;

    // Pulled out to the left, like a base pin: seated at x = 100, free at x = 60.
    private static readonly LinePath s_path = new(new Vector2(100, 50), new Vector2(60, 50));

    private readonly PartDrag _part = new(s_path, 6, DragFeel.Default);

    private static PointerSample Press(float x, float y = 50) => new(new Vector2(x, y), IsDown: true, IsPressed: true);

    private static PointerSample Hold(float x, float y = 50) => new(new Vector2(x, y), IsDown: true, IsPressed: false);

    private static PointerSample Release(float x) => new(new Vector2(x, 50), IsDown: false, IsPressed: false);

    private static PointerSample Hover(float x, float y = 50) => new(new Vector2(x, y), IsDown: false, IsPressed: false);

    /// <summary>
    /// Steps until an event in <paramref name="until"/> happens, and returns all events seen.
    /// </summary>
    private static DragEvents StepUntil(PartDrag part, PointerSample pointer, DragEvents until, int maxSteps = 600, bool canGrab = true)
    {
        var seen = DragEvents.None;
        for (var i = 0; i < maxSteps && (seen & until) == DragEvents.None; i++)
        {
            seen |= part.Step(pointer, Dt, canGrab);
            Assert.InRange(part.Distance, 0, s_path.Length);
        }

        return seen;
    }

    /// <summary>
    /// Grabs the seated part at its position and pulls it far enough to come loose.
    /// </summary>
    private void GrabAndBreakFree()
    {
        _part.Step(Press(100), Dt);
        Assert.Equal(DragEvents.BrokeFree, _part.Step(Hold(90), Dt));
    }

    [Fact]
    public void Constructor_Default_StartsSeatedAndLetGo()
    {
        Assert.Equal(PartStop.Start, _part.Stop);
        Assert.Equal(0, _part.Distance);
        Assert.Equal(new Vector2(100, 50), _part.Position);
        Assert.False(_part.IsHeld);
    }

    [Fact]
    public void Constructor_AtEnd_StartsPulledOut()
    {
        var part = new PartDrag(s_path, 6, DragFeel.Default, PartStop.End);

        Assert.Equal(PartStop.End, part.Stop);
        Assert.Equal(new Vector2(60, 50), part.Position);
    }

    [Fact]
    public void Constructor_LooseStop_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PartDrag(s_path, 6, DragFeel.Default, PartStop.None));

    [Fact]
    public void Constructor_SnapZonesTouch_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PartDrag(s_path, 6, new DragFeel { SnapDistance = 20 }));

    [Fact]
    public void Constructor_StrainAboveOne_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PartDrag(s_path, 6, new DragFeel { Strain = 1.5f }));

    [Fact]
    public void Constructor_NegativeGrabRadius_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new PartDrag(s_path, -1, DragFeel.Default));

    [Fact]
    public void Step_PointerWithinGrabRadius_IsHovered()
    {
        _part.Step(Hover(104, 53), Dt);

        Assert.True(_part.IsHovered);
    }

    [Fact]
    public void Step_PointerOutsideGrabRadius_IsNotHovered()
    {
        _part.Step(Hover(107), Dt);

        Assert.False(_part.IsHovered);
    }

    [Fact]
    public void Step_PressOnPart_GrabsIt()
    {
        var events = _part.Step(Press(102), Dt);

        Assert.Equal(DragEvents.Grabbed, events);
        Assert.True(_part.IsHeld);
        Assert.False(_part.IsHovered);
    }

    [Fact]
    public void Step_CannotGrab_NeitherHoversNorGrabs()
    {
        var events = _part.Step(Press(100), Dt, canGrab: false);

        Assert.Equal(DragEvents.None, events);
        Assert.False(_part.IsHovered);
        Assert.False(_part.IsHeld);
    }

    [Fact]
    public void Step_CannotGrabWhileHeld_KeepsDragging()
    {
        GrabAndBreakFree();

        var events = StepUntil(_part, Hold(0), DragEvents.SnappedEnd, canGrab: false);

        Assert.Equal(DragEvents.SnappedEnd, events);
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(106, 50, true)]
    [InlineData(104, 55, false)]
    public void IsUnder_Point_ChecksTheGrabRadius(float x, float y, bool expected)
        => Assert.Equal(expected, _part.IsUnder(new Vector2(x, y)));

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(127, 50, true)]
    [InlineData(127, 54, false)]
    [InlineData(106, 50, true)]
    [InlineData(99, 47, false)]
    public void IsUnder_WithShape_ChecksTheShapeInsteadOfTheGrabRadius(float x, float y, bool expected)
    {
        // A 30x3 bar reaching right from the grab point, much longer than the grab radius.
        var shape = new GrabShape(30, 3, new Vector2(1, 1), static (_, _) => true, margin: 0);
        var part = new PartDrag(s_path, 6, DragFeel.Default, shape: shape);

        Assert.Equal(expected, part.IsUnder(new Vector2(x, y)));
    }

    [Fact]
    public void IsUnder_Turned_ChecksTheTurnedShape()
    {
        var shape = new GrabShape(30, 3, new Vector2(1, 1), static (_, _) => true, margin: 0);
        var part = new PartDrag(s_path, 6, DragFeel.Default, shape: shape) { Angle = MathF.PI / 2 };

        Assert.True(part.IsUnder(new Vector2(100, 70)));
        Assert.False(part.IsUnder(new Vector2(120, 50)));
    }

    [Fact]
    public void IsUnder_OnItsCoverInTheGun_IsFalse()
    {
        // Everything right of x = 104 hides the part.
        var cover = new GrabShape(100, 100, new Vector2(-104, 0), static (_, _) => true, margin: 0);
        var shape = new GrabShape(30, 3, new Vector2(1, 1), static (_, _) => true, margin: 0);
        var part = new PartDrag(s_path, 6, DragFeel.Default, shape: shape, cover: cover);

        Assert.True(part.IsUnder(new Vector2(101, 50)));
        Assert.False(part.IsUnder(new Vector2(110, 50)));
        Assert.Same(cover, part.Cover);
    }

    [Fact]
    public void Step_PressOnTheFarEndOfItsShape_GrabsIt()
    {
        var shape = new GrabShape(30, 3, new Vector2(1, 1), static (_, _) => true, margin: 0);
        var part = new PartDrag(s_path, 6, DragFeel.Default, shape: shape);

        var events = part.Step(Press(127), Dt);

        Assert.Equal(DragEvents.Grabbed, events);
        Assert.True(part.IsHeld);
    }

    [Fact]
    public void Step_PressAwayFromPart_DoesNotGrab()
    {
        var events = _part.Step(Press(120), Dt);

        Assert.Equal(DragEvents.None, events);
        Assert.False(_part.IsHeld);
    }

    [Fact]
    public void Step_ButtonAlreadyDownOverPart_DoesNotGrab()
    {
        // Pressed elsewhere, then dragged over the part.
        _part.Step(Press(120), Dt);

        var events = _part.Step(Hold(100), Dt);

        Assert.Equal(DragEvents.None, events);
        Assert.False(_part.IsHeld);
    }

    [Fact]
    public void Step_PressAndReleaseInOneStep_GrabsAndDrops()
    {
        var events = _part.Step(new PointerSample(new Vector2(100, 50), IsDown: false, IsPressed: true), Dt);

        Assert.Equal(DragEvents.Grabbed | DragEvents.Dropped, events);
        Assert.False(_part.IsHeld);
        Assert.Equal(0, _part.Distance);
    }

    [Fact]
    public void Step_PullLessThanBreakaway_StrainsButStaysSnapped()
    {
        _part.Step(Press(100), Dt);

        var events = _part.Step(Hold(94), Dt);

        Assert.Equal(DragEvents.None, events);
        Assert.Equal(PartStop.Start, _part.Stop);
        Assert.Equal(6 * DragFeel.Default.Strain, _part.Distance);
        Assert.True(_part.IsHeld);
    }

    [Fact]
    public void Step_PushIntoTheStop_DoesNotMove()
    {
        _part.Step(Press(100), Dt);

        _part.Step(Hold(130), Dt);

        Assert.Equal(0, _part.Distance);
        Assert.Equal(PartStop.Start, _part.Stop);
    }

    [Fact]
    public void Step_ReleaseWhileStrained_SpringsBackToTheStop()
    {
        _part.Step(Press(100), Dt);
        _part.Step(Hold(94), Dt);

        var events = _part.Step(Release(94), Dt);

        Assert.Equal(DragEvents.Dropped, events);
        Assert.Equal(0, _part.Distance);
        Assert.Equal(PartStop.Start, _part.Stop);
        Assert.False(_part.IsHeld);
    }

    [Fact]
    public void Step_PullPastBreakaway_ComesLooseBehindThePointer()
    {
        GrabAndBreakFree();

        Assert.Equal(PartStop.None, _part.Stop);
        Assert.InRange(_part.Distance, 0.01f, 9.99f);
    }

    [Fact]
    public void Step_LooseAndHeld_CatchesUpWithThePointerOverTime()
    {
        GrabAndBreakFree();

        _part.Step(Hold(80), Dt);
        var afterOneStep = _part.Distance;
        StepUntil(_part, Hold(80), DragEvents.SnappedEnd | DragEvents.SnappedStart, maxSteps: 120);

        Assert.InRange(afterOneStep, 0, 19);
        Assert.Equal(20, _part.Distance, 0.01f);
        Assert.Equal(PartStop.None, _part.Stop);
    }

    [Fact]
    public void Step_FastPointer_MovesNoFasterThanMaxSpeed()
    {
        var part = new PartDrag(s_path, 6, new DragFeel { Follow = 1000, MaxSpeed = 60 });
        part.Step(Press(100), Dt);
        part.Step(Hold(90), Dt);

        var before = part.Distance;
        part.Step(Hold(70), Dt);

        Assert.Equal(1, part.Distance - before, 0.001f);
    }

    [Fact]
    public void Step_GrabbedOffCenter_KeepsTheGrabOffset()
    {
        // Grabbed 3 px behind its position: the part ends up 3 px ahead of the pointer.
        _part.Step(Press(103), Dt);

        StepUntil(_part, Hold(83), DragEvents.SnappedEnd, maxSteps: 120);

        Assert.Equal(20, _part.Distance, 0.01f);
    }

    [Fact]
    public void Step_PointerOffTheAxis_FollowsItsProjection()
    {
        _part.Step(Press(100, 54), Dt);

        StepUntil(_part, Hold(80, 70), DragEvents.SnappedEnd, maxSteps: 120);

        Assert.Equal(20, _part.Distance, 0.01f);
        Assert.Equal(50, _part.Position.Y);
    }

    [Fact]
    public void Step_PullToTheEnd_SnapsOutOnceAndLetsGo()
    {
        GrabAndBreakFree();

        var events = StepUntil(_part, Hold(0), DragEvents.SnappedEnd);
        var later = _part.Step(Hold(0), Dt);

        Assert.Equal(DragEvents.SnappedEnd, events);
        Assert.Equal(PartStop.End, _part.Stop);
        Assert.Equal(s_path.Length, _part.Distance);
        Assert.False(_part.IsHeld);
        Assert.Equal(DragEvents.None, later);
    }

    [Fact]
    public void Step_ReleaseBetweenTheEnds_StaysWhereItIs()
    {
        GrabAndBreakFree();
        StepUntil(_part, Hold(80), DragEvents.SnappedEnd, maxSteps: 30);
        var distance = _part.Distance;

        var events = _part.Step(Release(80), Dt);
        _part.Step(Hover(0), Dt);

        Assert.Equal(DragEvents.Dropped, events);
        Assert.Equal(distance, _part.Distance);
        Assert.Equal(PartStop.None, _part.Stop);
    }

    [Fact]
    public void Step_PushBackFromTheEnd_SnapsSeated()
    {
        var part = new PartDrag(s_path, 6, DragFeel.Default, PartStop.End);
        part.Step(Press(60), Dt);

        var events = StepUntil(part, Hold(200), DragEvents.SnappedStart);

        Assert.Equal(DragEvents.BrokeFree | DragEvents.SnappedStart, events);
        Assert.Equal(PartStop.Start, part.Stop);
        Assert.Equal(0, part.Distance);
    }

    [Fact]
    public void Step_JitterRightAfterLeavingAStop_DoesNotSnapBack()
    {
        var part = new PartDrag(s_path, 6, DragFeel.Default, PartStop.End);
        part.Step(Press(60), Dt);
        Assert.Equal(DragEvents.BrokeFree, part.Step(Hold(61), Dt));

        var events = StepUntil(part, Hold(55), DragEvents.SnappedEnd, maxSteps: 30);

        Assert.Equal(DragEvents.None, events);
        Assert.Equal(PartStop.None, part.Stop);
    }

    [Fact]
    public void Step_ReleasedInASnapZone_SettlesIntoThatEnd()
    {
        var part = new PartDrag(s_path, 6, DragFeel.Default, PartStop.End);
        part.Step(Press(60), Dt);
        part.Step(Hold(61), Dt);

        var events = part.Step(Release(61), Dt);

        Assert.Equal(DragEvents.Dropped | DragEvents.SnappedEnd, events);
        Assert.Equal(PartStop.End, part.Stop);
        Assert.Equal(s_path.Length, part.Distance);
    }

    [Fact]
    public void Step_LeftAStopAndCameBack_SnapsIntoItAgain()
    {
        var part = new PartDrag(s_path, 6, DragFeel.Default, PartStop.End);
        part.Step(Press(60), Dt);
        StepUntil(part, Hold(75), DragEvents.SnappedStart, maxSteps: 60);

        var events = StepUntil(part, Hold(40), DragEvents.SnappedEnd);

        Assert.Equal(DragEvents.SnappedEnd, events);
        Assert.Equal(PartStop.End, part.Stop);
    }

    [Fact]
    public void ReturnTo_HeldHalfway_LetsGoAndSitsInTheStopWithoutMotion()
    {
        GrabAndBreakFree();
        _part.Step(Hold(80), Dt);

        _part.ReturnTo(PartStop.Start);

        Assert.False(_part.IsHeld);
        Assert.Equal(PartStop.Start, _part.Stop);
        Assert.Equal(0, _part.Distance);
        Assert.Equal(_part.Position, _part.GetRenderPosition(0));
    }

    [Fact]
    public void ReturnTo_Loose_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _part.ReturnTo(PartStop.None));

    [Fact]
    public void GetRenderPosition_Alpha_InterpolatesTheLastStep()
    {
        GrabAndBreakFree();
        _part.Step(Hold(80), Dt);
        var from = s_path.PointAt(_part.PreviousDistance);
        var to = _part.Position;

        Assert.Equal(from, _part.GetRenderPosition(0));
        Assert.Equal(to, _part.GetRenderPosition(1));
        Assert.Equal(Vector2.Lerp(from, to, 0.5f).X, _part.GetRenderPosition(0.5f).X, 0.001f);
    }
}
