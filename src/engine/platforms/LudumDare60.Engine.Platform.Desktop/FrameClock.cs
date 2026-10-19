namespace LudumDare60.Engine.Platform.Desktop;

/// <summary>
/// Frame timing of the desktop loop: the delta time of each frame and how long to wait before the next one.
/// </summary>
internal sealed class FrameClock
{
    /// <summary>
    /// Refresh rate used when the display reports none.
    /// </summary>
    public const float DefaultRefreshRate = 60;

    private ulong _previousNs;
    private bool _started;

    /// <summary>
    /// Starts a frame.
    /// </summary>
    /// <param name="nowNs">Start of the frame, on the <c>SDL_GetTicksNS</c> timeline.</param>
    /// <returns>Seconds since the previous frame started; 0 for the first frame, as in the browser.</returns>
    public double Tick(ulong nowNs)
    {
        var dt = _started && nowNs > _previousNs ? (nowNs - _previousNs) / 1e9 : 0;
        _started = true;
        _previousNs = nowNs;
        return dt;
    }

    /// <summary>
    /// Nanoseconds per frame at <paramref name="refreshRate"/> Hz. Rates that are not positive and finite count as
    /// <see cref="DefaultRefreshRate"/>.
    /// </summary>
    public static ulong GetFramePeriodNs(float refreshRate)
        => (ulong)(1e9 / (float.IsFinite(refreshRate) && refreshRate > 0 ? refreshRate : DefaultRefreshRate));

    /// <summary>
    /// How long to wait so the next frame starts one <paramref name="periodNs"/> after <paramref name="frameStartNs"/>.
    /// </summary>
    /// <returns>Nanoseconds; 0 when the frame took the whole period or longer.</returns>
    public static ulong GetWaitNs(ulong frameStartNs, ulong nowNs, ulong periodNs)
    {
        var nextNs = frameStartNs + periodNs;
        return nowNs < nextNs ? nextNs - nowNs : 0;
    }
}
