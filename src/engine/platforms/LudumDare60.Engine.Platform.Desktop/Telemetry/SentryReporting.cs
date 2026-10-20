using Sentry.Protocol;

namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// Error reports of the desktop build, sent with the Sentry .NET SDK: the desktop twin of the browser's
/// <c>reporting.js</c>. The SDK also reports exceptions that no code catches (<see cref="AppDomain.UnhandledException"/>
/// and <see cref="TaskScheduler.UnobservedTaskException"/>) and sends what is queued when the process exits.
/// </summary>
internal static class SentryReporting
{
    /// <summary>
    /// Errors sent per run at most, like <c>MAX_ERRORS</c> in <c>reporting.js</c>.
    /// </summary>
    public const int MaxErrors = 10;

    private static ErrorBudget? s_budget;

    public static bool IsStarted => s_budget is not null;

    /// <summary>
    /// Starts the SDK. Does nothing when it already runs.
    /// </summary>
    /// <param name="config">The project's settings.</param>
    /// <param name="platform">The <c>game.platform</c> tag: <c>windows</c>, <c>linux</c> or <c>macos</c>.</param>
    public static void Start(ReportingConfig config, string platform)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (s_budget is not null)
        {
            return;
        }

        var budget = new ErrorBudget(MaxErrors);
        s_budget = budget;

        // The SDK stays on until the process exits; its exit hook sends what is still queued.
        _ = SentrySdk.Init(options => Configure(options, config, platform, budget));
    }

    /// <summary>
    /// The SDK options: no personal data, no sessions and no tracing, and at most <see cref="MaxErrors"/> different
    /// errors per run.
    /// </summary>
    internal static void Configure(SentryOptions options, ReportingConfig config, string platform, ErrorBudget budget)
    {
        options.Dsn = config.Dsn;
        options.Release = config.Release;
        options.Environment = config.Environment;
        options.SendDefaultPii = false;
        options.IsEnvironmentUser = false;

        // Otherwise the SDK sends the computer's name.
        options.ServerName = platform;
        options.AutoSessionTracking = false;
        options.IsGlobalModeEnabled = true;
        options.DefaultTags["game.platform"] = platform;
        options.SetBeforeSend((sentryEvent, _) => budget.TryTake(Key(sentryEvent)) ? sentryEvent : null);
    }

    /// <summary>
    /// Reports <paramref name="exception"/> when the SDK runs. Never throws.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <param name="handled"><see langword="true"/> when the run goes on after the error.</param>
    public static void Capture(Exception exception, bool handled)
    {
        if (s_budget is null)
        {
            return;
        }

        exception.Data[Mechanism.HandledKey] = handled;
        exception.Data[Mechanism.MechanismKey] = handled ? "dotnet.handled" : "dotnet.unhandled";
        SentrySdk.CaptureException(exception);
    }

    private static string Key(SentryEvent sentryEvent)
    {
        var exception = sentryEvent.SentryExceptions?.LastOrDefault();
        return exception is null ? sentryEvent.Message?.Formatted ?? "" : $"{exception.Type}: {exception.Value}";
    }
}
