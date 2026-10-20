using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// <see cref="IAnalytics"/> on the GameAnalytics REST API v2: the desktop twin of the browser's <c>analytics.js</c>,
/// which uses the GameAnalytics JavaScript SDK. Calls only queue the event; a background task signs in
/// (<c>init</c>) and sends the queue every <see cref="FlushInterval"/>, so a frame never waits for the network.
/// </summary>
/// <remarks>A session starts with the client (a <c>user</c> event) and ends with <see cref="Stop"/> (a
/// <c>session_end</c> event with its length). Events that fail to send for network reasons stay queued; events the
/// server rejects are dropped; rejected keys (401) or <c>enabled: false</c> turn analytics off for the run. Calls never
/// throw.</remarks>
internal sealed class GameAnalyticsClient : IAnalytics
{
    /// <summary>
    /// How often queued events are sent, as in the GameAnalytics SDKs.
    /// </summary>
    public static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Events kept while they cannot be sent; older ones are dropped beyond it.
    /// </summary>
    public const int MaxQueued = 500;

    /// <summary>
    /// Events per request.
    /// </summary>
    public const int MaxBatch = 100;

    /// <summary>
    /// Longest error message sent, as <c>MAX_ERROR_LENGTH</c> in <c>analytics.js</c>.
    /// </summary>
    public const int MaxErrorLength = 8192;

    private readonly AnalyticsConfig _config;
    private readonly AnalyticsState _state;
    private readonly Action<AnalyticsState> _saveState;
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;
    private readonly Uri _api;
    private readonly Func<DateTimeOffset> _now;
    private readonly DateTimeOffset _sessionStart;
    private readonly List<AnalyticsEvent> _queue = [];
    private readonly Lock _lock = new();
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _flushing;
    private string? _pointerType;
    private bool _initialized;
    private bool _off;
    private bool _fatalSent;
    private bool _ended;
    private TimeSpan _clockOffset;
    private Task? _loop;

