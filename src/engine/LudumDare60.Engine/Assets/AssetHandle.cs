using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Assets;

/// <summary>
/// A reference to an asset requested from an <see cref="AssetManager"/>. Resolving it is an array index, so it is cheap
/// enough for per-frame code. A handle becomes stale once it is released or <see cref="AssetManager.UnloadAll"/> runs;
/// the manager then rejects it. <c>default</c> is never valid.
/// </summary>
/// <typeparam name="T">Type the asset is parsed into.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct AssetHandle<T>
    where T : class
{
    internal AssetHandle(int slot, int generation)
    {
        Slot = slot;
        Generation = generation;
    }

    internal int Slot { get; }

    internal int Generation { get; }
}
