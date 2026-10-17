using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Parts;

public sealed class LinePathTests
{
    private readonly LinePath _path = new(new Vector2(100, 50), new Vector2(60, 50));

    [Fact]
    public void Constructor_TwoPoints_MeasuresLengthAndDirection()
    {
        Assert.Equal(40, _path.Length);
        Assert.Equal(new Vector2(-1, 0), _path.Direction);
    }

    [Fact]
    public void Constructor_SamePoint_Throws()
        => Assert.Throws<ArgumentException>(() => new LinePath(Vector2.One, Vector2.One));

    [Theory]
    [InlineData(0, 100)]
    [InlineData(10, 90)]
    [InlineData(40, 60)]
    [InlineData(50, 50)]
    public void PointAt_Distance_WalksFromStartTowardsEnd(float distance, float x)
        => Assert.Equal(new Vector2(x, 50), _path.PointAt(distance));

    [Theory]
    [InlineData(90, 50, 10)]
    [InlineData(90, 80, 10)]
    [InlineData(110, 50, -10)]
    [InlineData(40, 20, 60)]
    public void Project_Point_GivesDistanceAlongTheLine(float x, float y, float distance)
        => Assert.Equal(distance, _path.Project(new Vector2(x, y)));

    [Fact]
    public void Project_DiagonalPath_UsesTheLineNotTheAxes()
    {
        var path = new LinePath(Vector2.Zero, new Vector2(30, 40));

        Assert.Equal(50, path.Length);
        Assert.Equal(25, path.Project(new Vector2(15, 20)), 4);
        Assert.Equal(25, path.Project(new Vector2(15, 20) + new Vector2(-40, 30)), 4);
    }
}
