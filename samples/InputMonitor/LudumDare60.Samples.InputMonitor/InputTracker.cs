using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Samples.InputMonitor;

/// <summary>
/// Folds raw input events into the state they describe (held keys, pointers, gamepads) and writes it as text.
/// </summary>
/// <remarks>
/// A sample of how to consume <see cref="RawInputEvent"/>, not the engine's input layer: it shows the rules every
/// consumer follows. <see cref="RawInputEventKind.FocusLost"/> and <see cref="RawInputEventKind.Overflow"/> release
/// everything, because the matching up events may never arrive. Each pointer button has its own down and up event.
/// All storage is preallocated; <see cref="Apply(ReadOnlySpan{RawInputEvent}, long)"/> and <see cref="WriteReport"/> allocate nothing.
/// </remarks>
public sealed class InputTracker
{
    public const int MaxPointers = 8;
    public const int MaxGamepads = 4;
    public const int MaxGamepadButtons = 32;
    public const int MaxGamepadAxes = 8;
    public const int HistorySize = 12;

    private const int KeyCount = 256;
    private const int PointerButtonCount = 6;

    private static readonly CultureInfo s_culture = CultureInfo.InvariantCulture;

    // Formatting an enum boxes it, so names are looked up in tables built once.
    private static readonly string[] s_keyNames = NameTable<Key>(KeyCount);
    private static readonly string[] s_kindNames = NameTable<RawInputEventKind>(32);
    private static readonly string[] s_pointerTypeNames = NameTable<PointerType>(4);

    private readonly bool[] _keysDown = new bool[KeyCount];
    private readonly Pointer[] _pointers = new Pointer[MaxPointers];
    private readonly bool[] _gamepadConnected = new bool[MaxGamepads];
    private readonly bool[] _gamepadStandard = new bool[MaxGamepads];
    private readonly uint[] _gamepadButtons = new uint[MaxGamepads];
    private readonly float[] _gamepadAxes = new float[MaxGamepads * MaxGamepadAxes];
    private readonly RawInputEvent[] _history = new RawInputEvent[HistorySize];
    private readonly long[] _historyFrames = new long[HistorySize];
    private int _historyStart;

    public bool Focused { get; private set; }

    public bool PointerLocked { get; private set; }

    public float SurfaceWidth { get; private set; }

    public float SurfaceHeight { get; private set; }

    public float PixelRatio { get; private set; } = 1;

    /// <summary>
    /// Sum of all <see cref="RawInputEventKind.PointerMotion"/> deltas.
    /// </summary>
    public float MotionX { get; private set; }

    /// <inheritdoc cref="MotionX"/>
    public float MotionY { get; private set; }

    /// <summary>
    /// Sum of all <see cref="RawInputEventKind.Wheel"/> deltas.
    /// </summary>
    public float WheelX { get; private set; }

    /// <inheritdoc cref="WheelX"/>
    public float WheelY { get; private set; }

    /// <summary>
    /// Events the platform dropped, from <see cref="RawInputEventKind.Overflow"/>.
    /// </summary>
    public long DroppedEvents { get; private set; }

    public long TotalEvents { get; private set; }

    /// <summary>
    /// Most events delivered in a single frame so far.
    /// </summary>
    public int MaxEventsPerFrame { get; private set; }

    /// <summary>
    /// Applies one frame's events, in order.
    /// </summary>
    /// <param name="events">The frame's events.</param>
    /// <param name="frameIndex">The frame they came with, kept in the history.</param>
    public void Apply(ReadOnlySpan<RawInputEvent> events, long frameIndex)
    {
        TotalEvents += events.Length;
        MaxEventsPerFrame = Math.Max(MaxEventsPerFrame, events.Length);
        foreach (ref readonly var e in events)
        {
            Apply(in e);
            Remember(in e, frameIndex);
        }
    }

    public bool IsKeyDown(Key key) => (uint)key < KeyCount && _keysDown[(int)key];

