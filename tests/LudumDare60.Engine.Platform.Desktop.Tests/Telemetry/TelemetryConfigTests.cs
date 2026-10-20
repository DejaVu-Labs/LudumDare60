using LudumDare60.Engine.Platform.Desktop.Telemetry;

namespace LudumDare60.Engine.Platform.Desktop.Tests.Telemetry;

public sealed class TelemetryConfigTests
{
    private const string GameKey = "5c6bcb5402204249437fb5a7a80a4959";
    private const string SecretKey = "16813a12f718bc5c620f56944e1abc3ea13ccbac";

    [Fact]
    public void ReadReporting_FullConfig_ReadsEveryField()
        => Assert.Equal(
            new ReportingConfig("https://key@o1.ingest.sentry.io/2", "1.2.0", "release"),
            TelemetryConfig.ReadReporting("""{ "dsn": "https://key@o1.ingest.sentry.io/2", "release": "1.2.0", "environment": "release" }"""));

    [Fact]
    public void ReadReporting_OnlyDsn_DefaultsLikeReportingJs()
        => Assert.Equal(
            new ReportingConfig("https://key@o1.ingest.sentry.io/2", "unknown", "production"),
            TelemetryConfig.ReadReporting("""{ "dsn": "https://key@o1.ingest.sentry.io/2", "release": "" }"""));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("""{ "dsn": 5 }""")]
    [InlineData("""{ "dsn": "o1.ingest.sentry.io/2" }""")]
    [InlineData("""{ "dsn": "http://key@o1.ingest.sentry.io/2" }""")]
    [InlineData("""{ "dsn": "https://o1.ingest.sentry.io/2" }""")]
    public void ReadReporting_NoUsableDsn_IsOff(string? json)
        => Assert.Null(TelemetryConfig.ReadReporting(json));

    [Fact]
    public void ReadAnalytics_ValidKeys_ReadsThem()
        => Assert.Equal(
            new AnalyticsConfig(GameKey, SecretKey, "dev-42-abc1234"),
            TelemetryConfig.ReadAnalytics($$"""{ "gameKey": "{{GameKey}}", "secretKey": "{{SecretKey}}", "build": "dev-42-abc1234" }"""));

    [Theory]
    [InlineData("")]
    [InlineData("""
                , "build": "this build name is much longer than thirty-two"
                """)]
    [InlineData("""
                , "build": 3
                """)]
    public void ReadAnalytics_MissingOrBadBuild_IsUnknown(string extra)
        => Assert.Equal("unknown", TelemetryConfig.ReadAnalytics($$"""{ "gameKey": "{{GameKey}}", "secretKey": "{{SecretKey}}"{{extra}} }""")?.Build);

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "gameKey": "5c6bcb5402204249437fb5a7a80a495", "secretKey": "16813a12f718bc5c620f56944e1abc3ea13ccbac" }""")]
    [InlineData("""{ "gameKey": "5c6bcb5402204249437fb5a7a80a4959", "secretKey": "16813a12f718bc5c620f56944e1abc3ea13ccba!" }""")]
    [InlineData("""{ "gameKey": "5c6bcb5402204249437fb5a7a80a4959" }""")]
    public void ReadAnalytics_BadKeys_IsOff(string json)
        => Assert.Null(TelemetryConfig.ReadAnalytics(json));

    [Fact]
    public void ReadFile_Missing_ReturnsNull()
        => Assert.Null(TelemetryConfig.ReadFile(AppContext.BaseDirectory, "no-such-config.json"));
}
