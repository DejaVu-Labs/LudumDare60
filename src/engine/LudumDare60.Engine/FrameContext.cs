using System.Runtime.InteropServices;
using LudumDare60.Engine.Input;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Engine;

/// <summary>
/// Timing of a single <see cref="IGame.Update"/> or <see cref="IGame.FixedUpdate"/> call, where the virtual screen is,
/// and the input. Times are in seconds.
/// </summary>
/// <param name="DeltaTime">Time covered by this call.</param>
/// <param name="TotalTime">Time elapsed since the start, including this call.</param>
/// <param name="FrameIndex">Zero-based index of this call. Update frames and fixed steps are counted separately.</param>
/// <param name="Viewport">The virtual screen on the drawing surface, after this frame's resize events. Convert pointer
/// positions with <see cref="PixelViewport.ToVirtual"/>.</param>
/// <param name="Input">Keys, pointer and wheel. In <see cref="IGame.FixedUpdate"/> it holds the input up to the end of
/// this step, with the edges (pressed, released) of this step; in <see cref="IGame.Update"/> the input up to the start
/// of this frame, with the edges of this frame. Valid only during the call.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FrameContext(
    double DeltaTime,
    double TotalTime,
    long FrameIndex,
    PixelViewport Viewport,
    InputState Input);
