namespace LudumDare60.Engine.Assets;

/// <summary>
/// Turns the bytes of an asset file into a <typeparamref name="T"/> and releases it again. Register one loader per type
/// with <see cref="AssetManager.Register{T}"/>.
/// </summary>
/// <typeparam name="T">Type the file is parsed into.</typeparam>
public interface IAssetLoader<T>
    where T : class
{
    /// <summary>
    /// Parses a file. Called from <see cref="AssetManager.Update"/>. An exception marks the asset as
    /// <see cref="AssetState.Failed"/>.
    /// </summary>
    /// <param name="path">Path of the file, relative to the asset root. For error messages.</param>
    /// <param name="data">The whole file. The loader owns the array and may keep it.</param>
    T Load(string path, byte[] data);

    /// <summary>
    /// Releases an asset this loader created, when its last handle is released or on
    /// <see cref="AssetManager.UnloadAll"/>.
    /// </summary>
    void Unload(T asset);
}
