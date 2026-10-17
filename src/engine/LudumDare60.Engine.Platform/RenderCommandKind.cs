namespace LudumDare60.Engine.Platform;

/// <summary>
/// Opcode of a command in the render command stream that <see cref="IGraphicsBackend.Submit"/> receives.
/// </summary>
/// <remarks>
/// A command is a 32-bit opcode followed by its arguments, every one 32 bits wide and little-endian. Each member lists
/// its arguments in order. Values are part of the wire format shared with backend code (for example
/// <c>webgl2.js</c>): never renumber.
/// <para>A frame is <see cref="SetSurface"/>, then any number of <see cref="Clear"/> and <see cref="DrawSprites"/>, then
/// <see cref="Present"/>.</para>
/// </remarks>
public enum RenderCommandKind
{
    /// <summary>
    /// Never written. A backend that reads it has lost track of the stream.
    /// </summary>
    None = 0,

    /// <summary>
    /// Sizes the drawing surface and places the virtual screen on it. Always the first command of a frame.
    /// Arguments (all <c>int32</c>): surface width and height in device pixels, virtual screen width and height in
    /// virtual pixels, then the virtual screen's rectangle on the surface in device pixels from the top-left corner:
    /// x, y, width, height.
    /// </summary>
    SetSurface = 1,

    /// <summary>
    /// Fills the whole virtual screen with one color. Argument: the color as 4 bytes, RGBA (<see cref="Color"/>
    /// layout).
    /// </summary>
    Clear = 2,

    /// <summary>
    /// Draws a run of sprite instances with one texture, in order. Arguments (all <c>int32</c>): texture id, index of
    /// the first instance in the frame's instance buffer, instance count.
    /// </summary>
    DrawSprites = 3,

    /// <summary>
    /// Scales the virtual screen onto the surface with nearest filtering and fills the bars around it with black. Always
    /// the last command of a frame. No arguments.
    /// </summary>
    Present = 4,
}
