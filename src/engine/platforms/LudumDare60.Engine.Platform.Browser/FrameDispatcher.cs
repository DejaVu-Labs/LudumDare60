namespace LudumDare60.Engine.Platform.Browser;

/// <summary>
/// Turns one browser frame into the <see cref="IFrameSink"/> calls for that frame.
/// </summary>
internal sealed class FrameDispatcher(IFrameSink frameSink)
{
    /// <param name="dt">Seconds since the previous frame.</param>
    public void OnFrame(double dt)
    {
        frameSink.Update(dt);
        frameSink.Render();
    }
}
