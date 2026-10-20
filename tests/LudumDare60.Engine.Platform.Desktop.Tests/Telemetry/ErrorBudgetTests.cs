using LudumDare60.Engine.Platform.Desktop.Telemetry;

namespace LudumDare60.Engine.Platform.Desktop.Tests.Telemetry;

public sealed class ErrorBudgetTests
{
    [Fact]
    public void TryTake_SameErrorTwice_LetsItThroughOnce()
    {
        var budget = new ErrorBudget(10);

        Assert.True(budget.TryTake("A: x"));
        Assert.False(budget.TryTake("A: x"));
        Assert.True(budget.TryTake("A: y"));
    }

    [Fact]
    public void TryTake_BeyondCapacity_StopsLettingThrough()
    {
        var budget = new ErrorBudget(2);

        Assert.True(budget.TryTake("1"));
        Assert.True(budget.TryTake("2"));
        Assert.False(budget.TryTake("3"));
    }
}
