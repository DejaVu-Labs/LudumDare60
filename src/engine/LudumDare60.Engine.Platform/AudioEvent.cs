using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform;

/// <summary>
/// A report from the audio backend, returned by <see cref="IAudioBackend.Submit"/>. Its layout is the wire format: 8
/// bytes, both fields <c>int32</c>, little-endian.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct AudioEvent(AudioEventKind Kind, int SoundId)
{
    /// <summary>
    /// Size of one event in bytes.
    /// </summary>
    public const int Size = 8;
}
