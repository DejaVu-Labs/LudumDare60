using System.Numerics;
using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform;

/// <summary>
/// One sprite quad, as the GPU reads it: one instance of the sprite shader. Positions and sizes are in virtual pixels
/// (the low-resolution screen), with y pointing down.
/// </summary>
/// <remarks>
/// The layout is the wire format of <see cref="IGraphicsBackend.Submit"/> and the vertex layout of the sprite shaders
/// (<c>sprite.glsl.js</c>). Little-endian, <see cref="Size"/> bytes:
/// <code>
/// offset 0  Position  float x2   where the origin lands on the screen
/// offset 8  Origin    float x2   pivot inside the source rectangle, in source pixels from its top-left corner
/// offset 16 Scale     float x2   around the origin; negative flips
/// offset 24 Rotation  float      radians around the origin, clockwise on screen (y points down)
/// offset 28 Tint      byte x4    RGBA, straight alpha, multiplies the texel
/// offset 32 SourceX   ushort     source rectangle in texels from the texture's top-left corner
/// offset 34 SourceY   ushort
/// offset 36 SourceW   ushort
/// offset 38 SourceH   ushort
/// </code>
/// Field order and types must not change without changing every backend that reads them.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct SpriteInstance
{
    /// <summary>
    /// Size of one instance in bytes.
    /// </summary>
    public const int Size = 40;

    public SpriteInstance(
        Vector2 position,
        Vector2 origin,
        Vector2 scale,
        float rotation,
        Color tint,
        ushort sourceX,
        ushort sourceY,
        ushort sourceWidth,
        ushort sourceHeight)
    {
        Position = position;
        Origin = origin;
        Scale = scale;
        Rotation = rotation;
        Tint = tint;
        SourceX = sourceX;
        SourceY = sourceY;
        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
    }

    public Vector2 Position { get; }

    public Vector2 Origin { get; }

    public Vector2 Scale { get; }

    public float Rotation { get; }

    public Color Tint { get; }

    public ushort SourceX { get; }

    public ushort SourceY { get; }

    public ushort SourceWidth { get; }

    public ushort SourceHeight { get; }
}
