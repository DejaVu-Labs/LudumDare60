using System.Buffers;
using System.Text.Json;

namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// What GameAnalytics needs to remember between runs, as the browser SDK keeps it in <c>localStorage</c>: a random user
/// id, the number of sessions, and how often each progression was started since it was last completed. Kept as JSON in
/// one file; a missing or broken file starts over.
/// </summary>
internal sealed class AnalyticsState
{
    private readonly Dictionary<string, int> _attempts = new(StringComparer.Ordinal);

    private AnalyticsState(string userId, int sessionCount)
    {
        UserId = userId;
        SessionCount = sessionCount;
    }

    public string UserId { get; }

    public int SessionCount { get; private set; }

    /// <summary>
    /// The default file: <c>LudumDare60/analytics-state.json</c> in the user's local application data
    /// (<c>%LOCALAPPDATA%</c> on Windows, <c>~/.local/share</c> on Linux).
    /// </summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LudumDare60",
        "analytics-state.json");

    /// <summary>
    /// A new player: a fresh user id, no sessions.
    /// </summary>
    public static AnalyticsState Create() => new(Guid.NewGuid().ToString("D"), 0);

    /// <summary>
    /// Reads the state, or starts a new one when <paramref name="json"/> is null or broken.
    /// </summary>
    public static AnalyticsState Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Create();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("userId", out var userId)
                || userId.ValueKind != JsonValueKind.String
                || !Guid.TryParse(userId.GetString(), out var id))
            {
                return Create();
            }

            var sessions = root.TryGetProperty("sessions", out var count) && count.TryGetInt32(out var value) ? Math.Max(0, value) : 0;
            var state = new AnalyticsState(id.ToString("D"), sessions);
            if (root.TryGetProperty("attempts", out var attempts) && attempts.ValueKind == JsonValueKind.Object)
            {
                foreach (var attempt in attempts.EnumerateObject())
                {
                    if (attempt.Value.TryGetInt32(out var tries) && tries > 0)
                    {
                        state._attempts[attempt.Name] = tries;
                    }
                }
            }

            return state;
        }
        catch (JsonException)
        {
            return Create();
        }
    }

    /// <summary>
    /// Counts a new session.
    /// </summary>
    /// <returns>Its number, from 1.</returns>
    public int StartSession() => ++SessionCount;

    /// <summary>
    /// Counts a progression event the way the GameAnalytics SDKs do: a start adds an attempt, a complete or fail
    /// reports the attempts, and a complete starts the count over.
    /// </summary>
    /// <param name="status">Start, Complete or Fail.</param>
    /// <param name="progression">The progression without the status, for example <c>Order:hoodie</c>.</param>
    /// <returns>The attempt number to send with a complete or fail; null for a start.</returns>
    public int? CountProgression(ProgressionStatus status, string progression)
    {
        _attempts.TryGetValue(progression, out var tries);
        if (status == ProgressionStatus.Start)
        {
            _attempts[progression] = tries + 1;
            return null;
        }

        if (status == ProgressionStatus.Complete)
        {
            _attempts.Remove(progression);
        }

        return Math.Max(tries, 1);
    }

    public string Write()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("userId", UserId);
            writer.WriteNumber("sessions", SessionCount);
            writer.WriteStartObject("attempts");
            foreach (var (progression, tries) in _attempts)
            {
                writer.WriteNumber(progression, tries);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }
}
