using LudumDare60.Engine.Platform.Desktop.Telemetry;

namespace LudumDare60.Engine.Platform.Desktop.Tests.Telemetry;

public sealed class AnalyticsStateTests
{
    [Fact]
    public void Read_Written_KeepsUserSessionsAndAttempts()
    {
        var state = AnalyticsState.Create();
        state.StartSession();
        state.StartSession();
        state.CountProgression(ProgressionStatus.Start, "Order:hoodie");

        var read = AnalyticsState.Read(state.Write());

        Assert.Equal(state.UserId, read.UserId);
        Assert.Equal(2, read.SessionCount);
        Assert.Equal(1, read.CountProgression(ProgressionStatus.Fail, "Order:hoodie"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("broken")]
    [InlineData("""{ "userId": "not a guid", "sessions": 4 }""")]
    [InlineData("[1]")]
    public void Read_MissingOrBroken_StartsANewPlayer(string? json)
    {
        var state = AnalyticsState.Read(json);

        Assert.True(Guid.TryParse(state.UserId, out _));
        Assert.Equal(0, state.SessionCount);
    }

    [Fact]
    public void StartSession_CountsFromOne()
    {
        var state = AnalyticsState.Create();

        Assert.Equal(1, state.StartSession());
        Assert.Equal(2, state.StartSession());
    }

    [Fact]
    public void CountProgression_CountsStartsUntilComplete()
    {
        var state = AnalyticsState.Create();

        Assert.Null(state.CountProgression(ProgressionStatus.Start, "Order:a"));
        Assert.Equal(1, state.CountProgression(ProgressionStatus.Fail, "Order:a"));
        Assert.Null(state.CountProgression(ProgressionStatus.Start, "Order:a"));
        Assert.Equal(2, state.CountProgression(ProgressionStatus.Complete, "Order:a"));
        Assert.Null(state.CountProgression(ProgressionStatus.Start, "Order:a"));
        Assert.Equal(1, state.CountProgression(ProgressionStatus.Complete, "Order:a"));
    }

    [Fact]
    public void CountProgression_CompleteWithoutStart_IsAttemptOne()
        => Assert.Equal(1, AnalyticsState.Create().CountProgression(ProgressionStatus.Complete, "Order:b"));
}
