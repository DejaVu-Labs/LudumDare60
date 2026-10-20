using LudumDare60.Engine.Platform.Desktop.Rendering;

namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// Error reports (Sentry) and product analytics (GameAnalytics) of the desktop build, each on when its config file
/// next to the executable says so (<c>reporting.json</c>, <c>analytics.json</c>; see <see cref="TelemetryConfig"/>).
/// </summary>
internal sealed class DesktopTelemetry
{
    /// <summary>
    /// How long quitting waits for the last analytics events.
    /// </summary>
    public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);

    // One for the process, as HttpClient is meant to be used.
    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly GameAnalyticsClient? _analytics;

    private DesktopTelemetry(GameAnalyticsClient? analytics) => _analytics = analytics;

    /// <summary>
    /// <c>windows</c>, <c>linux</c> or <c>macos</c>, the <c>game.platform</c> tag of error reports.
    /// </summary>
    public static string PlatformName => OperatingSystem.IsWindows() ? "windows" : OperatingSystem.IsMacOS() ? "macos" : "linux";

    /// <summary>
    /// The analytics, or <see cref="NullAnalytics"/> when they are off.
    /// </summary>
    public IAnalytics Analytics => _analytics ?? (IAnalytics)NullAnalytics.Instance;

    /// <summary>
    /// <paramref name="renderer"/> as reports and analytics name it: <c>vulkan</c> or <c>opengl</c>.
    /// </summary>
    public static string GetRendererName(DesktopRenderer renderer) => renderer switch
    {
        DesktopRenderer.OpenGL => "opengl",
        _ => "vulkan",
    };

    /// <summary>
    /// Reads the config files in <paramref name="folder"/> and starts what they turn on. Sentry starts at once, so
    /// errors before the first frame are reported too. Both name <paramref name="renderer"/> from the start: the
    /// <c>game.renderer</c> tag in Sentry, custom dimension 02 in GameAnalytics.
    /// </summary>
    public static DesktopTelemetry Start(string folder, DesktopRenderer renderer)
    {
        var rendererName = GetRendererName(renderer);
        if (TelemetryConfig.ReadReporting(TelemetryConfig.ReadFile(folder, TelemetryConfig.ReportingFile)) is { } reporting)
        {
            SentryReporting.Start(reporting, PlatformName, rendererName);
        }

        GameAnalyticsClient? analytics = null;
        if (TelemetryConfig.ReadAnalytics(TelemetryConfig.ReadFile(folder, TelemetryConfig.AnalyticsFile)) is { } config)
        {
            var statePath = AnalyticsState.DefaultPath;
            analytics = new GameAnalyticsClient(
                config,
                AnalyticsState.Read(TelemetryConfig.ReadFile(Path.GetDirectoryName(statePath)!, Path.GetFileName(statePath))),
                state => SaveState(state, statePath),
                static (request, cancellationToken) => s_http.SendAsync(request, cancellationToken),
                GameAnalyticsProtocol.ProductionApi,
                AnalyticsDevice.Current,
                static () => DateTimeOffset.UtcNow,
                rendererName);

            // An error that ends the process: record it and send what is queued before the process is gone.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                analytics.Fatal(e.ExceptionObject is Exception exception ? $"{exception.GetType().Name}: {exception.Message}" : "Unhandled error");
                analytics.Stop(ShutdownTimeout);
            };
        }

        return new DesktopTelemetry(analytics);
    }

    /// <summary>
    /// Starts sending analytics in the background.
    /// </summary>
    public void BeginRun() => _analytics?.Start();

    /// <summary>
    /// Ends the analytics session and sends what is queued, waiting at most <see cref="ShutdownTimeout"/>.
    /// </summary>
    public void EndRun() => _analytics?.Stop(ShutdownTimeout);

    /// <summary>
    /// Adds the GPU and driver the renderer found to later error reports.
    /// </summary>
    public static void OnGraphicsReady(GraphicsInfo? info)
    {
        if (info is not null)
        {
            SentryReporting.SetGraphics(info);
        }
    }

    /// <summary>
    /// Records the pointer device of a press for the analytics' custom dimension.
    /// </summary>
    public void OnPointerDown(PointerType type) => _analytics?.SetPointerType(type);

    private static void SaveState(AnalyticsState state, string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, state.Write());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Without the file the next run counts as a new player; nothing worse.
        }
    }
}
