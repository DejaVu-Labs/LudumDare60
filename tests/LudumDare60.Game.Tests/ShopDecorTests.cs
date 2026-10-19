using System.Numerics;

namespace LudumDare60.Game.Tests;

public sealed class ShopDecorTests
{
    [Theory]
    [InlineData(40, 45)]
    [InlineData(22, 30)]
    [InlineData(59.5f, 59.5f)]
    public void IsOnElephant_OnTheStatuette_IsTrue(float x, float y)
        => Assert.True(ShopDecor.IsOnElephant(new Vector2(x, y)));

    [Theory]
    [InlineData(21.5f, 45)]
    [InlineData(40, 29.5f)]
    [InlineData(60, 45)]
    [InlineData(40, 60)]
    [InlineData(300, 200)]
    public void IsOnElephant_ElsewhereInTheRoom_IsFalse(float x, float y)
        => Assert.False(ShopDecor.IsOnElephant(new Vector2(x, y)));
}
