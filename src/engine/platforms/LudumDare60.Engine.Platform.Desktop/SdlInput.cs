using SDL3;

namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// Turns SDL events into <see cref="RawInputEvent"/>s with the same meaning the browser platform gives them (see
/// <see cref="RawInputEventKind"/>), and queues them until the frame drains them. Makes no SDL calls itself: the
/// platform passes in what it reads from SDL, so the translation runs in tests without a window.
/// </summary>
internal sealed class SdlInput
{
    /// <summary>
    /// Queue capacity, in events.
    /// </summary>
    public const int QueueCapacity = 4096;

    /// <summary>
    /// Pointer id of the mouse.
    /// </summary>
    public const int MousePointerId = 1;

    /// <summary>
    /// Pointer id of a pen, which SDL reports as mouse events.
    /// </summary>
    public const int PenPointerId = 2;

    /// <summary>
    /// <c>SDL_PEN_MOUSEID</c>, the mouse id of events SDL makes up from a pen. SDL3-CS declares it as an
    /// <see langword="int"/> (-2), which never equals the <see langword="uint"/> it is compared with.
    /// </summary>
    public static readonly uint PenMouseId = unchecked((uint)SDL.PenMouseID);

    /// <summary>
    /// Pointer id of the first finger; later fingers count up from it.
    /// </summary>
    public const int FirstFingerPointerId = 3;

    /// <summary>
    /// Pixels per wheel notch, as the browser platform scales line deltas (<c>WHEEL_LINE_PIXELS</c> in
    /// <c>input.js</c>).
    /// </summary>
    public const float WheelLinePixels = 16;

    /// <summary>
    /// A gamepad axis reports a change only when its value moves by more than this, as in the browser.
    /// </summary>
    public const float GamepadThreshold = 1f / 256;

    /// <summary>
    /// A trigger counts as pressed above this value (XInput's threshold, 30 of 255).
    /// </summary>
    public const float TriggerPressThreshold = 30f / 255;

    /// <summary>
    /// W3C Standard Gamepad indices of the triggers, which SDL reports as axes.
    /// </summary>
    public const int LeftTriggerButton = 6;

    /// <inheritdoc cref="LeftTriggerButton"/>
    public const int RightTriggerButton = 7;

    // SDL_GamepadButton to its W3C Standard Gamepad button index; -1 for buttons the standard layout lacks.
    private static readonly sbyte[] s_standardButtons =
    [
        0, // South
        1, // East
        2, // West
        3, // North
        8, // Back
        16, // Guide
        9, // Start
        10, // Left stick
        11, // Right stick
        4, // Left shoulder
        5, // Right shoulder
        12, // D-pad up
        13, // D-pad down
        14, // D-pad left
        15, // D-pad right
    ];

    private const int AxisCount = 6;

    private readonly InputQueue _queue = new(QueueCapacity);
    private readonly List<Gamepad> _gamepads = [];
    private readonly List<ulong> _fingers = [];
    private float _width = -1;
    private float _height = -1;
    private float _density = -1;
    private bool _hasKeyboardFocus;
    private bool _visible;
    private bool _focused;

    /// <summary>
    /// Events waiting to be drained.
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Queues the startup events: <see cref="RawInputEventKind.Resize"/>, then the focus state.
    /// </summary>
    /// <param name="timeNs">Now, on the <c>SDL_GetTicksNS</c> timeline.</param>
    /// <param name="pixelWidth">Window width in device pixels.</param>
    /// <param name="pixelHeight">Window height in device pixels.</param>
    /// <param name="density">Device pixels per logical pixel.</param>
    /// <param name="hasKeyboardFocus">Whether the window has keyboard focus.</param>
    /// <param name="visible">Whether the window is shown and not minimized.</param>
    public void Start(ulong timeNs, int pixelWidth, int pixelHeight, float density, bool hasKeyboardFocus, bool visible)
    {
        SetSize(timeNs, pixelWidth, pixelHeight, density);
        _hasKeyboardFocus = hasKeyboardFocus;
        _visible = visible;
        _focused = hasKeyboardFocus && visible;
        _queue.Push(_focused ? RawInputEventKind.FocusGained : RawInputEventKind.FocusLost, 0, 0, 0, timeNs, 0, 0, 0);
    }

    /// <summary>
    /// Queues a <see cref="RawInputEventKind.Resize"/> when the window size or density changed. The logical size is
    /// derived from the device size, so <c>X * Z</c> gives back the exact pixels.
    /// </summary>
    public void SetSize(ulong timeNs, int pixelWidth, int pixelHeight, float density)
    {
        if (!(density > 0))
        {
            density = 1;
        }

        var width = pixelWidth / density;
        var height = pixelHeight / density;
        if (width == _width && height == _height && density == _density)
        {
            return;
        }

        _width = width;
        _height = height;
        _density = density;
        _queue.Push(RawInputEventKind.Resize, 0, 0, 0, timeNs, width, height, density);
    }

