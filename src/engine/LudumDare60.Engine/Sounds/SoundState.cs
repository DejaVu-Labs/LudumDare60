namespace LudumDare60.Engine.Sounds;

/// <summary>
/// Decode state of a sound created with <see cref="Audio.CreateSound"/>. It changes only at the end of a frame, when the
/// engine submits the frame's audio commands.
/// </summary>
public enum SoundState
{
    /// <summary>
    /// The platform is decoding the file. <see cref="Audio.Play(Sound, Engine.Platform.AudioBus, in SoundOptions)"/>
    /// skips the sound; loops and music start once it is ready.
    /// </summary>
    Decoding = 0,

    /// <summary>
    /// The sound can play.
    /// </summary>
    Ready = 1,

    /// <summary>
    /// The file could not be decoded: it is corrupt, or the platform does not support its format. The sound never
    /// plays; the platform logs the reason.
    /// </summary>
    Failed = 2,
}
