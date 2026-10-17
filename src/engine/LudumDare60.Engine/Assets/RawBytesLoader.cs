namespace LudumDare60.Engine.Assets;

/// <summary>
/// Keeps a file as raw bytes, without parsing. Every <see cref="AssetManager"/> has it registered for <c>byte[]</c>.
/// </summary>
public sealed class RawBytesLoader : IAssetLoader<byte[]>
{
    /// <returns><paramref name="data"/> itself, without copying.</returns>
    public byte[] Load(string path, byte[] data) => data;

    public void Unload(byte[] asset)
    {
    }
}
