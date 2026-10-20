using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;

namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// The GameAnalytics REST API v2: request bodies and their signature. See
/// <see href="https://docs.gameanalytics.com/event-tracking-and-integrations/rest-api/"/>.
/// </summary>
internal static class GameAnalyticsProtocol
{
    /// <summary>
    /// The <c>sdk_version</c> the REST API requires from clients without an SDK.
    /// </summary>
    public const string SdkVersion = "rest api v2";

    public static readonly Uri ProductionApi = new("https://api.gameanalytics.com/v2/");

    /// <summary>
    /// The sandbox, which accepts the published sandbox keys and stores nothing.
    /// </summary>
    public static readonly Uri SandboxApi = new("https://sandbox-api.gameanalytics.com/v2/");

    /// <summary>
    /// The <c>Authorization</c> header: the body's HMAC-SHA256 with the secret key, in Base64.
    /// </summary>
    public static string Sign(ReadOnlySpan<byte> body, string secretKey)
    {
        ArgumentNullException.ThrowIfNull(secretKey);
        Span<byte> key = stackalloc byte[secretKey.Length];
        for (var i = 0; i < secretKey.Length; i++)
        {
            key[i] = (byte)secretKey[i];
        }

        Span<byte> hash = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(key, body, hash);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// The body of <c>POST /v2/{game_key}/init</c>.
    /// </summary>
    public static byte[] InitBody(AnalyticsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("platform", device.Platform);
            writer.WriteString("os_version", device.OsVersion);
            writer.WriteString("sdk_version", SdkVersion);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Reads the server time from the reply to <c>init</c>.
    /// </summary>
    /// <returns>Whether the game's analytics are enabled, and the server time in Unix seconds (0 when missing).</returns>
    /// <exception cref="JsonException">The reply is not JSON.</exception>
    public static (bool Enabled, long ServerTime) ReadInitReply(ReadOnlySpan<byte> reply)
    {
        var reader = new Utf8JsonReader(reply);
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return (false, 0);
        }

        var enabled = !root.TryGetProperty("enabled", out var enabledValue) || enabledValue.ValueKind != JsonValueKind.False;
        var serverTime = root.TryGetProperty("server_ts", out var time)
            && time.ValueKind == JsonValueKind.Number
            && time.TryGetInt64(out var seconds) ? seconds : 0;
        return (enabled, serverTime);
    }

    /// <summary>
    /// The body of <c>POST /v2/{game_key}/events</c>: the events with every field GameAnalytics requires.
    /// </summary>
    /// <param name="events">The events, oldest first.</param>
    /// <param name="session">The session they belong to.</param>
    /// <param name="clockOffset">Server time minus local time, added to every event's time.</param>
    public static byte[] EventsBody(IReadOnlyList<AnalyticsEvent> events, AnalyticsSession session, TimeSpan clockOffset)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(session);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var e in events)
            {
                writer.WriteStartObject();
                writer.WriteString("category", e.Category);
                writer.WriteNumber("v", 2);
                writer.WriteString("user_id", session.UserId);
                writer.WriteNumber("client_ts", (e.Time + clockOffset).ToUnixTimeSeconds());
                writer.WriteString("sdk_version", SdkVersion);
                writer.WriteString("os_version", session.Device.OsVersion);
                writer.WriteString("manufacturer", "unknown");
                writer.WriteString("device", session.DeviceName);
                writer.WriteString("platform", session.Device.Platform);
                writer.WriteString("session_id", session.SessionId);
                writer.WriteNumber("session_num", session.SessionNumber);
                writer.WriteString("build", session.Build);
                if (e.CustomDimension01 is { } dimension)
                {
                    writer.WriteString("custom_01", dimension);
                }

                if (e.EventId is { } eventId)
                {
                    writer.WriteString("event_id", eventId);
                }

                if (e.Value is { } value && double.IsFinite(value))
                {
                    writer.WriteNumber("value", value);
                }

                if (e.AttemptNumber is { } attempt)
                {
                    writer.WriteNumber("attempt_num", attempt);
                }

                if (e.Score is { } score)
                {
                    writer.WriteNumber("score", score);
                }

                if (e.Message is { } message)
                {
                    writer.WriteString("severity", "critical");
                    writer.WriteString("message", message);
                }

                if (e.Length is { } length)
                {
                    writer.WriteNumber("length", length);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
