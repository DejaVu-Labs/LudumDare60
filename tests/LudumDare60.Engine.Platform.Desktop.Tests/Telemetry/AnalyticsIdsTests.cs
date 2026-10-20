using LudumDare60.Engine.Platform.Desktop.Telemetry;

namespace LudumDare60.Engine.Platform.Desktop.Tests.Telemetry;

public sealed class AnalyticsIdsTests
{
    [Theory]
    [InlineData("Game:Ready", true)]
    [InlineData("Order:hoodie:HalfCock", true)]
    [InlineData("a:b:c:d:e", true)]
    [InlineData("Tool (brush)!? ok_1.2-x", true)]
    [InlineData("a:b:c:d:e:f", false)]
    [InlineData("Order::Done", false)]
    [InlineData("", false)]
    [InlineData("Order:ü", false)]
    [InlineData("Order:a/b", false)]
    [InlineData(null, false)]
    public void IsValidDesignId_FollowsGameAnalyticsRules(string? eventId, bool expected)
        => Assert.Equal(expected, AnalyticsIds.IsValidDesignId(eventId));

    [Fact]
    public void IsValidDesignId_PartOf64Characters_IsValidAnd65IsNot()
    {
        Assert.True(AnalyticsIds.IsValidDesignId(new string('a', 64)));
        Assert.False(AnalyticsIds.IsValidDesignId(new string('a', 65)));
    }

    [Theory]
    [InlineData(ProgressionStatus.Start, "Order", null, null, true)]
    [InlineData(ProgressionStatus.Complete, "Order", "hoodie", null, true)]
    [InlineData(ProgressionStatus.Fail, "Order", "hoodie", "Clean", true)]
    [InlineData(ProgressionStatus.None, "Order", null, null, false)]
    [InlineData(ProgressionStatus.Start, "Order", null, "Clean", false)]
    [InlineData(ProgressionStatus.Start, "", null, null, false)]
    [InlineData(ProgressionStatus.Start, "Or:der", null, null, false)]
    public void IsValidProgression_FollowsGameAnalyticsRules(ProgressionStatus status, string? p1, string? p2, string? p3, bool expected)
        => Assert.Equal(expected, AnalyticsIds.IsValidProgression(status, p1, p2, p3));
}
