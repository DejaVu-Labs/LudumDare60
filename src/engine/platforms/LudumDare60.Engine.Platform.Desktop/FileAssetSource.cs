namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// <see cref="IAssetSource"/> over the file system. Reads run on the thread pool; <c>AssetManager</c> applies their
/// results on the main thread.
/// </summary>
/// <param name="root">Folder of the assets.</param>
internal sealed class FileAssetSource(string root) : IAssetSource
{
    /// <summary>
    /// Folder of the assets.
    /// </summary>
    public string Root { get; } = root;

    public async Task<byte[]> ReadAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var fullPath = Path.Combine(Root, path);
        try
        {
            return await File.ReadAllBytesAsync(fullPath).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AssetLoadException(path, 0, $"Reading '{fullPath}' failed: {exception.Message}", exception);
        }
    }
}
