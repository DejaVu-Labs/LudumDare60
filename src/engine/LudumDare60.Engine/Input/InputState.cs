using System.Numerics;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Engine.Input;

/// <summary>
/// Keys, pointer buttons, pointer position and wheel, folded from raw input events. The game reads it through
/// <see cref="FrameContext.Input"/>.
/// </summary>
/// <remarks>
/// "Pressed" and "released" are edges: they hold for one fixed step in <see cref="IGame.FixedUpdate"/>, and for one
/// frame in <see cref="IGame.Update"/>. A key pressed and released within the same step is both pressed and released,
/// and not down. <see cref="RawInputEventKind.FocusLost"/> and <see cref="RawInputEventKind.Overflow"/> release
/// everything that is down, with released edges, because the matching up events may never arrive.
/// <para>Pointer buttons are merged across pointers, for one mouse or one finger at a time: a second touch while the
/// first is down changes nothing, and the first release releases the button. The position is the last one any pointer
/// reported.</para>
/// </remarks>
public sealed class InputState
{
    private const int KeyCount = 256;
    private const int ButtonCount = 8;

    private const byte Down = 1;
    private const byte Pressed = 2;
    private const byte Released = 4;

    private readonly byte[] _keys = new byte[KeyCount];
    private readonly byte[] _buttons = new byte[ButtonCount];

    internal InputState()
    {
    }

    /// <summary>
    /// Where the pointer is, in virtual pixels. Outside the virtual screen when the pointer is on the bars around it:
    /// check <see cref="IsPointerOnScreen"/>. Starts at the center of the screen.
    /// </summary>
    public Vector2 PointerPosition { get; private set; }

    /// <summary>
    /// How far the pointer moved during this step or frame, in virtual pixels. Includes the motion of a locked pointer.
    /// </summary>
    public Vector2 PointerDelta { get; private set; }

    /// <summary>
    /// Whether <see cref="PointerPosition"/> lies on the virtual screen.
    /// </summary>
    public bool IsPointerOnScreen { get; private set; }

    /// <summary>
    /// Wheel motion during this step or frame, in logical pixels (positive y scrolls down).
    /// </summary>
    public Vector2 Wheel { get; private set; }

    /// <summary>
    /// Whether the game has keyboard focus and is visible.
    /// </summary>
    public bool HasFocus { get; private set; }

    public bool IsDown(Key key) => Has(_keys, (int)key, Down);

    /// <summary>
    /// Whether <paramref name="key"/> went down during this step or frame.
    /// </summary>
    public bool IsPressed(Key key) => Has(_keys, (int)key, Pressed);

    /// <summary>
    /// Whether <paramref name="key"/> went up during this step or frame.
    /// </summary>
    public bool IsReleased(Key key) => Has(_keys, (int)key, Released);

    public bool IsDown(PointerButton button) => Has(_buttons, (int)button, Down);

    /// <inheritdoc cref="IsPressed(Key)"/>
    public bool IsPressed(PointerButton button) => Has(_buttons, (int)button, Pressed);

    /// <inheritdoc cref="IsReleased(Key)"/>
    public bool IsReleased(PointerButton button) => Has(_buttons, (int)button, Released);

    /// <summary>
    /// Folds one event into the state. Pointer positions are converted with <paramref name="viewport"/>.
    /// </summary>
    internal void Apply(in RawInputEvent e, in PixelViewport viewport)
    {
        switch (e.Kind)
        {
            case RawInputEventKind.KeyDown:
                Press(_keys, e.Code);
                break;
            case RawInputEventKind.KeyUp:
                Release(_keys, e.Code);
                break;
            case RawInputEventKind.PointerDown:
                MoveTo(viewport.ToVirtual(e.X, e.Y), viewport);
                Press(_buttons, e.Detail);
                break;
            case RawInputEventKind.PointerUp:
                MoveTo(viewport.ToVirtual(e.X, e.Y), viewport);
                Release(_buttons, e.Detail);
                break;
            case RawInputEventKind.PointerMove:
                MoveTo(viewport.ToVirtual(e.X, e.Y), viewport);
                break;
            case RawInputEventKind.PointerMotion:
                // Relative motion: logical pixels scaled like positions, without the bar offset.
                PointerDelta += new Vector2(e.X, e.Y) * viewport.PixelRatio * viewport.VirtualWidth / viewport.Width;
                break;
            case RawInputEventKind.PointerCancel:
                ReleaseAll(_buttons);
                break;
            case RawInputEventKind.Wheel:
                Wheel += new Vector2(e.X, e.Y);
                break;
            case RawInputEventKind.FocusGained:
                HasFocus = true;
                break;
            case RawInputEventKind.FocusLost:
                HasFocus = false;
                ReleaseAll(_keys);
                ReleaseAll(_buttons);
                break;
            case RawInputEventKind.Overflow:
                ReleaseAll(_keys);
                ReleaseAll(_buttons);
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Starts a new step or frame: forgets the edges, the pointer delta and the wheel.
    /// </summary>
    internal void ClearEdges()
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            _keys[i] &= Down;
        }

        for (var i = 0; i < _buttons.Length; i++)
        {
            _buttons[i] &= Down;
        }

        PointerDelta = Vector2.Zero;
        Wheel = Vector2.Zero;
    }

    /// <summary>
    /// Forgets everything: nothing down, no edges, the pointer at the center of the screen, no focus.
    /// </summary>
    internal void Reset(in PixelViewport viewport)
    {
        Array.Clear(_keys);
        Array.Clear(_buttons);
        PointerDelta = Vector2.Zero;
        Wheel = Vector2.Zero;
        HasFocus = false;
        PointerPosition = new Vector2(viewport.VirtualWidth / 2f, viewport.VirtualHeight / 2f);
        IsPointerOnScreen = true;
    }

    private void MoveTo(Vector2 position, in PixelViewport viewport)
    {
        PointerDelta += position - PointerPosition;
        PointerPosition = position;
        IsPointerOnScreen = viewport.Contains(position);
    }

    private static bool Has(byte[] states, int index, byte flag) => (uint)index < (uint)states.Length && (states[index] & flag) != 0;

    private static void Press(byte[] states, int index)
    {
        if ((uint)index < (uint)states.Length && (states[index] & Down) == 0)
        {
            states[index] |= Down | Pressed;
        }
    }

    private static void Release(byte[] states, int index)
    {
        if ((uint)index < (uint)states.Length && (states[index] & Down) != 0)
        {
            states[index] = (byte)((states[index] & ~Down) | Released);
        }
    }

    private static void ReleaseAll(byte[] states)
    {
        for (var i = 0; i < states.Length; i++)
        {
            Release(states, i);
        }
    }
}
