using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Rendering;

/// <summary>
/// A texture on the GPU, created by <see cref="Graphics.CreateTexture"/>. A handle becomes stale once
/// <see cref="Graphics.DestroyTexture"/> runs or the run ends; drawing with it then throws. <c>default</c> is never
/// valid.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Texture
{
    internal Texture(int id, int generation, int width, int height)
    {
        Id = id;
        Generation = generation;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Width in texels.
    /// </summary>
    public int Width { get; }

    /// <inheritdoc cref="Width"/>
    public int Height { get; }

    /// <summary>
    /// The backend's texture id. Ids are reused after a texture is destroyed; <see cref="Generation"/> tells the uses
    /// apart.
    /// </summary>
    internal int Id { get; }

    internal int Generation { get; }
}
