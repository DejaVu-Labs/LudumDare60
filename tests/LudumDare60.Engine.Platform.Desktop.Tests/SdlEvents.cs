using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Tests;

/// <summary>
/// Builds SDL events the way SDL fills them in, without a window or the native library.
/// </summary>
internal static class SdlEvents
{
    public const uint WindowId = 1;

    public static SDL.Event Key(SDL.Scancode scancode, bool down, ulong timeNs = 0, bool repeat = false) => new()
    {
        Key = new SDL.KeyboardEvent
        {
            Type = down ? SDL.EventType.KeyDown : SDL.EventType.KeyUp,
            Timestamp = timeNs,
            WindowID = WindowId,
            Scancode = scancode,
            Down = down,
            Repeat = repeat,
        },
    };

    public static SDL.Event MouseButton(byte button, bool down, float x, float y, uint which = 1, ulong timeNs = 0) => new()
    {
        Button = new SDL.MouseButtonEvent
        {
            Type = down ? SDL.EventType.MouseButtonDown : SDL.EventType.MouseButtonUp,
            Timestamp = timeNs,
            WindowID = WindowId,
            Which = which,
            Button = button,
            Down = down,
            X = x,
            Y = y,
        },
    };

    public static SDL.Event MouseMotion(float x, float y, uint which = 1, ulong timeNs = 0) => new()
    {
        Motion = new SDL.MouseMotionEvent
        {
            Type = SDL.EventType.MouseMotion,
            Timestamp = timeNs,
            WindowID = WindowId,
            Which = which,
            X = x,
            Y = y,
        },
    };

    public static SDL.Event Wheel(float x, float y, bool flipped = false, uint which = 1) => new()
    {
        Wheel = new SDL.MouseWheelEvent
        {
            Type = SDL.EventType.MouseWheel,
            WindowID = WindowId,
            Which = which,
            X = x,
            Y = y,
            Direction = flipped ? SDL.MouseWheelDirection.Flipped : SDL.MouseWheelDirection.Normal,
        },
    };

    public static SDL.Event Finger(SDL.EventType type, ulong fingerId, float x, float y, ulong touchId = 7) => new()
    {
        TFinger = new SDL.TouchFingerEvent
        {
            Type = type,
            TouchID = touchId,
            FingerID = fingerId,
            X = x,
            Y = y,
            WindowID = WindowId,
        },
    };

    public static SDL.Event Window(SDL.EventType type, ulong timeNs = 0) => new()
    {
        Window = new SDL.WindowEvent
        {
            Type = type,
            Timestamp = timeNs,
            WindowID = WindowId,
        },
    };

    public static SDL.Event Gamepad(SDL.EventType type, uint which) => new()
    {
        GDevice = new SDL.GamepadDeviceEvent
        {
            Type = type,
            Which = which,
        },
    };

    public static SDL.Event GamepadButton(uint which, SDL.GamepadButton button, bool down) => new()
    {
        GButton = new SDL.GamepadButtonEvent
        {
            Type = down ? SDL.EventType.GamepadButtonDown : SDL.EventType.GamepadButtonUp,
            Which = which,
            Button = (byte)button,
            Down = down,
        },
    };

    public static SDL.Event GamepadAxis(uint which, SDL.GamepadAxis axis, short value) => new()
    {
        GAxis = new SDL.GamepadAxisEvent
        {
            Type = SDL.EventType.GamepadAxisMotion,
            Which = which,
            Axis = (byte)axis,
            Value = value,
        },
    };
}