    /// <summary>
    /// Queues the raw input events for <paramref name="e"/>. Ignores events that carry no input. Window size changes
    /// go through <see cref="SetSize"/> instead, since their size has to be read from SDL.
    /// </summary>
    public void Handle(in SDL.Event e)
    {
        switch ((SDL.EventType)e.Type)
        {
            case SDL.EventType.KeyDown:
            case SDL.EventType.KeyUp:
                HandleKey(in e.Key);
                break;
            case SDL.EventType.MouseButtonDown:
            case SDL.EventType.MouseButtonUp:
                HandleMouseButton(in e.Button);
                break;
            case SDL.EventType.MouseMotion:
                HandleMouseMotion(in e.Motion);
                break;
            case SDL.EventType.MouseWheel:
                HandleWheel(in e.Wheel);
                break;
            case SDL.EventType.FingerDown:
            case SDL.EventType.FingerUp:
            case SDL.EventType.FingerMotion:
            case SDL.EventType.FingerCanceled:
                HandleFinger(in e.TFinger);
                break;
            case SDL.EventType.WindowFocusGained:
                SetFocus(e.Window.Timestamp, hasKeyboardFocus: true, _visible);
                break;
            case SDL.EventType.WindowFocusLost:
                SetFocus(e.Window.Timestamp, hasKeyboardFocus: false, _visible);
                break;
            case SDL.EventType.WindowShown:
            case SDL.EventType.WindowRestored:
            case SDL.EventType.WindowMaximized:
                SetFocus(e.Window.Timestamp, _hasKeyboardFocus, visible: true);
                break;
            case SDL.EventType.WindowHidden:
            case SDL.EventType.WindowMinimized:
                SetFocus(e.Window.Timestamp, _hasKeyboardFocus, visible: false);
                break;
            case SDL.EventType.GamepadAdded:
                HandleGamepadAdded(in e.GDevice);
                break;
            case SDL.EventType.GamepadRemoved:
                HandleGamepadRemoved(in e.GDevice);
                break;
            case SDL.EventType.GamepadButtonDown:
            case SDL.EventType.GamepadButtonUp:
                HandleGamepadButton(in e.GButton);
                break;
            case SDL.EventType.GamepadAxisMotion:
                HandleGamepadAxis(in e.GAxis);
                break;
            default:
                break;
        }
    }

    /// <inheritdoc cref="InputQueue.Drain"/>
    public int Drain(Span<RawInputEvent> target, ulong frameTimeNs) => _queue.Drain(target, frameTimeNs);

    private void HandleKey(in SDL.KeyboardEvent e)
    {
        var key = KeyMap.FromScancode((int)e.Scancode);
        if (key == Key.None || (e.Down && e.Repeat))
        {
            return;
        }

        var kind = e.Down ? RawInputEventKind.KeyDown : RawInputEventKind.KeyUp;
        _queue.Push(kind, 0, (ushort)key, 0, e.Timestamp, 0, 0, 0);
    }

    private void HandleMouseButton(in SDL.MouseButtonEvent e)
    {
        // SDL numbers buttons from 1 (left, middle, right, back, forward); the browser from 0 in the same order.
        if (!TryGetMousePointer(e.Which, out var type, out var id) || e.Button is < 1 or > 5)
        {
            return;
        }

        var kind = e.Down ? RawInputEventKind.PointerDown : RawInputEventKind.PointerUp;
        _queue.Push(kind, (byte)(e.Button - 1), (ushort)type, id, e.Timestamp, e.X, e.Y, 0);
    }

    private void HandleMouseMotion(in SDL.MouseMotionEvent e)
    {
        if (TryGetMousePointer(e.Which, out var type, out var id))
        {
            _queue.Push(RawInputEventKind.PointerMove, 0, (ushort)type, id, e.Timestamp, e.X, e.Y, 0);
        }
    }

    private void HandleWheel(in SDL.MouseWheelEvent e)
    {
        if (e.Which == SDL.TouchMouseID)
        {
            return;
        }

        // SDL counts notches with positive Y away from the user; the browser counts pixels with positive Y towards
        // the user (scrolling down).
        var sign = e.Direction == SDL.MouseWheelDirection.Flipped ? -WheelLinePixels : WheelLinePixels;
        _queue.Push(RawInputEventKind.Wheel, 0, 0, 0, e.Timestamp, e.X * sign, -e.Y * sign, 0);
    }

    private void HandleFinger(in SDL.TouchFingerEvent e)
    {
        // Touches that SDL makes up from the mouse would report the mouse twice.
        if (e.TouchID == SDL.MouseTouchID)
        {
            return;
        }

        var type = (SDL.EventType)e.Type;
        var index = _fingers.IndexOf(e.FingerID);
        if (index < 0)
        {
            if (type != SDL.EventType.FingerDown)
            {
                return;
            }

            index = _fingers.IndexOf(0);
            if (index < 0)
            {
                index = _fingers.Count;
                _fingers.Add(e.FingerID);
            }
            else
            {
                _fingers[index] = e.FingerID;
            }
        }

        var id = FirstFingerPointerId + index;
        var x = e.X * _width;
        var y = e.Y * _height;
        var code = (ushort)PointerType.Touch;
        switch (type)
        {
            case SDL.EventType.FingerDown:
                _queue.Push(RawInputEventKind.PointerDown, 0, code, id, e.Timestamp, x, y, 0);
                break;
            case SDL.EventType.FingerMotion:
                _queue.Push(RawInputEventKind.PointerMove, 0, code, id, e.Timestamp, x, y, 0);
                break;
            case SDL.EventType.FingerUp:
                _queue.Push(RawInputEventKind.PointerUp, 0, code, id, e.Timestamp, x, y, 0);
                _fingers[index] = 0;
                break;
            default:
                _queue.Push(RawInputEventKind.PointerCancel, 0, code, id, e.Timestamp, 0, 0, 0);
                _fingers[index] = 0;
                break;
        }
    }

