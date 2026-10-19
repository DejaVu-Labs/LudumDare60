using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Parts;

public sealed class PartAssemblyTests
{
    private const float Dt = 1f / 60;
    private const int Pin = 0;
    private const int Cylinder = 1;

    // A pin pulled out to the left, through a cylinder that comes out downwards once the pin is out.
    private readonly PartDrag _pin = new(new LinePath(new Vector2(100, 50), new Vector2(60, 50)), 6, DragFeel.Default);
    private readonly PartDrag _cylinder = new(new LinePath(new Vector2(120, 50), new Vector2(120, 90)), 8, new DragFeel { BreakawayAtStart = 4 });
    private readonly PartAssembly _gun;

    public PartAssemblyTests()
    {
        _gun = new PartAssembly(_pin, _cylinder);
        _gun.AddBlock(Pin, Cylinder);
    }

    private static PointerSample Press(Vector2 at) => new(at, IsDown: true, IsPressed: true);

    private static PointerSample Hold(Vector2 at) => new(at, IsDown: true, IsPressed: false);

    private static PointerSample Release(Vector2 at) => new(at, IsDown: false, IsPressed: false);

    /// <summary>
    /// Grabs <paramref name="part"/> where it is and drags it towards <paramref name="towards"/> until it snaps, then
    /// lets go. Returns every event of that part.
    /// </summary>
    private DragEvents Drag(int part, Vector2 towards)
    {
        var seen = DragEvents.None;
        _gun.Step(Press(_gun[part].Position), Dt);
        seen |= _gun.Events[part];
        for (var i = 0; i < 600 && (seen & (DragEvents.SnappedStart | DragEvents.SnappedEnd | DragEvents.Refused)) == DragEvents.None; i++)
        {
            _gun.Step(Hold(towards), Dt);
            seen |= _gun.Events[part];
        }

        _gun.Step(Release(towards), Dt);
        return seen;
    }

    [Fact]
    public void Constructor_NoParts_Throws()
        => Assert.Throws<ArgumentException>(() => new PartAssembly());

