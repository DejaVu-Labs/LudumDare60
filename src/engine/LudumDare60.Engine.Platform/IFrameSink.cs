namespace LudumDare60.Engine.Platform;

/// <summary>
/// Receives frames from an <see cref="IPlatform"/>. The platform calls <see cref="Update"/> and then
/// <see cref="Render"/> once per frame.
/// </summary>
public interface IFrameSink
{
    /// <summary>
    /// Advances the frame.
    /// </summary>
    /// <param name="dt">Time since the previous frame, in seconds.</param>
    /// <param name="events">Raw input events that arrived since the previous frame, in arrival order. The span is
    /// valid only during this call.</param>
    void Update(double dt, ReadOnlySpan<RawInputEvent> events);

    /// <summary>
    /// Draws the frame prepared by the last <see cref="Update"/>.
    /// </summary>
    void Render();
}