    /// <param name="config">The keys and the build.</param>
    /// <param name="state">The player's state; the client counts a session in it.</param>
    /// <param name="saveState">Stores the state after it changed. Must not throw.</param>
    /// <param name="send">Sends a request (<see cref="HttpClient.SendAsync(HttpRequestMessage, CancellationToken)"/>
    /// in the game).</param>
    /// <param name="api">The API root, <see cref="GameAnalyticsProtocol.ProductionApi"/> in the game.</param>
    /// <param name="device">The platform and OS version.</param>
    /// <param name="now">The clock.</param>
    public GameAnalyticsClient(
        AnalyticsConfig config,
        AnalyticsState state,
        Action<AnalyticsState> saveState,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send,
        Uri api,
        AnalyticsDevice device,
        Func<DateTimeOffset> now)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(device);
        _config = config;
        _state = state;
        _saveState = saveState;
        _send = send;
        _api = api;
        _now = now;
        Session = new AnalyticsSession(
            state.UserId,
            Guid.NewGuid().ToString("D"),
            state.StartSession(),
            config.Build,
            device,
            AnalyticsDevice.Device);
        saveState(state);
        _sessionStart = now();
        _queue.Add(new AnalyticsEvent("user", _sessionStart, null));
    }

    public AnalyticsSession Session { get; }

    /// <summary>
    /// Whether analytics were turned off for the run (rejected keys, or disabled for the game).
    /// </summary>
    public bool IsOff
    {
        get
        {
            lock (_lock)
            {
                return _off;
            }
        }
    }

    /// <summary>
    /// Events waiting to be sent.
    /// </summary>
    public int QueuedCount
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    public void Progression(ProgressionStatus status, string progression01, string? progression02, string? progression03, int? score)
    {
        if (!AnalyticsIds.IsValidProgression(status, progression01, progression02, progression03))
        {
            Warn($"invalid progression event dropped: {status} {progression01}:{progression02}:{progression03}");
            return;
        }

        var progression = progression03 is not null
            ? $"{progression01}:{progression02}:{progression03}"
            : progression02 is not null ? $"{progression01}:{progression02}" : progression01;
        lock (_lock)
        {
            if (_off || _ended)
            {
                return;
            }

            var attempt = _state.CountProgression(status, progression);
            Enqueue(new AnalyticsEvent("progression", _now(), _pointerType)
            {
                EventId = $"{status}:{progression}",
                AttemptNumber = attempt,
                Score = score,
            });
        }

        _saveState(_state);
    }

    public void Design(string eventId, double? value)
    {
        if (!AnalyticsIds.IsValidDesignId(eventId))
        {
            Warn($"invalid design event id dropped: {eventId}");
            return;
        }

        lock (_lock)
        {
            if (!_off && !_ended)
            {
                Enqueue(new AnalyticsEvent("design", _now(), _pointerType) { EventId = eventId, Value = value });
            }
        }
    }

    /// <summary>
    /// Records the error that ends the run, once per run, as <c>fatal</c> in <c>analytics.js</c>.
    /// </summary>
    public void Fatal(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_lock)
        {
            if (_off || _ended || _fatalSent)
            {
                return;
            }

            _fatalSent = true;
            var text = message.Length > MaxErrorLength ? message[..MaxErrorLength] : message;
            Enqueue(new AnalyticsEvent("error", _now(), _pointerType) { Message = text });
        }
    }

    /// <summary>
    /// Sets the custom dimension of later events to the device of the last press, as <c>setPointerType</c> in
    /// <c>analytics.js</c>.
    /// </summary>
    public void SetPointerType(PointerType type)
    {
        var name = type switch
        {
            PointerType.Touch => "touch",
            PointerType.Pen => "pen",
            _ => "mouse",
        };
        lock (_lock)
        {
            _pointerType = name;
        }
    }

    /// <summary>
    /// Starts sending in the background.
    /// </summary>
    public void Start()
    {
        _loop ??= Task.Run(RunAsync);
    }

    /// <summary>
    /// Ends the session: queues <c>session_end</c>, stops the background task and sends what is queued, waiting at most
    /// <paramref name="timeout"/>. Later events are dropped.
    /// </summary>
    public void Stop(TimeSpan timeout)
    {
        lock (_lock)
        {
            if (_ended)
            {
                return;
            }

            if (!_off)
            {
                var now = _now();
                Enqueue(new AnalyticsEvent("session_end", now, _pointerType)
                {
                    Length = Math.Max(0, (long)(now - _sessionStart).TotalSeconds),
                });
            }

            _ended = true;
        }

        _stopped.TrySetResult();
        using var timeoutSource = new CancellationTokenSource(timeout);
        try
        {
            // The loop ends after the flush it may be in; then one last flush sends session_end.
            _loop?.Wait(timeout);
            FlushAsync(timeoutSource.Token).Wait(timeout);
        }
        catch (AggregateException)
        {
            // The last send failed or timed out: those events are lost, the game is closing anyway.
        }
    }

    /// <summary>
    /// Signs in if needed, then sends every queued event. Does nothing while another flush runs.
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _flushing, 1) == 1)
        {
            return;
        }

        try
        {
            if (IsOff || (!_initialized && !await InitializeAsync(cancellationToken).ConfigureAwait(false)))
            {
                return;
            }

            while (true)
            {
                AnalyticsEvent[] batch;
                lock (_lock)
                {
                    if (_off || _queue.Count == 0)
                    {
                        return;
                    }

                    batch = [.. _queue.Take(MaxBatch)];
                }

                var sent = await SendEventsAsync(batch, cancellationToken).ConfigureAwait(false);
                if (!sent)
                {
                    return;
                }

                lock (_lock)
                {
                    _queue.RemoveRange(0, Math.Min(batch.Length, _queue.Count));
                }
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException)
        {
            // Offline or timed out: the events stay queued for the next flush.
        }
        finally
        {
            Volatile.Write(ref _flushing, 0);
        }
    }

    private async Task RunAsync()
    {
        while (!_stopped.Task.IsCompleted)
        {
            await FlushAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAny(Task.Delay(FlushInterval), _stopped.Task).ConfigureAwait(false);
        }
    }

    private async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        using var response = await PostAsync("init", GameAnalyticsProtocol.InitBody(Session.Device), cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            TurnOff($"init rejected the keys ({(int)response.StatusCode})");
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var reply = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var (enabled, serverTime) = GameAnalyticsProtocol.ReadInitReply(reply);
        if (!enabled)
        {
            TurnOff("disabled for this game");
            return false;
        }

        _clockOffset = serverTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(serverTime) - _now() : TimeSpan.Zero;
        _initialized = true;
        return true;
    }

    /// <returns>Whether the batch is done with: sent, or rejected for good.</returns>
    private async Task<bool> SendEventsAsync(AnalyticsEvent[] batch, CancellationToken cancellationToken)
    {
        var body = GameAnalyticsProtocol.EventsBody(batch, Session, _clockOffset);
        using var response = await PostAsync("events", body, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                TurnOff($"events rejected the keys ({(int)response.StatusCode})");
                return false;
            case HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge:
                var reason = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                Warn($"{batch.Length} events rejected ({(int)response.StatusCode}): {reason}");
                return true;
            default:
                // Server trouble: try again later.
                return false;
        }
    }

    private async Task<HttpResponseMessage> PostAsync(string endpoint, byte[] body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_api, $"{_config.GameKey}/{endpoint}"));
        request.Content = new ByteArrayContent(body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.TryAddWithoutValidation("Authorization", GameAnalyticsProtocol.Sign(body, _config.SecretKey));
        return await _send(request, cancellationToken).ConfigureAwait(false);
    }

    private void Enqueue(AnalyticsEvent analyticsEvent)
    {
        if (_queue.Count >= MaxQueued)
        {
            _queue.RemoveAt(0);
        }

        _queue.Add(analyticsEvent);
    }

    private void TurnOff(string reason)
    {
        lock (_lock)
        {
            _off = true;
            _queue.Clear();
        }

        Warn($"GameAnalytics turned off: {reason}");
    }

    private static void Warn(string message)
        => Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[analytics] {message}"));
}
