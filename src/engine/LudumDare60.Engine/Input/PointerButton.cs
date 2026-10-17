namespace LudumDare60.Engine.Input;

/// <summary>
/// A pointer button, as <see cref="Platform.RawInputEvent.Detail"/> numbers it. Touch and pen contact are
/// <see cref="Primary"/>.
/// </summary>
public enum PointerButton
{
    /// <summary>
    /// The left mouse button, a touch, or pen contact.
    /// </summary>
    Primary = 0,

    Middle = 1,

    /// <summary>
    /// The right mouse button.
    /// </summary>
    Secondary = 2,

    Back = 3,

    Forward = 4,

    /// <summary>
    /// The pen's eraser end.
    /// </summary>
    Eraser = 5,
}
