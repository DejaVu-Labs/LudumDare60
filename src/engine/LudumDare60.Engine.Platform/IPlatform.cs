namespace LudumDare60.Engine.Platform;

/// <summary>
/// A host environment that owns frame timing and drives an <see cref="IFrameSink"/>.
/// </summary>
public interface IPlatform
{
    /// <summary>
    /// Reads asset files. Available before <see cref="RunAsync"/> starts, so a game can request assets before its
    /// first frame.
    /// </summary>
    IAssetSource Assets { get; }

    /// <summary>
    /// The GPU. Usable from the first frame on: <see cref="RunAsync"/> sets it up before it starts producing frames.
    /// </summary>
    IGraphicsBackend Graphics { get; }

    /// <summary>
    /// The sound output. Usable from the first frame on, like <see cref="Graphics"/>.
    /// </summary>
    IAudioBackend Audio { get; }

    /// <summary>
    /// Product analytics. Usable at any time; events sent before the platform's analytics are ready are queued or
    /// dropped, as the platform decides. The default implementation drops every event (<see cref="NullAnalytics"/>).
    /// </summary>
    IAnalytics Analytics => NullAnalytics.Instance;

    /// <summary>
    /// Drives <paramref name="frameSink"/> until the platform shuts down.
    /// </summary>
    /// <returns>A task that completes when the platform stops producing frames.</returns>
    Task RunAsync(IFrameSink frameSink);

    /// <summary>
    /// Reports an error to wherever the platform collects them (the browser platform sends it to Sentry when the page
    /// is configured to). The engine calls it for failures it handles itself, such as an asset that failed to load.
    /// Must not throw. The default implementation drops the error.
    /// </summary>
    /// <param name="exception">The error.</param>
    /// <param name="handled"><see langword="true"/> when the run goes on after the error.</param>
    void ReportError(Exception exception, bool handled)
    {
    }
}
