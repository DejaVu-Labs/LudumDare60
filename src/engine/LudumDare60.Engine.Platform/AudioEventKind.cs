namespace LudumDare60.Engine.Platform;

/// <summary>
/// What an <see cref="AudioEvent"/> reports. Values are part of the wire format shared with backend code: never
/// renumber.
/// </summary>
public enum AudioEventKind
{
    /// <summary>
    /// Never written.
    /// </summary>
    None = 0,

    /// <summary>
    /// A sound finished decoding and can play.
    /// </summary>
    SoundReady = 1,

    /// <summary>
    /// A sound could not be decoded (corrupt file, or a format this platform does not support). It never plays.
    /// </summary>
    SoundFailed = 2,
}
