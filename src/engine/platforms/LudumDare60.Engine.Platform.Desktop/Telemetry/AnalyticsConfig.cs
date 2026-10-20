namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// GameAnalytics settings from <c>analytics.json</c> next to the executable, in the format of the browser build's file.
/// </summary>
/// <param name="GameKey">32 letters and digits.</param>
/// <param name="SecretKey">40 letters and digits.</param>
/// <param name="Build">Build version, at most 32 characters.</param>
internal sealed record AnalyticsConfig(string GameKey, string SecretKey, string Build);
