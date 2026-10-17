namespace LudumDare60.Engine.Platform;

/// <summary>
/// An asset could not be read or parsed.
/// </summary>
public sealed class AssetLoadException : Exception
{
    public AssetLoadException()
    {
    }

    public AssetLoadException(string message)
        : base(message)
    {
    }

    public AssetLoadException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <param name="path">Path of the asset, relative to the asset root.</param>
    /// <param name="statusCode">HTTP status of the failed request, or 0 when there was no HTTP response.</param>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The underlying error (network, parser), if any.</param>
    public AssetLoadException(string path, int statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        AssetPath = path;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Path of the asset, relative to the asset root. Empty when unknown.
    /// </summary>
    public string AssetPath { get; } = "";

    /// <summary>
    /// HTTP status of the failed request, or 0 when there was no HTTP response (network error, parser error).
    /// </summary>
    public int StatusCode { get; }
}
