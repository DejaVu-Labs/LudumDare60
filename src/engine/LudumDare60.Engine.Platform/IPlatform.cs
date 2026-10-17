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
    /// Drives <paramref name="frameSink"/> until the platform shuts down.
    /// </summary>
    /// <returns>A task that completes when the platform stops producing frames.</returns>
    Task RunAsync(IFrameSink frameSink);
}
