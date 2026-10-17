namespace LudumDare60.Engine.Platform;

/// <summary>
/// Reads asset files from wherever the platform keeps them (in the browser: <c>fetch</c> from the asset root next to
/// the page).
/// </summary>
public interface IAssetSource
{
    /// <summary>
    /// Reads the whole file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Path relative to the platform's asset root, for example <c>sounds/click.ogg</c>. Callers
    /// validate it; the source only reads.</param>
    /// <returns>The file's bytes. The caller owns the array.</returns>
    /// <exception cref="AssetLoadException">The file is missing or could not be read.</exception>
    Task<byte[]> ReadAsync(string path);
}