    private void SetFocus(ulong timeNs, bool hasKeyboardFocus, bool visible)
    {
        _hasKeyboardFocus = hasKeyboardFocus;
        _visible = visible;
        var focused = hasKeyboardFocus && visible;
        if (focused != _focused)
        {
            _focused = focused;
            _queue.Push(focused ? RawInputEventKind.FocusGained : RawInputEventKind.FocusLost, 0, 0, 0, timeNs, 0, 0, 0);
        }
    }

    private void HandleGamepadAdded(in SDL.GamepadDeviceEvent e)
    {
        if (FindGamepad(e.Which) >= 0)
        {
            return;
        }

        var index = FindGamepad(0);
        if (index < 0)
        {
            index = _gamepads.Count;
            _gamepads.Add(new Gamepad());
        }

        _gamepads[index].Connect(e.Which);
        _queue.Push(RawInputEventKind.GamepadConnected, 1, 0, index, e.Timestamp, 0, 0, 0);
    }

    private void HandleGamepadRemoved(in SDL.GamepadDeviceEvent e)
    {
        var index = FindGamepad(e.Which);
        if (index >= 0)
        {
            _gamepads[index].Connect(0);
            _queue.Push(RawInputEventKind.GamepadDisconnected, 0, 0, index, e.Timestamp, 0, 0, 0);
        }
    }

    private void HandleGamepadButton(in SDL.GamepadButtonEvent e)
    {
        var index = FindGamepad(e.Which);
        if (index < 0 || e.Button >= s_standardButtons.Length)
        {
            return;
        }

        var pressed = e.Down ? (byte)1 : (byte)0;
        _queue.Push(RawInputEventKind.GamepadButton, pressed, (ushort)s_standardButtons[e.Button], index, e.Timestamp, pressed, 0, 0);
    }

    private void HandleGamepadAxis(in SDL.GamepadAxisEvent e)
    {
        var index = FindGamepad(e.Which);
        if (index < 0 || e.Axis >= AxisCount)
        {
            return;
        }

        var gamepad = _gamepads[index];
        var value = Math.Max(-1f, e.Value / 32767f);
        if (Math.Abs(value - gamepad.Axes[e.Axis]) <= GamepadThreshold)
        {
            return;
        }

        gamepad.Axes[e.Axis] = value;
        switch ((SDL.GamepadAxis)e.Axis)
        {
            case SDL.GamepadAxis.LeftTrigger:
                PushTrigger(index, LeftTriggerButton, value, e.Timestamp);
                break;
            case SDL.GamepadAxis.RightTrigger:
                PushTrigger(index, RightTriggerButton, value, e.Timestamp);
                break;
            default:
                // LeftX, LeftY, RightX and RightY are axes 0 to 3 in both layouts.
                _queue.Push(RawInputEventKind.GamepadAxis, 0, e.Axis, index, e.Timestamp, value, 0, 0);
                break;
        }
    }

    private void PushTrigger(int index, int button, float value, ulong timeNs)
    {
        var pressed = value > TriggerPressThreshold ? (byte)1 : (byte)0;
        _queue.Push(RawInputEventKind.GamepadButton, pressed, (ushort)button, index, timeNs, value, 0, 0);
    }

    private int FindGamepad(uint instanceId)
    {
        for (var i = 0; i < _gamepads.Count; i++)
        {
            if (_gamepads[i].InstanceId == instanceId)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryGetMousePointer(uint which, out PointerType type, out int id)
    {
        if (which == SDL.TouchMouseID)
        {
            // Finger events report touches already.
            type = PointerType.Touch;
            id = 0;
            return false;
        }

        if (which == PenMouseId)
        {
            type = PointerType.Pen;
            id = PenPointerId;
            return true;
        }

        type = PointerType.Mouse;
        id = MousePointerId;
        return true;
    }

    private sealed class Gamepad
    {
        /// <summary>
        /// SDL joystick instance id; 0 while the slot is free (SDL never uses 0).
        /// </summary>
        public uint InstanceId { get; private set; }

        /// <summary>
        /// Last reported value of each SDL axis.
        /// </summary>
        public float[] Axes { get; } = new float[AxisCount];

        public void Connect(uint instanceId)
        {
            InstanceId = instanceId;
            Array.Clear(Axes);
        }
    }
}
