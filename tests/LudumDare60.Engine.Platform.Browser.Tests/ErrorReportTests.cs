namespace LudumDare60.Engine.Platform.Browser.Tests;

public sealed class ErrorReportTests
{
    private static T Thrown<T>(T exception)
        where T : Exception
    {
        try
        {
            throw exception;
        }
        catch (T caught)
        {
            return caught;
        }
    }

    [Fact]
    public void From_Null_Throws() => Assert.Throws<ArgumentNullException>(() => ErrorReport.From(null!));

    [Fact]
    public void From_ThrownException_TypeMessageStackAndDetails()
    {
        var exception = Thrown(new InvalidOperationException("No customer seat."));

        var report = ErrorReport.From(exception);

        Assert.Equal("System.InvalidOperationException", report.Type);
        Assert.Equal("No customer seat.", report.Message);
        Assert.Contains(nameof(Thrown), report.StackTrace, StringComparison.Ordinal);
        Assert.Equal(exception.ToString(), report.Details);
    }

    [Fact]
    public void From_NeverThrown_NoStackTrace()
    {
        var report = ErrorReport.From(new AssetLoadException("gun.png", 404, "Reading 'gun.png' failed."));

        Assert.Equal("LudumDare60.Engine.Platform.AssetLoadException", report.Type);
        Assert.Null(report.StackTrace);
    }

    [Fact]
    public void From_AggregateWithOneInner_DescribesInnerButKeepsFullDetails()
    {
        var inner = Thrown(new FileNotFoundException("level.txt"));
        var aggregate = new AggregateException(inner);

        var report = ErrorReport.From(aggregate);

        Assert.Equal("System.IO.FileNotFoundException", report.Type);
        Assert.Equal("level.txt", report.Message);
        Assert.Equal(aggregate.ToString(), report.Details);
    }

    [Fact]
    public void From_AggregateWithSeveralInner_DescribesAggregate()
    {
        var aggregate = new AggregateException("Unloading assets failed.", new IOException("a"), new IOException("b"));

        var report = ErrorReport.From(aggregate);

        Assert.Equal("System.AggregateException", report.Type);
        Assert.StartsWith("Unloading assets failed.", report.Message, StringComparison.Ordinal);
    }
}
