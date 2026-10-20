using System.Net;
using System.Text;
using System.Text.Json;
using LudumDare60.Engine.Platform.Desktop.Telemetry;

namespace LudumDare60.Engine.Platform.Desktop.Tests.Telemetry;

public sealed class GameAnalyticsClientTests
{
    private const string GameKey = "5c6bcb5402204249437fb5a7a80a4959";
    private const string SecretKey = "16813a12f718bc5c620f56944e1abc3ea13ccbac";

    private readonly List<(string Path, string Body, string? Authorization)> _requests = [];
    private readonly Queue<Func<HttpResponseMessage>> _replies = new();
    private readonly AnalyticsState _state = AnalyticsState.Create();
    private int _saves;
    private DateTimeOffset _now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private GameAnalyticsClient CreateClient() => new(
        new AnalyticsConfig(GameKey, SecretKey, "1.2.0"),
        _state,
        _ => _saves++,
        SendAsync,
        GameAnalyticsProtocol.SandboxApi,
        new AnalyticsDevice("linux", "linux 6.6.0"),
        () => _now,
        "vulkan");

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        request.Headers.TryGetValues("Authorization", out var authorization);
        _requests.Add((request.RequestUri!.AbsolutePath, body, authorization?.Single()));
        return _replies.Count > 0 ? _replies.Dequeue()() : Reply(HttpStatusCode.OK, "{}");
    }

    private static HttpResponseMessage Reply(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private void ReplyInit(long serverTime = 1_800_000_000, bool enabled = true)
        => _replies.Enqueue(() => Reply(HttpStatusCode.OK, $$"""{"enabled":{{(enabled ? "true" : "false")}},"server_ts":{{serverTime}},"flags":[]}"""));

    private List<JsonElement> SentEvents()
    {
        var events = new List<JsonElement>();
        foreach (var request in _requests.Where(static r => r.Path.EndsWith("/events", StringComparison.Ordinal)))
        {
            events.AddRange(JsonDocument.Parse(request.Body).RootElement.EnumerateArray().Select(static e => e.Clone()));
        }

        return events;
    }

    [Fact]
    public async Task FlushAsync_FirstTime_InitsThenSendsTheSessionStart()
    {
        var client = CreateClient();
        ReplyInit();

        await client.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Equal([$"/v2/{GameKey}/init", $"/v2/{GameKey}/events"], _requests.Select(static r => r.Path), StringComparer.Ordinal);
        Assert.Equal(GameAnalyticsProtocol.Sign(Encoding.UTF8.GetBytes(_requests[0].Body), SecretKey), _requests[0].Authorization);
        Assert.Equal(GameAnalyticsProtocol.Sign(Encoding.UTF8.GetBytes(_requests[1].Body), SecretKey), _requests[1].Authorization);
        var start = Assert.Single(SentEvents());
        Assert.Equal("user", start.GetProperty("category").GetString());
        Assert.Equal("vulkan", start.GetProperty("custom_02").GetString());
        Assert.Equal(0, client.QueuedCount);
    }

    [Fact]
    public void Constructor_CountsASessionAndSavesTheState()
    {
        var client = CreateClient();

        Assert.Equal(1, client.Session.SessionNumber);
        Assert.Equal(_state.UserId, client.Session.UserId);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public async Task Events_CarryIdsAttemptsAndPointerType()
    {
        var client = CreateClient();
        ReplyInit();
        client.SetPointerType(PointerType.Touch);

        client.Progression(ProgressionStatus.Start, "Order", "hoodie", null, null);
        client.Progression(ProgressionStatus.Complete, "Order", "hoodie", null, 80);
        client.Design("Order:hoodie:Clean", 4.5);
        await client.FlushAsync(TestContext.Current.CancellationToken);

        var events = SentEvents();
        Assert.Equal("Start:Order:hoodie", events[1].GetProperty("event_id").GetString());
        Assert.False(events[1].TryGetProperty("attempt_num", out _));
        Assert.Equal("Complete:Order:hoodie", events[2].GetProperty("event_id").GetString());
        Assert.Equal(1, events[2].GetProperty("attempt_num").GetInt32());
        Assert.Equal(80, events[2].GetProperty("score").GetInt32());
        Assert.Equal("touch", events[2].GetProperty("custom_01").GetString());
        Assert.Equal(4.5, events[3].GetProperty("value").GetDouble());
    }

    [Fact]
    public void InvalidEvents_AreDropped()
    {
        var client = CreateClient();

        client.Design("a:b:c:d:e:f", null);
        client.Progression(ProgressionStatus.Start, "Order", null, "Clean", null);

        Assert.Equal(1, client.QueuedCount);
    }

    [Fact]
    public async Task FlushAsync_ServerTimeDiffers_AdjustsClientTime()
    {
        var client = CreateClient();
        ReplyInit(serverTime: 1_800_000_100);

        await client.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1_800_000_100, SentEvents()[0].GetProperty("client_ts").GetInt64());
    }

    [Fact]
    public async Task FlushAsync_Offline_KeepsTheEventsForLater()
    {
        var client = CreateClient();
        ReplyInit();
        _replies.Enqueue(() => throw new HttpRequestException("offline"));
        client.Design("Game:Ready", null);

        await client.FlushAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, client.QueuedCount);

        await client.FlushAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, client.QueuedCount);
        Assert.Single(_requests, static r => r.Path.EndsWith("/init", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FlushAsync_ServerError_KeepsTheEvents()
    {
        var client = CreateClient();
        ReplyInit();
        _replies.Enqueue(() => Reply(HttpStatusCode.ServiceUnavailable, ""));

        await client.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, client.QueuedCount);
        Assert.False(client.IsOff);
    }

    [Fact]
    public async Task FlushAsync_EventsRejected_DropsThem()
    {
        var client = CreateClient();
        ReplyInit();
        _replies.Enqueue(() => Reply(HttpStatusCode.BadRequest, """[{"errors":[]}]"""));

        await client.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, client.QueuedCount);
        Assert.False(client.IsOff);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task FlushAsync_KeysRejected_TurnsOff(HttpStatusCode status)
    {
        var client = CreateClient();
        _replies.Enqueue(() => Reply(status, ""));

        await client.FlushAsync(TestContext.Current.CancellationToken);
        client.Design("Game:Ready", null);
        await client.FlushAsync(TestContext.Current.CancellationToken);

        Assert.True(client.IsOff);
        Assert.Equal(0, client.QueuedCount);
        Assert.Single(_requests);
    }

    [Fact]
    public async Task FlushAsync_GameDisabled_TurnsOff()
    {
        var client = CreateClient();
        ReplyInit(enabled: false);

        await client.FlushAsync(TestContext.Current.CancellationToken);

        Assert.True(client.IsOff);
    }

    [Fact]
    public void Stop_SendsSessionEndWithItsLength()
    {
        var client = CreateClient();
        ReplyInit();
        _now += TimeSpan.FromSeconds(95);

        client.Stop(TimeSpan.FromSeconds(5));

        var end = SentEvents()[^1];
        Assert.Equal("session_end", end.GetProperty("category").GetString());
        Assert.Equal(95, end.GetProperty("length").GetInt64());
    }

    [Fact]
    public void Stop_LaterEvents_AreDropped()
    {
        var client = CreateClient();
        ReplyInit();
        client.Stop(TimeSpan.FromSeconds(5));

        client.Design("Game:Ready", null);

        Assert.Equal(0, client.QueuedCount);
    }

    [Fact]
    public void Fatal_Twice_RecordsOnceAndCutsLongMessages()
    {
        var client = CreateClient();

        client.Fatal(new string('x', GameAnalyticsClient.MaxErrorLength + 10));
        client.Fatal("second");
        client.Stop(TimeSpan.FromSeconds(5));

        var errors = SentEvents().Where(static e => string.Equals(e.GetProperty("category").GetString(), "error", StringComparison.Ordinal)).ToList();
        Assert.Single(errors);
        Assert.Equal(GameAnalyticsClient.MaxErrorLength, errors[0].GetProperty("message").GetString()!.Length);
    }

    [Fact]
    public void Queue_BeyondItsLimit_DropsTheOldest()
    {
        var client = CreateClient();
        for (var i = 0; i < GameAnalyticsClient.MaxQueued + 5; i++)
        {
            client.Design("Game:Ready", i);
        }

        Assert.Equal(GameAnalyticsClient.MaxQueued, client.QueuedCount);
    }
}
