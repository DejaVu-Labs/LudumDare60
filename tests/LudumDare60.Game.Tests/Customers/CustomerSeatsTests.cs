using LudumDare60.Game.Customers;

namespace LudumDare60.Game.Tests.Customers;

public sealed class CustomerSeatsTests
{
    [Fact]
    public void TryGetWaitingSeat_Robe_SitsUnderThePainting()
    {
        Assert.True(CustomerSeats.TryGetWaitingSeat("robe", out var seat));
        Assert.Equal(CustomerSeats.UnderThePainting, seat);
    }

    [Theory]
    [InlineData("hoodie")]
    [InlineData("work-jacket")]
    [InlineData("headphones")]
    [InlineData("raincoat")]
    [InlineData("unknown")]
    public void TryGetWaitingSeat_LookThatStands_ReturnsFalse(string look)
        => Assert.False(CustomerSeats.TryGetWaitingSeat(look, out _));

    [Fact]
    public void UnderThePainting_IsBehindTheCounterAndSmaller()
    {
        // The seat is at the back wall: above the far edge of the bench and drawn smaller than a customer at the counter.
        Assert.True(CustomerSeats.UnderThePainting.Bottom.Y <= Shop.CustomerBottom);
        Assert.True(CustomerSeats.UnderThePainting.Bottom.X < Shop.CounterX - Shop.CustomerHalfWidth);
        Assert.InRange(CustomerSeats.UnderThePainting.Scale, 0.5f, LudumDare60Game.CustomerScale - 0.1f);
    }
}
