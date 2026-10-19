namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class FrameClockTests
{
    [Fact]
    public void Tick_FirstFrame_ReturnsZero()
        => Assert.Equal(0, new FrameClock().Tick(5_000_000_000));

    [Fact]
    public void Tick_LaterFrames_ReturnSecondsSinceThePreviousOne()
    {
        var clock = new FrameClock();
        clock.Tick(1_000_000_000);

        Assert.Equal(0.016, clock.Tick(1_016_000_000), 9);
        Assert.Equal(0.5, clock.Tick(1_516_000_000), 9);
    }

    [Fact]
    public void Tick_TimeGoingBack_ReturnsZero()
    {
        var clock = new FrameClock();
        clock.Tick(2_000);

        Assert.Equal(0, clock.Tick(1_000));
    }

    [Theory]
    [InlineData(60, 16_666_666UL)]
    [InlineData(144, 6_944_444UL)]
    [InlineData(0, 16_666_666UL)]
    [InlineData(-1, 16_666_666UL)]
    [InlineData(float.NaN, 16_666_666UL)]
    [InlineData(float.PositiveInfinity, 16_666_666UL)]
    public void GetFramePeriodNs_UsesRefreshRateOrSixtyHertz(float refreshRate, ulong expected)
        => Assert.Equal(expected, FrameClock.GetFramePeriodNs(refreshRate));

    [Theory]
    [InlineData(1_000UL, 1_000UL, 100UL, 100UL)]
    [InlineData(1_000UL, 1_040UL, 100UL, 60UL)]
    [InlineData(1_000UL, 1_100UL, 100UL, 0UL)]
    [InlineData(1_000UL, 1_500UL, 100UL, 0UL)]
    public void GetWaitNs_WaitsUntilOnePeriodAfterFrameStart(ulong frameStartNs, ulong nowNs, ulong periodNs, ulong expected)
        => Assert.Equal(expected, FrameClock.GetWaitNs(frameStartNs, nowNs, periodNs));
}
