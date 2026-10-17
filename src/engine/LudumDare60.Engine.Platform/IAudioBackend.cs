namespace LudumDare60.Engine.Platform;

/// <summary>
/// A platform's sound output. The engine creates sounds through it and submits one command stream per frame. The engine
/// never calls it before the platform's first frame, so a backend can set up its device in <see cref="IPlatform.RunAsync"/>.
/// </summary>
/// <remarks>
/// Decoding is asynchronous: <see cref="CreateSound"/> starts it, and its result arrives later as an
/// <see cref="AudioEvent"/> from <see cref="Submit"/>. Commands may refer to a sound that is still decoding.
/// </remarks>
public interface IAudioBackend
{
    /// <summary>
    /// Creates sound <paramref name="id"/> and starts decoding <paramref name="data"/>. Reports
    /// <see cref="AudioEventKind.SoundReady"/> or <see cref="AudioEventKind.SoundFailed"/> through a later
    /// <see cref="Submit"/>.
    /// </summary>
    /// <param name="id">Chosen by the engine: zero or more, unused until <see cref="DestroySound"/> frees it.</param>
    /// <param name="data">The encoded file (WAV, OGG, ...). Read during the call only.</param>
    void CreateSound(int id, ReadOnlySpan<byte> data);

    /// <summary>
    /// Stops every voice that plays sound <paramref name="id"/> and deletes the sound. No event is reported for it
    /// afterwards, even if it was still decoding. The id may be created again afterwards.
    /// </summary>
    void DestroySound(int id);

    /// <summary>
    /// Runs one frame of audio commands and reports what happened since the previous call.
    /// </summary>
    /// <param name="commands">The frame's commands, see <see cref="AudioCommandKind"/>. Read during the call only.</param>
    /// <param name="events">Receives the events, oldest first. Events that do not fit are kept for the next call.</param>
    /// <returns>Number of events written to <paramref name="events"/>.</returns>
    int Submit(ReadOnlySpan<byte> commands, Span<AudioEvent> events);
}
