using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Parts;

public sealed class PartPathTests
{
    private const float Radius = 8;
    private const float Turn = MathF.Tau * Radius;

    private static readonly Vector2 s_center = new(100, 100);

    private readonly PartPath _screw = PartPath.Screw(s_center, turns: 2, Radius);

    /// <summary>
    /// The point <paramref name="pixels"/> from the screw's middle at <paramref name="angle"/> radians, as atan2 on screen
    /// measures it: 0 to the right, growing clockwise.
    /// </summary>
    private static Vector2 Around(float angle, float pixels = 20) => s_center + (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * pixels);

    [Fact]
    public void FromLinePath_KeepsTheLine()
    {
        PartPath path = new LinePath(new Vector2(100, 50), new Vector2(60, 50));

        Assert.False(path.IsTurn);
        Assert.Equal(40, path.Length);
        Assert.Equal(new Vector2(90, 50), path.PointAt(10));
        Assert.Equal(10, path.Project(new Vector2(90, 80), near: 1000));
        Assert.Equal(30, path.SideDistance(new Vector2(90, 80)));
        Assert.Equal(0, path.AngleAt(10));
        Assert.Equal(float.PositiveInfinity, path.Slack);
    }

    [Fact]
    public void Screw_IsAsLongAsItsTurnsAndStaysPut()
    {
        Assert.True(_screw.IsTurn);
        Assert.Equal(2 * Turn, _screw.Length, 3);
        Assert.Equal(s_center, _screw.PointAt(0));
        Assert.Equal(s_center, _screw.PointAt(30));
        Assert.Equal(Vector2.Zero, _screw.Direction);
        Assert.Equal(0, _screw.SideDistance(new Vector2(300, 0)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    public void Screw_NoTurns_Throws(float turns)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PartPath.Screw(s_center, turns));

    [Fact]
    public void Project_HandCounterclockwise_GrowsTheDistance()
    {
        // A quarter turn counterclockwise on screen: from the right up to the top.
        var right = _screw.Project(Around(0), near: 0);
        var top = _screw.Project(Around(-MathF.PI / 2), near: right);

        Assert.Equal(0, right, 3);
        Assert.Equal(Turn / 4, top, 3);
    }

    [Fact]
    public void Project_HandClockwise_ShrinksTheDistance()
    {
        var bottom = _screw.Project(Around(MathF.PI / 2), near: 0);

        Assert.Equal(-Turn / 4, bottom, 3);
    }

    [Fact]
    public void Project_NearAnotherTurn_PicksTheClosestWinding()
    {
        var once = _screw.Project(Around(-MathF.PI / 2), near: Turn);
        var twice = _screw.Project(Around(-MathF.PI / 2), near: 2 * Turn);

        Assert.Equal(Turn * 1.25f, once, 3);
        Assert.Equal(Turn * 2.25f, twice, 3);
    }

    [Fact]
    public void Project_HowFarFromTheMiddle_DoesNotMatter()
        => Assert.Equal(_screw.Project(Around(-1, 5), near: 0), _screw.Project(Around(-1, 60), near: 0), 3);

    [Fact]
    public void AngleAt_BackingOut_TurnsCounterclockwise()
    {
        Assert.Equal(0, _screw.AngleAt(0));
        Assert.Equal(-MathF.PI / 2, _screw.AngleAt(Turn / 4), 4);
        Assert.Equal(MathF.PI * Radius, _screw.Slack, 3);
    }
}
