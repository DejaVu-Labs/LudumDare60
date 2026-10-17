using System.Numerics;
using System.Runtime.InteropServices;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Rendering;

/// <summary>
/// How <see cref="SpriteBatch.Draw(Texture, IntRect, Vector2, in SpriteOptions)"/> places a sprite. Start from
/// <c>new SpriteOptions { ... }</c>: <c>default</c> has a zero scale and draws nothing.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SpriteOptions
{
    public SpriteOptions()
    {
    }

    /// <summary>
    /// No rotation, scale 1, white tint, origin at the top-left corner.
    /// </summary>
    public static SpriteOptions Default => new();

    /// <summary>
    /// The pivot, in source pixels from the source rectangle's top-left corner. The sprite's position is where the
    /// origin lands, and rotation and scale happen around it.
    /// </summary>
    public Vector2 Origin { get; init; }

    /// <summary>
    /// Scale around <see cref="Origin"/>. A negative component flips the sprite on that axis.
    /// </summary>
    public Vector2 Scale { get; init; } = Vector2.One;

    /// <summary>
    /// Rotation around <see cref="Origin"/> in radians, clockwise on screen.
    /// </summary>
    public float Rotation { get; init; }

    /// <summary>
    /// Multiplies every texel. Alpha below 255 fades the sprite.
    /// </summary>
    public Color Tint { get; init; } = Color.White;
}
