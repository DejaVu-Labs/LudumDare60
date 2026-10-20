namespace LudumDare60.Engine.Platform.Desktop.Telemetry;

/// <summary>
/// The fields that tie events to a player and a session.
/// </summary>
/// <param name="UserId">A random id kept on the device (see <see cref="AnalyticsState"/>).</param>
/// <param name="SessionId">A random id for this run.</param>
/// <param name="SessionNumber">How many sessions the player started, this one included.</param>
/// <param name="Build">The build version.</param>
/// <param name="Device">The platform and OS version.</param>
/// <param name="DeviceName">The <c>device</c> field.</param>
/// <param name="Renderer">Custom dimension 02, the renderer (<c>vulkan</c> or <c>opengl</c>), or null for none.</param>
internal sealed record AnalyticsSession(
    string UserId,
    string SessionId,
    int SessionNumber,
    string Build,
    AnalyticsDevice Device,
    string DeviceName,
    string? Renderer = null);