    [Fact]
    public void Constructor_SamePartTwice_Throws()
        => Assert.Throws<ArgumentException>(() => new PartAssembly(_pin, _pin));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 2)]
    [InlineData(-1, 1)]
    public void AddBlock_BadIndexes_Throws(int blocker, int blocked)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _gun.AddBlock(blocker, blocked));

    [Fact]
    public void CanMove_Assembled_OnlyTheBlockerMoves()
    {
        Assert.True(_gun.CanMove(Pin));
        Assert.False(_gun.CanMove(Cylinder));
        Assert.True(_gun.Holds(Pin, Cylinder));
        Assert.False(_gun.Holds(Cylinder, Pin));
    }

    [Fact]
    public void Step_PressOnBlockedPart_RefusesItAndLeavesItSeated()
    {
        var events = Drag(Cylinder, new Vector2(120, 200));

        Assert.Equal(DragEvents.Refused, events);
        Assert.Equal(PartStop.Start, _cylinder.Stop);
        Assert.Equal(0, _cylinder.Distance);
        Assert.False(_cylinder.IsHovered);
    }

    [Fact]
    public void Step_BlockerHalfwayOut_StillBlocks()
    {
        _gun.Step(Press(_pin.Position), Dt);
        for (var i = 0; i < 20; i++)
        {
            _gun.Step(Hold(new Vector2(80, 50)), Dt);
        }

        _gun.Step(Release(new Vector2(80, 50)), Dt);

        Assert.Equal(PartStop.None, _pin.Stop);
        Assert.False(_gun.CanMove(Cylinder));
    }

    [Fact]
    public void Step_BlockerOut_FreesTheBlockedPart()
    {
        Assert.Equal(DragEvents.SnappedEnd, Drag(Pin, new Vector2(0, 50)) & DragEvents.SnappedEnd);

        Assert.True(_gun.CanMove(Cylinder));
        Assert.Equal(DragEvents.SnappedEnd, Drag(Cylinder, new Vector2(120, 200)) & DragEvents.SnappedEnd);
        Assert.True(_gun.AllAt(PartStop.End));
    }

    [Fact]
    public void CanMove_BlockedPartOut_BlockerCannotGoBack()
    {
        Drag(Pin, new Vector2(0, 50));
        Drag(Cylinder, new Vector2(120, 200));

        Assert.False(_gun.CanMove(Pin));
        Assert.True(_gun.Holds(Cylinder, Pin));
        Assert.Equal(DragEvents.Refused, Drag(Pin, new Vector2(200, 50)));
        Assert.Equal(PartStop.End, _pin.Stop);
    }

    [Fact]
    public void Step_ReassembledInReverseOrder_EndsAllSeated()
    {
        Drag(Pin, new Vector2(0, 50));
        Drag(Cylinder, new Vector2(120, 200));

        Drag(Cylinder, new Vector2(120, 0));
        Drag(Pin, new Vector2(200, 50));

        Assert.True(_gun.AllAt(PartStop.Start));
        Assert.False(_gun.AllAt(PartStop.End));
    }

    [Fact]
    public void Step_HeldPart_KeepsThePointerFromOtherParts()
    {
        Drag(Pin, new Vector2(0, 50));
        _gun.Step(Press(_cylinder.Position), Dt);

        // Dragged over the pin's head, the held cylinder keeps the pointer; the pin does not light up.
        _gun.Step(Hold(_pin.Position), Dt);

        Assert.True(_cylinder.IsHeld);
        Assert.False(_pin.IsHovered);
    }

    [Fact]
    public void Step_OverlappingParts_TheFirstOneGetsThePointer()
    {
        var a = new PartDrag(new LinePath(new Vector2(10, 10), new Vector2(50, 10)), 6, DragFeel.Default);
        var b = new PartDrag(new LinePath(new Vector2(12, 10), new Vector2(12, 50)), 6, DragFeel.Default);
        var parts = new PartAssembly(a, b);

        parts.Step(Press(new Vector2(11, 10)), Dt);

        Assert.True(a.IsHeld);
        Assert.False(b.IsHeld);
        Assert.False(b.IsHovered);
        Assert.Equal(DragEvents.Grabbed, parts.Events[0]);
        Assert.Equal(DragEvents.None, parts.Events[1]);
    }

    [Fact]
    public void Step_OverlappingPartsAtDifferentDepths_TheHigherOneGetsThePointer()
    {
        var a = new PartDrag(new LinePath(new Vector2(10, 10), new Vector2(50, 10)), 6, DragFeel.Default);
        var b = new PartDrag(new LinePath(new Vector2(12, 10), new Vector2(12, 50)), 6, DragFeel.Default);
        var parts = new PartAssembly(a, b);
        parts.SetDepth(1, 1);

        parts.Step(Press(new Vector2(11, 10)), Dt);

        Assert.True(b.IsHeld);
        Assert.False(a.IsHeld);
        Assert.Equal(DragEvents.None, parts.Events[0]);
    }

    [Fact]
    public void Step_LoosePartOverAPartInTheGun_TheLooseOneGetsThePointer()
    {
        var free = new FreeMove(Vector2.Zero, new Vector2(200), DockRadius: 2, DockSpan: 0, SeatsOnDock: true);
        var a = new PartDrag(new LinePath(new Vector2(10, 10), new Vector2(50, 10)), 6, DragFeel.Default);
        var b = new PartDrag(new LinePath(new Vector2(100, 10), new Vector2(100, 40)), 6, DragFeel.Default, free: free);
        var parts = new PartAssembly(a, b);
        parts.SetDepth(0, 5);
        CarryOut(parts, 1, new Vector2(100, 10), new Vector2(11, 10));

        parts.Step(Press(new Vector2(11, 10)), Dt);

        Assert.True(b.IsHeld);
        Assert.False(a.IsHeld);
    }

    [Fact]
    public void Step_TwoLooseParts_TheLaterOneLiesOnTopAndGetsThePointer()
    {
        var free = new FreeMove(Vector2.Zero, new Vector2(200), DockRadius: 2, DockSpan: 0, SeatsOnDock: true);
        var a = new PartDrag(new LinePath(new Vector2(100, 10), new Vector2(100, 40)), 6, DragFeel.Default, free: free);
        var b = new PartDrag(new LinePath(new Vector2(150, 10), new Vector2(150, 40)), 6, DragFeel.Default, free: free);
        var parts = new PartAssembly(a, b);
        CarryOut(parts, 1, new Vector2(150, 10), new Vector2(60, 100));
        CarryOut(parts, 0, new Vector2(100, 10), new Vector2(61, 100));

        parts.Step(Press(new Vector2(60.5f, 100)), Dt);

        Assert.True(b.IsHeld);
        Assert.False(a.IsHeld);
    }

    [Fact]
    public void PartUnder_OverlappingParts_IsTheOneOnTop()
    {
        var a = new PartDrag(new LinePath(new Vector2(10, 10), new Vector2(50, 10)), 6, DragFeel.Default);
        var b = new PartDrag(new LinePath(new Vector2(12, 10), new Vector2(12, 50)), 6, DragFeel.Default);
        var parts = new PartAssembly(a, b);

        Assert.Equal(0, parts.PartUnder(new Vector2(11, 10)));
        Assert.Equal(1, parts.PartUnder(new Vector2(17, 10)));
        Assert.Equal(-1, parts.PartUnder(new Vector2(100, 100)));

        parts.SetDepth(1, 1);

        Assert.Equal(1, parts.PartUnder(new Vector2(11, 10)));
    }

    /// <summary>
    /// Takes <paramref name="part"/> at <paramref name="from"/>, pulls it down out of the gun and puts it down at
    /// <paramref name="to"/>.
    /// </summary>
    private static void CarryOut(PartAssembly parts, int part, Vector2 from, Vector2 to)
    {
        parts.Step(Press(from), Dt);
        for (var i = 0; i < 60 && !parts[part].IsFree; i++)
        {
            parts.Step(Hold(from + new Vector2(0, 60)), Dt);
        }

        for (var i = 0; i < 120; i++)
        {
            parts.Step(Hold(to), Dt);
        }

        parts.Step(Release(to), Dt);
        Assert.True(parts[part].IsFree);
        Assert.False(parts[part].IsHeld);
        Assert.Equal(to.X, parts[part].Position.X, 0.1f);
        Assert.Equal(to.Y, parts[part].Position.Y, 0.1f);
    }

    [Fact]
    public void SetLocked_FreePart_CannotBeGrabbedAndIsNotRefused()
    {
        _gun.SetLocked(Pin, locked: true);

        var events = Drag(Pin, new Vector2(0, 50));

        Assert.True(_gun.IsLocked(Pin));
        Assert.False(_gun.CanMove(Pin));
        Assert.Equal(DragEvents.None, events);
        Assert.Equal(PartStop.Start, _pin.Stop);
    }

    [Fact]
    public void SetLocked_Unlocked_MovesAgain()
    {
        _gun.SetLocked(Pin, locked: true);
        _gun.SetLocked(Pin, locked: false);

        Assert.True(_gun.CanMove(Pin));
        Assert.Equal(DragEvents.SnappedEnd, Drag(Pin, new Vector2(0, 50)) & DragEvents.SnappedEnd);
    }

    [Fact]
    public void Events_NextStep_StartsEmpty()
    {
        _gun.Step(Press(_cylinder.Position), Dt);
        Assert.Equal(DragEvents.Refused, _gun.Events[Cylinder]);

        _gun.Step(Hold(_cylinder.Position), Dt);

        Assert.Equal(DragEvents.None, _gun.Events[Cylinder]);
    }
}
