using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Parts;

public sealed class PartDragScrewTests
{
    private const float Dt = 1f / 60;

    // A quarter turn about every ten steps: 9 rad/s, 72 px/s along the turn, well under the part's top speed.
    private const float AngleStep = 0.15f;

    private static readonly Vector2 s_center = new(100, 100);
    private static readonly PartPath s_path = PartPath.Screw(s_center, turns: 2);
    private static readonly DragFeel s_feel = new() { BreakawayAtStart = 6, SnapDistance = 4 };
    private static readonly FreeMove s_free = new(Vector2.Zero, new Vector2(300, 200), DockRadius: 5, DockSpan: 0);

    private readonly PartDrag _screw = new(s_path, 5, s_feel, PartStop.Start, s_free);

    private float _angle;

    private static PointerSample Press(Vector2 at) => new(at, IsDown: true, IsPressed: true);

    private static PointerSample Hold(Vector2 at) => new(at, IsDown: true, IsPressed: false);

    private static PointerSample Release(Vector2 at) => new(at, IsDown: false, IsPressed: false);

    private static Vector2 Around(float angle, float pixels = 15) => s_center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * pixels);

    /// <summary>
    /// Takes the screw by its head, just right of its middle.
    /// </summary>
    private DragEvents Grab()
    {
        _angle = 0;
        return _screw.Step(Press(s_center + new Vector2(3, 0)), Dt);
    }

    /// <summary>
    /// Circles the hand around the screw for <paramref name="steps"/> steps: counterclockwise on screen (backing it
    /// out) when <paramref name="outwards"/>, else clockwise. Returns every event seen.
    /// </summary>
    private DragEvents Circle(bool outwards, int steps, bool canDock = true)
    {
        var seen = DragEvents.None;
        for (var i = 0; i < steps; i++)
        {
            _angle += outwards ? -AngleStep : AngleStep;
            seen |= _screw.Step(Hold(Around(_angle)), Dt, canGrab: true, canDock);
        }

        return seen;
    }

    private DragEvents HoldAt(Vector2 at, int steps, bool canDock = true)
    {
        var seen = DragEvents.None;
        for (var i = 0; i < steps; i++)
        {
            seen |= _screw.Step(Hold(at), Dt, canGrab: true, canDock);
        }

        return seen;
    }

    /// <summary>
    /// Backs the screw all the way out: it comes into the hand.
    /// </summary>
    private DragEvents Unscrew()
    {
        var seen = Grab();
        return seen | Circle(outwards: true, 150);
    }

    [Fact]
    public void Step_HandCirclesCounterclockwise_BacksTheScrewOutIntoTheHand()
    {
        var events = Unscrew();

        Assert.True((events & DragEvents.BrokeFree) != DragEvents.None);
        Assert.True((events & DragEvents.SnappedEnd) != DragEvents.None);
        Assert.True(_screw.IsFree);
        Assert.True(_screw.IsHeld);
        Assert.Equal(PartStop.End, _screw.Stop);
    }

    [Fact]
    public void Step_HandCirclesClockwiseWhenTight_OnlyStrains()
    {
        Grab();

        var events = Circle(outwards: false, 60);

        Assert.Equal(DragEvents.None, events);
        Assert.Equal(PartStop.Start, _screw.Stop);
        Assert.InRange(_screw.Distance, 0, s_feel.BreakawayAtStart * s_feel.Strain);
    }

    [Fact]
    public void Step_HalfATurnOutAndLetGo_StaysHalfwayOutWhereItIs()
    {
        Grab();
        Circle(outwards: true, 40);
        _screw.Step(Release(Around(_angle)), Dt);

        Assert.Equal(PartStop.None, _screw.Stop);
        Assert.False(_screw.IsHeld);
        Assert.InRange(_screw.Distance, s_feel.BreakawayAtStart, s_path.Length - s_feel.SnapDistance);
        Assert.Equal(s_center, _screw.Position);
        Assert.True(s_path.AngleAt(_screw.Distance) < 0, "It has turned counterclockwise.");
    }

    [Fact]
    public void Step_TurningOnPastTheWinding_DoesNotRunAway()
    {
        Grab();
        Circle(outwards: true, 40);
        var partway = _screw.Distance;

        // Back the other way: the screw follows at once, not after unwinding many turns.
        Circle(outwards: false, 20);

        Assert.True(_screw.Distance < partway, $"Back from {partway} to {_screw.Distance}.");
    }

    [Fact]
    public void Step_UnscrewedAndPutOnItsHole_DocksAndScrewsBackInClockwise()
    {
        Unscrew();
        HoldAt(Around(_angle, 40), 30);

        var docked = HoldAt(s_center, 30);
        Assert.True((docked & DragEvents.Docked) != DragEvents.None);
        Assert.False(_screw.IsFree);
        Assert.Equal(PartStop.End, _screw.Stop);

        _angle = 0;
        var events = Circle(outwards: false, 200);

        Assert.True((events & DragEvents.SnappedStart) != DragEvents.None);
        Assert.Equal(PartStop.Start, _screw.Stop);
        Assert.False(_screw.IsHeld);
    }

    [Fact]
    public void Step_UnscrewedAndPutOnItsHoleWhileItMayNotGoBack_IsRefused()
    {
        Unscrew();
        HoldAt(Around(_angle, 40), 30, canDock: false);

        var events = HoldAt(s_center, 30, canDock: false);

        Assert.True((events & DragEvents.DockRefused) != DragEvents.None);
        Assert.True(_screw.IsFree);
    }
}
