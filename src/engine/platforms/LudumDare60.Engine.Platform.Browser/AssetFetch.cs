namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Starts reading <paramref name="url"/> and keeps the bytes under <paramref name="id"/> for <see cref="AssetTake"/>.
/// </summary>
/// <returns>The byte length, or minus the HTTP status for an error status.</returns>
internal delegate Task<int> AssetFetch(int id, string url, bool noCache);
