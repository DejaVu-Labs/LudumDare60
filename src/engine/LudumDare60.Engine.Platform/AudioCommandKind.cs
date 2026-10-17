namespace LudumDare60.Engine.Platform;

/// <summary>
/// Opcode of a command in the audio command stream that <see cref="IAudioBackend.Submit"/> receives.
/// </summary>
/// <remarks>
/// A command is a 32-bit opcode followed by its arguments, every one 32 bits wide and little-endian: <c>int32</c> for
/// ids and buses, <c>float32</c> for the rest. Each member lists its arguments in order. Values are part of the wire
/// format shared with backend code (for example <c>webaudio.js</c>): never renumber.
/// <para>Volumes are linear gains (1 = unchanged). Pitch is a playback rate (2 = an octave up and twice as fast). Pan
/// runs from -1 (left) to 1 (right). Buses are <see cref="AudioBus"/> values. Changes to a playing voice or a bus are
/// smoothed over about 10 ms, so they never click.</para>
/// </remarks>
public enum AudioCommandKind
{
    /// <summary>
    /// Never written. A backend that reads it has lost track of the stream.
    /// </summary>
    None = 0,

    /// <summary>
    /// Plays a sound once, fire and forget. Arguments: sound id, bus, volume, pitch, pan. The backend drops it while
    /// audio is locked (no user gesture yet) and when <see cref="AudioLimits.MaxVoices"/> voices already play.
    /// </summary>
    Play = 1,

    /// <summary>
    /// Starts a looping voice. Arguments: voice id, sound id, bus, volume, pitch, pan, fade-in seconds. A sound that
    /// is still decoding starts once it is ready. Loops are never dropped. The voice id is chosen by the engine and
    /// stays in use until <see cref="StopVoice"/>.
    /// </summary>
    PlayLoop = 2,

    /// <summary>
    /// Changes a looping voice. Arguments: voice id, volume, pitch, pan.
    /// </summary>
    SetVoice = 3,

    /// <summary>
    /// Fades a looping voice out and stops it. Arguments: voice id, fade-out seconds. The voice id is free once the
    /// command runs, even while the fade is still audible.
    /// </summary>
    StopVoice = 4,

    /// <summary>
    /// Sets a bus volume. Arguments: bus, volume.
    /// </summary>
    SetBusVolume = 5,
}
