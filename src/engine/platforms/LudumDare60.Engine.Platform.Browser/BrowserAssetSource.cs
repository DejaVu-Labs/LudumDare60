namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// <see cref="IAssetSource"/> over <c>fetch</c>. Each file costs two interop calls: one that fetches it and reports its
/// length, one that copies it into a <c>byte[]</c> of that length.
/// </summary>
/// <param name="root">Folder of the assets, relative to the page, for example <c>assets/</c>.</param>
/// <param name="version">Appended to every URL as <c>?v=</c>, so a new build never reads a stale file from a cache.
/// <see langword="null"/> for none.</param>
/// <param name="noCache">Revalidate every read with the server instead of trusting the browser cache.</param>
/// <param name="fetch">Starts a read in JS (<see cref="AssetsInterop.ReadAsync"/> in the browser).</param>
/// <param name="take">Copies a finished read out of JS (<see cref="AssetsInterop.Take"/> in the browser).</param>
internal sealed class BrowserAssetSource(string root, string? version, bool noCache, AssetFetch fetch, AssetTake take)
    : IAssetSource
{
    private int _nextReadId;

    /// <summary>
    /// The URL of the asset at <paramref name="path"/>, relative to the page.
    /// </summary>
    public static string GetUrl(string root, string path, string? version)
    {
        var separator = root.Length == 0 || root.EndsWith('/') ? "" : "/";
        var url = string.Concat(root, separator, path);
        return string.IsNullOrEmpty(version) ? url : $"{url}?v={Uri.EscapeDataString(version)}";
    }

    public async Task<byte[]> ReadAsync(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var id = ++_nextReadId;
        var url = GetUrl(root, path, version);
        int length;
        try
        {
            length = await fetch(id, url, noCache);
        }
        catch (Exception exception) when (exception is not AssetLoadException)
        {
            throw new AssetLoadException(path, 0, $"Reading '{url}' failed: {exception.Message}", exception);
        }

        if (length < 0)
        {
            throw new AssetLoadException(path, -length, $"Reading '{url}' failed with HTTP status {-length}.");
        }

        var data = new byte[length];
        take(id, data);
        return data;
    }
}
