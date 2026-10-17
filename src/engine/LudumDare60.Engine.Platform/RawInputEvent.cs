using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform;

/// <summary>
/// One raw input event from the platform. What each field means depends on <see cref="Kind"/>: see
/// <see cref="RawInputEventKind"/>.
/// </summary>
/// <remarks>
/// The layout is the wire format: platforms write events as bytes (little-endian, <see cref="Size"/> bytes each) and
/// the engine reads them in place with <see cref="MemoryMarshal.Cast{TFrom, TTo}(Span{TFrom})"/>. Field order and
/// types must not change without changing every platform that writes them.
/// <code>
/// offset 0  Kind    byte
/// offset 1  Detail  byte
/// offset 2  Code    ushort
/// offset 4  Id      int
/// offset 8  Time    float
/// offset 12 X       float
/// offset 16 Y       float
/// offset 20 Z       float
/// </code>
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct RawInputEvent
{
    /// <summary>
    /// Size of one event in bytes.
    /// </summary>
    public const int Size = 24;

    private readonly byte _kind;

    public RawInputEvent(RawInputEventKind kind, byte detail, ushort code, int id, float time, float x, float y, float z)
    {
        _kind = (byte)kind;
        Detail = detail;
        Code = code;
        Id = id;
        Time = time;
        X = x;
        Y = y;
        Z = z;
    }

    public RawInputEventKind Kind => (RawInputEventKind)_kind;

    /// <summary>
    /// A small value: pointer button, flag or state.
    /// </summary>
    public byte Detail { get; }

    /// <summary>
    /// <see cref="Platform.Key"/>, <see cref="PointerType"/>, or a gamepad button or axis index.
    /// </summary>
    public ushort Code { get; }

    /// <summary>
    /// Pointer id, gamepad index or a count.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// When the event happened, in seconds relative to the start of the frame that delivers it. Usually in
    /// [-dt, 0]; never positive.
    /// </summary>
    /// <remarks>Events are in arrival order, not sorted by time: events of different devices may interleave.</remarks>
    public float Time { get; }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    /// <summary>
    /// <see cref="Code"/> as a key, for <see cref="RawInputEventKind.KeyDown"/> and <see cref="RawInputEventKind.KeyUp"/>.
    /// </summary>
    public Key Key => (Key)Code;
}
