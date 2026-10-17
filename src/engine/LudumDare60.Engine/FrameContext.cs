using System.Runtime.InteropServices;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Engine;

/// <summary>
/// Timing of a single <see cref="IGame.Update"/> or <see cref="IGame.FixedUpdate"/> call, and where the virtual screen
/// is. Times are in seconds.
/// </summary>
/// <param name="DeltaTime">Time covered by this call.</param>
/// <param name="TotalTime">Time elapsed since the start, including this call.</param>
/// <param name="FrameIndex">Zero-based index of this call. Update frames and fixed steps are counted separately.</param>
/// <param name="Viewport">The virtual screen on the drawing surface, after this frame's resize events. Convert pointer
/// positions with <see cref="PixelViewport.ToVirtual"/>.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FrameContext(double DeltaTime, double TotalTime, long FrameIndex, PixelViewport Viewport = default);
