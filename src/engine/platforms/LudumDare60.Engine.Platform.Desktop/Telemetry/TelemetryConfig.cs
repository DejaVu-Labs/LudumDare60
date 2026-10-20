using System.Text.Json;

namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// Reads the telemetry config files. Each one is optional: a missing, empty (<c>{}</c>) or broken file turns its
/// service off. The rules match <c>readConfig</c> in <c>reporting.js</c> and <c>analytics.js</c>.
/// </summary>
internal static class TelemetryConfig
{
    public const string ReportingFile = "reporting.json";

    public const string AnalyticsFile = "analytics.json";

    /// <returns>The settings, or null when <paramref name="json"/> holds no usable DSN.</returns>
    public static ReportingConfig? ReadReporting(string? json)
    {
        using var document = Parse(json);
        if (document is null)
        {
            return null;
        }

        var root = document.RootElement;
        var dsn = GetString(root, "dsn");
        if (dsn is null || !Uri.TryCreate(dsn, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || uri.UserInfo.Length == 0)
        {
            return null;
        }

        return new ReportingConfig(
            dsn,
            GetString(root, "release") is { Length: > 0 } release ? release : "unknown",
            GetString(root, "environment") is { Length: > 0 } environment ? environment : "production");
    }

    /// <returns>The settings, or null when <paramref name="json"/> holds no valid keys.</returns>
    public static AnalyticsConfig? ReadAnalytics(string? json)
    {
        using var document = Parse(json);
        if (document is null)
        {
            return null;
        }

        var root = document.RootElement;
        var gameKey = GetString(root, "gameKey");
        var secretKey = GetString(root, "secretKey");
        if (!IsKey(gameKey, 32) || !IsKey(secretKey, 40))
        {
            return null;
        }

        var build = GetString(root, "build");
        return new AnalyticsConfig(gameKey!, secretKey!, build is { Length: > 0 and <= 32 } ? build : "unknown");
    }

    /// <summary>
    /// Reads <paramref name="name"/> in <paramref name="folder"/>.
    /// </summary>
    /// <returns>The text, or null when the file is missing or cannot be read.</returns>
    public static string? ReadFile(string folder, string name)
    {
        try
        {
            return File.ReadAllText(Path.Combine(folder, name));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static JsonDocument? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return document;
            }

            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool IsKey(string? key, int length)
    {
        if (key is null || key.Length != length)
        {
            return false;
        }

        foreach (var c in key)
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