    /// <summary>
    /// Finds an active pointer.
    /// </summary>
    /// <returns>Whether the pointer is known: it moved over the surface or pressed a button, and was not cancelled.</returns>
    public bool TryGetPointer(int id, out float x, out float y, out int buttons)
    {
        var index = FindPointer(id);
        if (index < 0)
        {
            x = y = 0;
            buttons = 0;
            return false;
        }

        ref readonly var pointer = ref _pointers[index];
        x = pointer.X;
        y = pointer.Y;
        buttons = pointer.Buttons;
        return true;
    }

    public bool IsGamepadConnected(int index) => (uint)index < MaxGamepads && _gamepadConnected[index];

    public bool IsGamepadButtonDown(int index, int button)
    {
        return IsGamepadConnected(index) && (uint)button < MaxGamepadButtons && (_gamepadButtons[index] & (1u << button)) != 0;
    }

    public float GetGamepadAxis(int index, int axis)
    {
        return IsGamepadConnected(index) && (uint)axis < MaxGamepadAxes ? _gamepadAxes[(index * MaxGamepadAxes) + axis] : 0;
    }

    /// <summary>
    /// Events kept in the history, oldest first.
    /// </summary>
    public int HistoryCount { get; private set; }

    /// <summary>
    /// An event from the history: 0 is the oldest kept.
    /// </summary>
    public RawInputEvent GetHistory(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)HistoryCount, nameof(index));
        return _history[(_historyStart + index) % HistorySize];
    }

    /// <summary>
    /// Writes the state as UTF-8 text.
    /// </summary>
    /// <returns>Bytes written. The text is cut off when <paramref name="destination"/> is too small.</returns>
    public int WriteReport(Span<byte> destination)
    {
        var w = new ReportWriter(destination);

        w.Append(w.Rest, s_culture, $"LD60 input monitor   events {TotalEvents}   max/frame {MaxEventsPerFrame}   dropped {DroppedEvents}\n");
        w.Append(w.Rest, s_culture, $"Double-click to lock the pointer, Esc to unlock.\n\n");
        w.Append(w.Rest, s_culture, $"Focus      {(Focused ? "yes" : "no")}\n");
        w.Append(w.Rest, s_culture, $"Surface    {SurfaceWidth:0}x{SurfaceHeight:0} @{PixelRatio:0.##}x\n");
        w.Append(w.Rest, s_culture, $"Lock       {(PointerLocked ? "locked" : "free")}   motion {MotionX:0.#}, {MotionY:0.#}\n");
        w.Append(w.Rest, s_culture, $"Wheel      {WheelX:0.#}, {WheelY:0.#}\n");

        w.Append(w.Rest, s_culture, $"Keys       ");
        var anyKey = false;
        for (var i = 0; i < KeyCount; i++)
        {
            if (_keysDown[i])
            {
                w.Append(w.Rest, s_culture, $"{s_keyNames[i]} ");
                anyKey = true;
            }
        }

        w.Append(w.Rest, s_culture, $"{(anyKey ? "" : "-")}\n");

        w.Append(w.Rest, s_culture, $"Pointers   ");
        var anyPointer = false;
        foreach (ref readonly var pointer in _pointers.AsSpan())
        {
            if (!pointer.Active)
            {
                continue;
            }

            w.Append(w.Rest, s_culture, $"\n  #{pointer.Id} {Name(s_pointerTypeNames, (int)pointer.Type)} {pointer.X:0.#}, {pointer.Y:0.#} buttons");
            for (var b = 0; b < PointerButtonCount; b++)
            {
                if ((pointer.Buttons & (1 << b)) != 0)
                {
                    w.Append(w.Rest, s_culture, $" {PointerButtonName(b)}");
                }
            }

            anyPointer = true;
        }

        w.Append(w.Rest, s_culture, $"{(anyPointer ? "" : "-")}\n");

        w.Append(w.Rest, s_culture, $"Gamepads   ");
        var anyGamepad = false;
        for (var p = 0; p < MaxGamepads; p++)
        {
            if (!_gamepadConnected[p])
            {
                continue;
            }

            w.Append(w.Rest, s_culture, $"\n  #{p} {(_gamepadStandard[p] ? "standard" : "raw")} buttons");
            for (var b = 0; b < MaxGamepadButtons; b++)
            {
                if ((_gamepadButtons[p] & (1u << b)) != 0)
                {
                    w.Append(w.Rest, s_culture, $" {b}");
                }
            }

            w.Append(w.Rest, s_culture, $"\n     axes");
            for (var a = 0; a < MaxGamepadAxes; a++)
            {
                w.Append(w.Rest, s_culture, $" {_gamepadAxes[(p * MaxGamepadAxes) + a],5:+0.00;-0.00; 0.00}");
            }

            anyGamepad = true;
        }

        w.Append(w.Rest, s_culture, $"{(anyGamepad ? "" : "-")}\n\nLast events (frame, kind, detail, code, id, time, x, y, z)\n");
        for (var i = 0; i < HistoryCount; i++)
        {
            var slot = (_historyStart + i) % HistorySize;
            ref readonly var e = ref _history[slot];
            w.Append(w.Rest, s_culture, $"  {_historyFrames[slot],6} {Name(s_kindNames, (int)e.Kind),-19} {e.Detail,3} {e.Code,5} {e.Id,6} {e.Time * 1000,7:0.0}ms {e.X,8:0.##} {e.Y,8:0.##} {e.Z,5:0.##}\n");
        }

        return w.Written;
    }

    private static string[] NameTable<TEnum>(int size)
        where TEnum : struct, Enum
    {
        var names = new string[size];
        Array.Fill(names, "?");
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var index = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if ((uint)index < (uint)size)
            {
                names[index] = value.ToString();
            }
        }

        return names;
    }

    private static string Name(string[] names, int value) => (uint)value < (uint)names.Length ? names[value] : "?";

    private static string PointerButtonName(int button) => button switch
    {
        0 => "left",
        1 => "middle",
        2 => "right",
        3 => "back",
        4 => "forward",
        _ => "eraser",
    };

    private void Apply(in RawInputEvent e)
    {
        switch (e.Kind)
        {
            case RawInputEventKind.Overflow:
                DroppedEvents += e.Id;
                ReleaseAll();
                break;
            case RawInputEventKind.FocusLost:
                Focused = false;
                ReleaseAll();
                break;
            case RawInputEventKind.FocusGained:
                Focused = true;
                break;
            case RawInputEventKind.Resize:
                SurfaceWidth = e.X;
                SurfaceHeight = e.Y;
                PixelRatio = e.Z;
                break;
            case RawInputEventKind.KeyDown or RawInputEventKind.KeyUp:
                if (e.Code < KeyCount)
                {
                    _keysDown[e.Code] = e.Kind == RawInputEventKind.KeyDown;
                }

                break;
            case RawInputEventKind.PointerDown or RawInputEventKind.PointerUp or RawInputEventKind.PointerMove:
                ApplyPointer(in e);
                break;
            case RawInputEventKind.PointerMotion:
                MotionX += e.X;
                MotionY += e.Y;
                break;
            case RawInputEventKind.PointerCancel:
                var cancelled = FindPointer(e.Id);
                if (cancelled >= 0)
                {
                    _pointers[cancelled] = default;
                }

                break;
            case RawInputEventKind.PointerLockChanged:
                PointerLocked = e.Detail != 0;
                break;
            case RawInputEventKind.Wheel:
                WheelX += e.X;
                WheelY += e.Y;
                break;
            case RawInputEventKind.GamepadConnected or RawInputEventKind.GamepadDisconnected:
                if ((uint)e.Id < MaxGamepads)
                {
                    _gamepadConnected[e.Id] = e.Kind == RawInputEventKind.GamepadConnected;
                    _gamepadStandard[e.Id] = e.Detail != 0;
                    _gamepadButtons[e.Id] = 0;
                    _gamepadAxes.AsSpan(e.Id * MaxGamepadAxes, MaxGamepadAxes).Clear();
                }

                break;
            case RawInputEventKind.GamepadButton:
                if ((uint)e.Id < MaxGamepads && e.Code < MaxGamepadButtons)
                {
                    var mask = 1u << e.Code;
                    _gamepadButtons[e.Id] = e.Detail != 0 ? _gamepadButtons[e.Id] | mask : _gamepadButtons[e.Id] & ~mask;
                }

                break;
            case RawInputEventKind.GamepadAxis:
                if ((uint)e.Id < MaxGamepads && e.Code < MaxGamepadAxes)
                {
                    _gamepadAxes[(e.Id * MaxGamepadAxes) + e.Code] = e.X;
                }

                break;
            default:
                // Kinds added later are ignored, so an older consumer keeps working.
                break;
        }
    }

    private void ApplyPointer(in RawInputEvent e)
    {
        var index = FindPointer(e.Id);
        if (index < 0)
        {
            index = FindPointer(id: 0, active: false);
            if (index < 0)
            {
                // More pointers than slots: ignore the extra ones.
                return;
            }

            _pointers[index] = new Pointer { Id = e.Id, Active = true };
        }

        ref var pointer = ref _pointers[index];
        pointer.Type = (PointerType)e.Code;
        pointer.X = e.X;
        pointer.Y = e.Y;
        if (e.Kind != RawInputEventKind.PointerMove && e.Detail < PointerButtonCount)
        {
            var mask = 1 << e.Detail;
            pointer.Buttons = e.Kind == RawInputEventKind.PointerDown ? pointer.Buttons | mask : pointer.Buttons & ~mask;
        }

        // A lifted finger is gone; a mouse stays over the surface.
        if (e.Kind == RawInputEventKind.PointerUp && pointer.Buttons == 0 && pointer.Type == PointerType.Touch)
        {
            pointer = default;
        }
    }

    private int FindPointer(int id, bool active = true)
    {
        for (var i = 0; i < _pointers.Length; i++)
        {
            ref readonly var pointer = ref _pointers[i];
            if (pointer.Active == active && (!active || pointer.Id == id))
            {
                return i;
            }
        }

        return -1;
    }

    private void ReleaseAll()
    {
        Array.Clear(_keysDown);
        foreach (ref var pointer in _pointers.AsSpan())
        {
            pointer.Buttons = 0;
        }

        Array.Clear(_gamepadButtons);
    }

    private void Remember(in RawInputEvent e, long frameIndex)
    {
        int slot;
        if (HistoryCount < HistorySize)
        {
            slot = (_historyStart + HistoryCount) % HistorySize;
            HistoryCount++;
        }
        else
        {
            slot = _historyStart;
            _historyStart = (_historyStart + 1) % HistorySize;
        }

        _history[slot] = e;
        _historyFrames[slot] = frameIndex;
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Pointer
    {
        public int Id;
        public bool Active;
        public PointerType Type;
        public float X;
        public float Y;
        public int Buttons;
    }

    /// <summary>
    /// Appends formatted UTF-8 to a span and stops at the first piece that does not fit. Pass <see cref="Rest"/> as
    /// the first argument of <see cref="Append"/>: the handler formats straight into it.
    /// </summary>
    private ref struct ReportWriter(Span<byte> destination)
    {
        /// <summary>
        /// The unwritten part of the destination; empty once a piece did not fit.
        /// </summary>
        public Span<byte> Rest { get; private set; } = destination;

        public int Written { get; private set; }

        public void Append(
            Span<byte> rest,
            IFormatProvider provider,
            [InterpolatedStringHandlerArgument(nameof(rest), nameof(provider))] ref Utf8.TryWriteInterpolatedStringHandler handler)
        {
            if (Utf8.TryWrite(rest, provider, ref handler, out var count))
            {
                Written += count;
                Rest = Rest[count..];
            }
            else
            {
                Rest = default;
            }
        }
    }
}
