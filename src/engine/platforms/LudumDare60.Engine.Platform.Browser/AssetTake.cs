namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Copies the bytes of read <paramref name="id"/> into <paramref name="destination"/> and forgets them.
/// </summary>
internal delegate void AssetTake(int id, Span<byte> destination);
