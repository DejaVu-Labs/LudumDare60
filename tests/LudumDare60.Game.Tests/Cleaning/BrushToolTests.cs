using System.Numerics;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Cleaning;

public sealed class BrushToolTests
{
    private const float Dt = 1f / 60;

    // Bristles at (100, 50), handle to the right.
    private readonly BrushTool _brush = new(new Vector2(100, 50), new Vector2(-4, -4), new Vector2(19, 4));

    private static PointerSample Press(float x, float y) => new(new Vector2(x, y), IsDown: true, IsPressed: true);

    private static PointerSample Hold(float x, float y) => new(new Vector2(x, y), IsDown: true, IsPressed: false);

    private static PointerSample Release(float x, float y) => new(new Vector2(x, y), IsDown: false, IsPressed: false);

    [Fact]
    public void Constructor_LiesAtItsPlace()
    {
        Assert.True(_brush.IsResting);
        Assert.Equal(new Vector2(100, 50), _brush.Position);
    }

    [Theory]
    [InlineData(100, 50, true)]
    [InlineData(118, 53, true)]
    [InlineData(95, 50, false)]
    [InlineData(110, 55, false)]
    public void IsUnder_Point_ChecksTheGrabArea(float x, float y, bool expected)
        => Assert.Equal(expected, _brush.IsUnder(new Vector2(x, y)));

    [Fact]
    public void Step_PointerOverIt_IsHovered()
    {
        _brush.Step(Release(110, 50), Dt, canGrab: true);

        Assert.True(_brush.IsHovered);
    }

    [Fact]
    public void Step_PressOnTheHandle_PicksItUpWithTheBristlesUnderThePointer()
    {
        var events = _brush.Step(Press(115, 51), Dt, canGrab: true);

        Assert.Equal(BrushEvents.PickedUp, events);
        Assert.True(_brush.IsHeld);
        Assert.False(_brush.IsHovered);
        Assert.Equal(new Vector2(115, 51), _brush.Position);
    }

    [Fact]
    public void Step_CannotGrab_StaysPut()
    {
        var events = _brush.Step(Press(100, 50), Dt, canGrab: false);

        Assert.Equal(BrushEvents.None, events);
        Assert.False(_brush.IsHeld);
        Assert.False(_brush.IsHovered);
    }

    [Fact]
    public void Step_ButtonAlreadyDown_DoesNotPickItUp()
    {
        _brush.Step(Press(300, 300), Dt, canGrab: true);

        var events = _brush.Step(Hold(100, 50), Dt, canGrab: true);

        Assert.Equal(BrushEvents.None, events);
        Assert.False(_brush.IsHeld);
    }

    [Fact]
    public void Step_Held_FollowsThePointer()
    {
        _brush.Step(Press(100, 50), Dt, canGrab: true);

        _brush.Step(Hold(40, 70), Dt, canGrab: false);

        Assert.Equal(new Vector2(40, 70), _brush.Position);
        Assert.Equal(new Vector2(100, 50), _brush.PreviousPosition);
        Assert.Equal(new Vector2(70, 60), _brush.GetRenderPosition(0.5f));
    }

    [Fact]
    public void Step_LetGo_SlidesHomeAndLandsOnce()
    {
        _brush.Step(Press(100, 50), Dt, canGrab: true);
        _brush.Step(Hold(40, 70), Dt, canGrab: true);

        var events = _brush.Step(Release(40, 70), Dt, canGrab: true);
        var landed = 0;
        for (var i = 0; i < 120; i++)
        {
            if ((_brush.Step(Release(40, 70), Dt, canGrab: true) & BrushEvents.Landed) != BrushEvents.None)
            {
                landed++;
            }
        }

        Assert.Equal(BrushEvents.PutDown, events);
        Assert.Equal(1, landed);
        Assert.True(_brush.IsResting);
    }

    [Fact]
    public void Step_LetGo_IsOnItsWayAfterOneStep()
    {
        _brush.Step(Press(100, 50), Dt, canGrab: true);
        _brush.Step(Hold(40, 50), Dt, canGrab: true);
        _brush.Step(Release(40, 50), Dt, canGrab: true);

        _brush.Step(Release(40, 50), Dt, canGrab: true);

        Assert.InRange(_brush.Position.X, 40.1f, 99f);
    }

    [Fact]
    public void Step_PressAndReleaseInOneStep_PicksUpAndPutsDown()
    {
        var events = _brush.Step(new PointerSample(new Vector2(100, 50), IsDown: false, IsPressed: true), Dt, canGrab: true);

        Assert.Equal(BrushEvents.PickedUp | BrushEvents.PutDown, events);
        Assert.True(_brush.IsResting);
    }
}
