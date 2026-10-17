namespace LudumDare60.Engine.Platform;

/// <summary>
/// A volume group. Every voice plays on one of <see cref="Sfx"/>, <see cref="Ambience"/> or <see cref="Music"/>, and
/// those three feed <see cref="Master"/>. Values are part of the audio wire format (see
/// <see cref="AudioCommandKind"/>): never renumber.
/// </summary>
public enum AudioBus
{
    /// <summary>
    /// Everything. Only its volume can be set; nothing plays on it directly.
    /// </summary>
    Master = 0,

    /// <summary>
    /// Sound effects: clicks, clinks, squeaks.
    /// </summary>
    Sfx = 1,

    /// <summary>
    /// Background loops: rain, traffic, room tone.
    /// </summary>
    Ambience = 2,

    /// <summary>
    /// Music tracks.
    /// </summary>
    Music = 3,
}
