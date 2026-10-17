using System.Runtime.InteropServices;

namespace LudumDare60.Engine;

/// <summary>
/// Timing of a single <see cref="IGame.Update"/> or <see cref="IGame.FixedUpdate"/> call. Times are in seconds.
/// </summary>
/// <param name="DeltaTime">Time covered by this call.</param>
/// <param name="TotalTime">Time elapsed since the start, including this call.</param>
/// <param name="FrameIndex">Zero-based index of this call. Update frames and fixed steps are counted separately.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct FrameContext(double DeltaTime, double TotalTime, long FrameIndex);
