using System.Numerics;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game.Tests.Parts;

public sealed class GrabShapeTests
{
    // A 10x4 bar whose grab point is on its left end: pixels (0..9, 0..3), origin (1, 2).
    private static readonly GrabShape s_bar = new(10, 4, new Vector2(1, 2), static (_, _) => true, margin: 0);

    // A single pixel at (2, 2) of a 5x5 sprite, taken within two pixels of it; the origin is that pixel's middle.
    private static readonly GrabShape s_dot = new(5, 5, new Vector2(2.5f, 2.5f), static (x, y) => x == 2 && y == 2, margin: 2);

    [Theory]
    [InlineData(-1, -2, true)]
    [InlineData(8.9f, 1.9f, true)]
    [InlineData(9, 0, false)]
    [InlineData(-1.1f, 0, false)]
    [InlineData(0, 2, false)]
    public void Contains_Offset_ChecksThePixelsAroundTheOrigin(float x, float y, bool expected)
        => Assert.Equal(expected, s_bar.Contains(new Vector2(x, y)));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(2, 0, true)]
    [InlineData(0, -2, true)]
    [InlineData(1, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(3, 0, false)]
    public void Contains_Margin_AddsADiscAroundEveryPixel(float x, float y, bool expected)
        => Assert.Equal(expected, s_dot.Contains(new Vector2(x, y)));

    [Fact]
    public void Contains_PixelOutsideThePart_IsFalse()
    {
        var hollow = new GrabShape(3, 3, Vector2.Zero, static (x, y) => x != 1 || y != 1, margin: 0);

        Assert.True(hollow.Contains(new Vector2(0.5f, 0.5f)));
        Assert.False(hollow.Contains(new Vector2(1.5f, 1.5f)));
    }

    [Fact]
    public void Constructor_NegativeMargin_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new GrabShape(3, 3, Vector2.Zero, static (_, _) => true, margin: -1));
}
