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

    [Fact]
    public void TryGetWaitingSeat_DogWalker_StandsOutsideTheWindow()
    {
        Assert.True(CustomerSeats.TryGetWaitingSeat("dog-walker", out var seat));
        Assert.Equal(CustomerSeats.OutsideTheWindow, seat);
        Assert.True(seat.IsOutside);
    }

    [Theory]
    [InlineData("hoodie")]
    [InlineData("headphones")]
    [InlineData("parcel")]
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
        Assert.False(CustomerSeats.UnderThePainting.IsOutside);
    }

    [Fact]
    public void TryGetFixedPlace_Parcel_LiesOnTheCounter()
    {
        Assert.True(CustomerSeats.TryGetFixedPlace("parcel", out var place));
        Assert.Equal(CustomerSeats.OnTheCounter, place);
        Assert.False(place.IsOutside);
    }

    [Theory]
    [InlineData("hoodie")]
    [InlineData("robe")]
    [InlineData("dog-walker")]
    [InlineData("headphones")]
    public void TryGetFixedPlace_CustomerWhoWalksIn_ReturnsFalse(string look)
        => Assert.False(CustomerSeats.TryGetFixedPlace(look, out _));

    [Fact]
    public void OnTheCounter_IsRightOfTheCounterMiddleOnTheCounterTop()
    {
        // The counter's top runs from about y = 140 to 157 and ends on the right at about x = 478, next to the door.
        var place = CustomerSeats.OnTheCounter;
        Assert.InRange(place.Bottom.X, Shop.CounterX + Shop.CustomerHalfWidth, 478 - (place.Scale * CustomerSheet.FrameWidth / 2) + 10);
        Assert.InRange(place.Bottom.Y, 140, 157);
    }

    [Fact]
    public void OutsideTheWindow_StandsBehindTheWindowWithTheFeetBelowTheSill()
    {
        // The window runs from about x = 200 to 445, and its sill is at about y = 135: the feet are behind the wall.
        var spot = CustomerSeats.OutsideTheWindow;
        var halfWidth = spot.Scale * CustomerSheet.FrameWidth / 2;
        Assert.InRange(spot.Bottom.X - halfWidth, 200, 445);
        Assert.InRange(spot.Bottom.X + halfWidth, 200, 445);
        Assert.InRange(spot.Bottom.Y, 136, 170);
    }
}
