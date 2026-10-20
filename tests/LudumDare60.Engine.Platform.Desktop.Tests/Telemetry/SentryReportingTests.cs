using LudumDare60.Engine.Platform.Desktop.Telemetry;

namespace LudumDare60.Engine.Platform.Desktop.Tests.Telemetry;

public sealed class SentryReportingTests
{
    [Fact]
    public void Configure_SetsProjectAndSendsNoPersonalData()
    {
        var options = new SentryOptions();

        SentryReporting.Configure(options, new ReportingConfig("https://key@o1.ingest.sentry.io/2", "1.2.0", "dev"), "linux", new ErrorBudget(10));

        Assert.Equal("https://key@o1.ingest.sentry.io/2", options.Dsn);
        Assert.Equal("1.2.0", options.Release);
        Assert.Equal("dev", options.Environment);
        Assert.False(options.SendDefaultPii);
        Assert.False(options.IsEnvironmentUser);
        Assert.Equal("linux", options.ServerName);
        Assert.False(options.AutoSessionTracking);
        Assert.Equal("linux", options.DefaultTags["game.platform"]);
    }

    [Fact]
    public void Capture_NotStarted_DoesNothing()
    {
        var exception = new InvalidOperationException("x");

        SentryReporting.Capture(exception, handled: true);

        Assert.Empty(exception.Data);
    }
}
