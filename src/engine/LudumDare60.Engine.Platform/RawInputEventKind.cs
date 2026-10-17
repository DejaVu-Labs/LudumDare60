namespace LudumDare60.Engine.Platform;

/// <summary>
/// Kind of a <see cref="RawInputEvent"/>. Each member lists the fields it uses; unused fields are zero.
/// </summary>
/// <remarks>Values are part of the wire format shared with platform code (for example <c>input.js</c>): never renumber.</remarks>
public enum RawInputEventKind
{
    /// <summary>
    /// Empty event. Platforms never send it.
    /// </summary>
    None = 0,

    /// <summary>
    /// The platform dropped events because its queue was full. The event takes the place of the dropped ones.
    /// <see cref="RawInputEvent.Id"/>: number of dropped events. Treat input state as unknown: release everything.
    /// </summary>
    Overflow = 1,

    /// <summary>
    /// The game lost keyboard focus or became hidden. Release everything: key-up events may never arrive.
    /// </summary>
    FocusLost = 2,

    /// <summary>
    /// The game gained keyboard focus or became visible.
    /// </summary>
    FocusGained = 3,

    /// <summary>
    /// The drawing surface changed size. Sent first at startup.
    /// <see cref="RawInputEvent.X"/>, <see cref="RawInputEvent.Y"/>: width and height in logical (CSS) pixels, possibly
    /// fractional. <see cref="RawInputEvent.Z"/>: device pixels per logical pixel. Where the platform knows the size in
    /// device pixels, <c>X * Z</c> and <c>Y * Z</c> round to it exactly.
    /// </summary>
    Resize = 4,

    /// <summary>
    /// A key went down. Auto-repeat is not reported. <see cref="RawInputEvent.Code"/>: <see cref="Platform.Key"/>.
    /// </summary>
    KeyDown = 5,

    /// <summary>
    /// A key went up. <see cref="RawInputEvent.Code"/>: <see cref="Platform.Key"/>.
    /// </summary>
    KeyUp = 6,

    /// <summary>
    /// A pointer button went down. <see cref="RawInputEvent.Id"/>: pointer id. <see cref="RawInputEvent.Code"/>:
    /// <see cref="PointerType"/>. <see cref="RawInputEvent.Detail"/>: button (0 left, 1 middle, 2 right, 3 back,
    /// 4 forward, 5 pen eraser; touch and pen contact are 0). <see cref="RawInputEvent.X"/>,
    /// <see cref="RawInputEvent.Y"/>: position in logical pixels from the surface's top-left corner.
    /// </summary>
    PointerDown = 7,

    /// <summary>
    /// A pointer button went up. Same fields as <see cref="PointerDown"/>.
    /// </summary>
    PointerUp = 8,

    /// <summary>
    /// A pointer moved. <see cref="RawInputEvent.Id"/>: pointer id. <see cref="RawInputEvent.Code"/>:
    /// <see cref="PointerType"/>. <see cref="RawInputEvent.X"/>, <see cref="RawInputEvent.Y"/>: position in logical
    /// pixels. Not sent for a locked pointer: see <see cref="PointerMotion"/>.
    /// </summary>
    PointerMove = 9,

    /// <summary>
    /// A locked mouse moved. <see cref="RawInputEvent.Id"/>: pointer id. <see cref="RawInputEvent.Code"/>:
    /// <see cref="PointerType"/>. <see cref="RawInputEvent.X"/>, <see cref="RawInputEvent.Y"/>: relative motion in
    /// logical pixels.
    /// </summary>
    PointerMotion = 10,

    /// <summary>
    /// The platform took a pointer away (for example a touch turned into a system gesture). All its buttons count as
    /// released. <see cref="RawInputEvent.Id"/>: pointer id. <see cref="RawInputEvent.Code"/>: <see cref="PointerType"/>.
    /// </summary>
    PointerCancel = 11,

    /// <summary>
    /// The pointer was locked or unlocked, including an unlock by the platform itself (Esc in the browser).
    /// <see cref="RawInputEvent.Detail"/>: 1 locked, 0 unlocked.
    /// </summary>
    PointerLockChanged = 12,

    /// <summary>
    /// The wheel turned. <see cref="RawInputEvent.X"/>, <see cref="RawInputEvent.Y"/>, <see cref="RawInputEvent.Z"/>:
    /// deltas in logical pixels.
    /// </summary>
    Wheel = 13,

    /// <summary>
    /// A gamepad appeared. Its current state follows as button and axis events. <see cref="RawInputEvent.Id"/>:
    /// gamepad index. <see cref="RawInputEvent.Detail"/>: 1 when button and axis indices follow the W3C Standard
    /// Gamepad layout, 0 when they are raw and device specific.
    /// </summary>
    GamepadConnected = 14,

    /// <summary>
    /// A gamepad went away. <see cref="RawInputEvent.Id"/>: gamepad index.
    /// </summary>
    GamepadDisconnected = 15,

    /// <summary>
    /// A gamepad button changed. <see cref="RawInputEvent.Id"/>: gamepad index. <see cref="RawInputEvent.Code"/>:
    /// button index. <see cref="RawInputEvent.Detail"/>: 1 pressed, 0 released. <see cref="RawInputEvent.X"/>:
    /// analog value in [0, 1].
    /// </summary>
    GamepadButton = 16,

    /// <summary>
    /// A gamepad axis changed. <see cref="RawInputEvent.Id"/>: gamepad index. <see cref="RawInputEvent.Code"/>: axis
    /// index. <see cref="RawInputEvent.X"/>: value in [-1, 1].
    /// </summary>
    GamepadAxis = 17,
}
