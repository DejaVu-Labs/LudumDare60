using System.Text;
using System.Text.Json;
using LudumDare60.Engine.Platform.Desktop.Telemetry;

namespace LudumDare60.Engine.Platform.Desktop.Tests.Telemetry;

public sealed class GameAnalyticsProtocolTests
{
    private static readonly AnalyticsDevice s_device = new("linux", "linux 6.6.119");

    private static readonly AnalyticsSession s_session = new(
        "6c8c2b4e-8d1e-4a43-9d6a-3f0c7a7b1e21", "b0b7e3f4-1d2c-4c5b-8e9f-0a1b2c3d4e5f", 3, "1.2.0", s_device, "x64");

    private static readonly DateTimeOffset s_time = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Fact]
    public void Sign_MatchesHmacSha256InBase64()
        => Assert.Equal(
            "WyrzSGzDxs0GnK6Nm8RusI+6BnY28jQ4EHGGULs0E48=",
            GameAnalyticsProtocol.Sign("""{"platform":"linux"}"""u8, "16813a12f718bc5c620f56944e1abc3ea13ccbac"));

    [Fact]
    public void InitBody_HasPlatformOsAndSdkVersion()
        => Assert.Equal(
            """{"platform":"linux","os_version":"linux 6.6.119","sdk_version":"rest api v2"}""",
            Encoding.UTF8.GetString(GameAnalyticsProtocol.InitBody(s_device)));

    [Theory]
    [InlineData("""{"enabled":true,"server_ts":1800000123,"flags":[]}""", true, 1_800_000_123L)]
    [InlineData("""{"enabled":false,"server_ts":1}""", false, 1L)]
    [InlineData("""{"server_ts":"x"}""", true, 0L)]
    [InlineData("[]", false, 0L)]
    public void ReadInitReply_ReadsEnabledAndServerTime(string reply, bool enabled, long serverTime)
        => Assert.Equal((enabled, serverTime), GameAnalyticsProtocol.ReadInitReply(Encoding.UTF8.GetBytes(reply)));

    [Fact]
    public void EventsBody_EveryEvent_CarriesTheDefaultAnnotations()
    {
        using var body = Parse(new AnalyticsEvent("user", s_time, null));
        var e = body.RootElement[0];

        Assert.Equal("user", e.GetProperty("category").GetString());
        Assert.Equal(2, e.GetProperty("v").GetInt32());
        Assert.Equal(s_session.UserId, e.GetProperty("user_id").GetString());
        Assert.Equal(1_800_000_000, e.GetProperty("client_ts").GetInt64());
        Assert.Equal("rest api v2", e.GetProperty("sdk_version").GetString());
        Assert.Equal("linux 6.6.119", e.GetProperty("os_version").GetString());
        Assert.Equal("unknown", e.GetProperty("manufacturer").GetString());
        Assert.Equal("x64", e.GetProperty("device").GetString());
        Assert.Equal("linux", e.GetProperty("platform").GetString());
        Assert.Equal(s_session.SessionId, e.GetProperty("session_id").GetString());
        Assert.Equal(3, e.GetProperty("session_num").GetInt32());
        Assert.Equal("1.2.0", e.GetProperty("build").GetString());
        Assert.False(e.TryGetProperty("custom_01", out _));
    }

    [Fact]
    public void EventsBody_ClockOffset_MovesClientTime()
    {
        using var body = Parse(TimeSpan.FromSeconds(-30), new AnalyticsEvent("user", s_time, null));

        Assert.Equal(1_800_000_000 - 30, body.RootElement[0].GetProperty("client_ts").GetInt64());
    }

    [Fact]
    public void EventsBody_EachCategory_HasItsFields()
    {
        using var body = Parse(
            new AnalyticsEvent("progression", s_time, "touch") { EventId = "Complete:Order:hoodie", AttemptNumber = 2, Score = 90 },
            new AnalyticsEvent("design", s_time, "mouse") { EventId = "Order:hoodie:Clean", Value = 12.5 },
            new AnalyticsEvent("design", s_time, null) { EventId = "Game:Ready", Value = double.NaN },
            new AnalyticsEvent("error", s_time, null) { Message = "Boom" },
            new AnalyticsEvent("session_end", s_time, null) { Length = 75 });
        var events = body.RootElement;

        Assert.Equal("Complete:Order:hoodie", events[0].GetProperty("event_id").GetString());
        Assert.Equal(2, events[0].GetProperty("attempt_num").GetInt32());
        Assert.Equal(90, events[0].GetProperty("score").GetInt32());
        Assert.Equal("touch", events[0].GetProperty("custom_01").GetString());
        Assert.Equal(12.5, events[1].GetProperty("value").GetDouble());
        Assert.False(events[2].TryGetProperty("value", out _));
        Assert.Equal("critical", events[3].GetProperty("severity").GetString());
        Assert.Equal("Boom", events[3].GetProperty("message").GetString());
        Assert.Equal(75, events[4].GetProperty("length").GetInt64());
    }

    [Theory]
    [InlineData("windows", 10, 0, 26100, 0, "windows 10.0.26100")]
    [InlineData("linux", 6, 6, 119, 9, "linux 6.6.119")]
    [InlineData("mac_osx", 15, 1, -1, -1, "mac_osx 15.1.0")]
    [InlineData("linux", 123456, 0, 0, 0, "linux 99999.0.0")]
    public void GetOsVersion_KeepsThreeNumbersOfAtMostFiveDigits(string platform, int major, int minor, int build, int revision, string expected)
    {
        var version = build < 0 ? new Version(major, minor) : revision < 0 ? new Version(major, minor, build) : new Version(major, minor, build, revision);

        Assert.Equal(expected, AnalyticsDevice.GetOsVersion(platform, version));
    }

    private static JsonDocument Parse(params AnalyticsEvent[] events) => Parse(TimeSpan.Zero, events);

    private static JsonDocument Parse(TimeSpan offset, params AnalyticsEvent[] events)
        => JsonDocument.Parse(GameAnalyticsProtocol.EventsBody(events, s_session, offset));
}
