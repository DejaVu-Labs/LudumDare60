namespace LudumDare60.Engine.Assets;

/// <summary>
/// Load state of a requested asset.
/// </summary>
public enum AssetState
{
    /// <summary>
    /// The file is being read, or it was read and waits for the next <see cref="AssetManager.Update"/> to be parsed.
    /// </summary>
    Loading = 0,

    /// <summary>
    /// The asset is parsed and available through <see cref="AssetManager.Get{T}"/>.
    /// </summary>
    Loaded = 1,

    /// <summary>
    /// Reading or parsing failed. <see cref="AssetManager.GetError{T}"/> tells why. Requesting the asset again
    /// retries.
    /// </summary>
    Failed = 2,
}
