namespace LudumDare60.Engine.Platform;

/// <summary>
/// Device behind a pointer event, stored in <see cref="RawInputEvent.Code"/>.
/// </summary>
/// <remarks>Values are part of the wire format: never renumber.</remarks>
public enum PointerType
{
    /// <summary>
    /// A mouse, or a pointer of unknown type.
    /// </summary>
    Mouse = 0,

    /// <summary>
    /// A finger on a touch surface.
    /// </summary>
    Touch = 1,

    /// <summary>
    /// A pen or stylus.
    /// </summary>
    Pen = 2,
}
