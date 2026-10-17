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
    void Update(double dt);

    /// <summary>
    /// Draws the frame prepared by the last <see cref="Update"/>.
    /// </summary>
    void Render();
}
