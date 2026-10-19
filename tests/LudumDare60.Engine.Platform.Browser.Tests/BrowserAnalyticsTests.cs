namespace LudumDare60.Engine.Platform.Browser.Tests;

public sealed class BrowserAnalyticsTests
{
    private readonly List<object?[]> _progressions = [];
    private readonly List<object?[]> _designs = [];

    private BrowserAnalytics Create() => new(
        (status, p1, p2, p3, hasScore, score) => _progressions.Add([status, p1, p2, p3, hasScore, score]),
        (eventId, hasValue, value) => _designs.Add([eventId, hasValue, value]));

    [Fact]
    public void Progression_WithScore_PassesStatusLevelsAndScore()
    {
        Create().Progression(ProgressionStatus.Complete, "Order", "hoodie", null, 74);

        Assert.Equal([[2, "Order", "hoodie", null, true, 74]], _progressions);
    }

    [Fact]
    public void Progression_WithoutScore_PassesNoScore()
    {
        Create().Progression(ProgressionStatus.Start, "Order", "hoodie", "step", null);

        Assert.Equal([[1, "Order", "hoodie", "step", false, 0]], _progressions);
    }

    [Fact]
    public void Design_WithValue_PassesIt()
    {
        Create().Design("Order:hoodie:HalfCock", 3.5);

        Assert.Equal([["Order:hoodie:HalfCock", true, 3.5]], _designs);
    }

    [Fact]
    public void Design_WithoutValue_PassesNoValue()
    {
        Create().Design("Game:Ready", null);

        Assert.Equal([["Game:Ready", false, 0.0]], _designs);
    }
}
