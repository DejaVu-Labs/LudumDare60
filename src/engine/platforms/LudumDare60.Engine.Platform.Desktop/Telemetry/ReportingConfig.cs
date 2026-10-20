namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// Sentry settings from <c>reporting.json</c> next to the executable, in the format of the browser build's file.
/// </summary>
/// <param name="Dsn">The project's DSN.</param>
/// <param name="Release">Build version, for example <c>1.2.0</c> or <c>dev-42-abc1234</c>.</param>
/// <param name="Environment"><c>release</c> or <c>dev</c>.</param>
internal sealed record ReportingConfig(string Dsn, string Release, string Environment);
