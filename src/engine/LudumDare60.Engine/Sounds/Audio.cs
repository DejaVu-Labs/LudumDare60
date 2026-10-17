using System.Buffers.Binary;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Sounds;

/// <summary>
/// Creates sounds and plays them for a run. The engine passes it to <see cref="IGame.Initialize"/>.
/// </summary>
/// <remarks>
/// <para>Calls only record commands. The engine submits the frame's commands to the platform in one call at the end of
/// the frame, after the render commands, and applies the platform's decode results at the same point. Commands may be
/// recorded in <see cref="IGame.Initialize"/>, <see cref="IGame.FixedUpdate"/> and <see cref="IGame.Update"/>, but not
/// in <see cref="IGame.PrepareRender"/> or <see cref="IGame.Render"/>, which only draw.</para>
/// <para>Sounds can be created from the first frame on, like textures. Every sound still alive when the run ends is
/// destroyed, and the bus volumes go back to 1.</para>
/// </remarks>
public sealed class Audio
{
    /// <summary>
    /// Largest <see cref="SoundOptions.Pitch"/>.
    /// </summary>
    public const float MaxPitch = 16;

    /// <summary>
    /// Bytes of audio commands one frame can hold: room for about 2000 commands.
    /// </summary>
    internal const int CommandCapacity = 64 * 1024;

    /// <summary>
    /// Backend events read per frame. Events beyond it stay queued in the backend and arrive with the next frame.
    /// </summary>
    internal const int EventCapacity = 256;

    private const int BusCount = 4;
    private const int PlaySize = 6 * sizeof(int);
    private const int PlayLoopSize = 8 * sizeof(int);
    private const int SetVoiceSize = 5 * sizeof(int);
    private const int StopVoiceSize = 3 * sizeof(int);
    private const int SetBusVolumeSize = 3 * sizeof(int);

    private readonly List<SoundSlot> _sounds = [];
    private readonly Stack<int> _freeSoundIds = new();
    private readonly List<int> _pendingDestroys = [];
    private readonly List<VoiceSlot> _voices = [];
    private readonly Stack<int> _freeVoiceIds = new();
    private readonly byte[] _commands = new byte[CommandCapacity];
    private readonly AudioEvent[] _events = new AudioEvent[EventCapacity];
    private readonly float[] _busVolumes = [1, 1, 1, 1];
    private int _commandLength;
    private bool _started;
    private bool _rendering;
    private Voice _musicVoice;
    private Sound _musicSound;
    private bool _hasMusic;

    internal Audio(IAudioBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        Backend = backend;
    }

    /// <summary>
    /// Number of live sounds.
    /// </summary>
    public int SoundCount { get; private set; }

    /// <summary>
    /// Number of live looping voices started with <see cref="PlayLoop"/>, music excluded.
    /// </summary>
    public int LoopCount { get; private set; }

    /// <summary>
    /// The track <see cref="PlayMusic"/> started last, or <c>null</c> when no music plays (or it is fading out after
    /// <see cref="StopMusic"/>).
    /// </summary>
    public Sound? Music => _hasMusic ? _musicSound : null;

    internal IAudioBackend Backend { get; }

    /// <summary>
    /// Hands <paramref name="data"/>, an encoded file, to the platform for decoding. The sound starts in
    /// <see cref="SoundState.Decoding"/>; the platform keeps its own copy of the bytes, so the caller may drop them (for
    /// example release the asset) right away.
    /// </summary>
    /// <param name="data">The whole file, for example a <c>byte[]</c> asset. Ship WAV for short effects and OGG Vorbis
    /// for long loops; see docs/tech/architecture.md.</param>
    /// <exception cref="InvalidOperationException">Called before the first frame, for example from
    /// <see cref="IGame.Initialize"/>, or while rendering.</exception>
    /// <exception cref="ArgumentException"><paramref name="data"/> is empty.</exception>
    public Sound CreateSound(ReadOnlySpan<byte> data)
    {
        EnsureNotRendering();
        if (!_started)
        {
            throw new InvalidOperationException(
                "Sounds can be created from the first frame on, not in Initialize: the audio device is not set up yet.");
        }

        if (data.IsEmpty)
        {
            throw new ArgumentException("A sound file cannot be empty.", nameof(data));
        }

        if (!_freeSoundIds.TryPop(out var id))
        {
            id = _sounds.Count;
            _sounds.Add(default);
        }

        Backend.CreateSound(id, data);

        var generation = _sounds[id].Generation + 1;
        _sounds[id] = new SoundSlot(generation, Alive: true, OnBackend: true, SoundState.Decoding);
        SoundCount++;
        return new Sound(id, generation);
    }

    /// <summary>
    /// Destroys <paramref name="sound"/> and stops every voice that plays it, including the music if it is the current
    /// track. The handle and those voices are stale at once.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handle is stale, or called while rendering.</exception>
    public void DestroySound(Sound sound)
    {
        EnsureNotRendering();
        Validate(sound);
        var id = sound.Id;
        for (var voiceId = 0; voiceId < _voices.Count; voiceId++)
        {
            if (_voices[voiceId] is { Alive: true } voice && voice.SoundId == id)
            {
                var isMusic = _hasMusic && _musicVoice.Id == voiceId;
                StopVoice(voiceId, 0);
                if (isMusic)
                {
                    _hasMusic = false;
                }
                else
                {
                    LoopCount--;
                }
            }
        }

        _sounds[id] = _sounds[id] with { Alive = false };
        _pendingDestroys.Add(id);
        SoundCount--;
    }

    /// <summary>
    /// Whether <paramref name="sound"/> is a live sound of this run.
    /// </summary>
    public bool IsAlive(Sound sound)
    {
        var id = sound.Id;
        return (uint)id < (uint)_sounds.Count && _sounds[id] is { Alive: true } slot && slot.Generation == sound.Generation;
    }

    /// <exception cref="InvalidOperationException">The handle is stale.</exception>
    public SoundState GetState(Sound sound)
    {
        Validate(sound);
        return _sounds[sound.Id].State;
    }

    /// <summary>
    /// Plays <paramref name="sound"/> once on the <see cref="AudioBus.Sfx"/> bus at full volume, original pitch,
    /// centered.
    /// </summary>
    /// <inheritdoc cref="Play(Sound, AudioBus, in SoundOptions)"/>
    public void Play(Sound sound) => Play(sound, AudioBus.Sfx, SoundOptions.Default);

    /// <summary>
    /// Plays <paramref name="sound"/> once, fire and forget. Skipped while the sound is not
    /// <see cref="SoundState.Ready"/>. The platform also drops it while audio is locked (before the player's first
    /// click or key press) and when <see cref="AudioLimits.MaxVoices"/> voices already play.
    /// </summary>
    /// <param name="sound">The sound.</param>
    /// <param name="bus">Any bus but <see cref="AudioBus.Master"/>.</param>
    /// <param name="options">Volume, pitch and pan. Vary the pitch a little per play so repeated effects do not sound
    /// mechanical.</param>
    /// <exception cref="InvalidOperationException">The handle is stale, or called while rendering.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The bus is <see cref="AudioBus.Master"/> or unknown, or an option
    /// is out of range.</exception>
    public void Play(Sound sound, AudioBus bus, in SoundOptions options)
    {
        EnsureNotRendering();
        Validate(sound);
        ValidatePlayBus(bus);
        ValidateOptions(options);
        if (_sounds[sound.Id].State != SoundState.Ready)
        {
            return;
        }

        var command = Reserve(PlaySize);
        WriteOpcode(command, AudioCommandKind.Play);
        WriteInt(command, 1, sound.Id);
        WriteInt(command, 2, (int)bus);
        WriteOptions(command, 3, options);
    }

    /// <summary>
    /// Starts <paramref name="sound"/> as a loop that plays until <see cref="Stop"/>. A sound that is still decoding
    /// starts once it is ready; one that failed stays silent, but the voice is still valid.
    /// </summary>
    /// <param name="sound">The sound.</param>
    /// <param name="bus">Any bus but <see cref="AudioBus.Master"/>.</param>
    /// <param name="options">Volume, pitch and pan; change them later with <see cref="SetVoice"/>.</param>
    /// <param name="fadeInSeconds">Time to rise from silence to the volume. 0 starts at once.</param>
    /// <returns>A handle for <see cref="SetVoice"/> and <see cref="Stop"/>.</returns>
    /// <exception cref="InvalidOperationException">The handle is stale, or called while rendering.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The bus is <see cref="AudioBus.Master"/> or unknown, or an option
    /// or the fade is out of range.</exception>
    public Voice PlayLoop(Sound sound, AudioBus bus, in SoundOptions options, float fadeInSeconds = 0)
    {
        EnsureNotRendering();
        Validate(sound);
        ValidatePlayBus(bus);
        ValidateOptions(options);
        ValidateFade(fadeInSeconds, nameof(fadeInSeconds));
        var voice = StartLoop(sound, bus, options, fadeInSeconds);
        LoopCount++;
        return voice;
    }

    /// <summary>
    /// Changes the volume, pitch and pan of a playing loop. The change is smoothed over about 10 ms, so calling it every
    /// frame (for example to follow the pointer speed) does not click.
    /// </summary>
    /// <exception cref="InvalidOperationException">The voice is stale, or called while rendering.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is out of range.</exception>
    public void SetVoice(Voice voice, in SoundOptions options)
    {
        EnsureNotRendering();
        Validate(voice);
        ValidateOptions(options);
        var command = Reserve(SetVoiceSize);
        WriteOpcode(command, AudioCommandKind.SetVoice);
        WriteInt(command, 1, voice.Id);
        WriteOptions(command, 2, options);
    }

    /// <summary>
    /// Fades a loop out and stops it. The handle is stale at once.
    /// </summary>
    /// <param name="voice">A voice from <see cref="PlayLoop"/>.</param>
    /// <param name="fadeOutSeconds">Time to fall to silence. 0 stops at once (with a few milliseconds of fade so it does
    /// not click).</param>
    /// <exception cref="InvalidOperationException">The voice is stale, or called while rendering.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The fade is out of range.</exception>
    public void Stop(Voice voice, float fadeOutSeconds = 0)
    {
        EnsureNotRendering();
        Validate(voice);
        ValidateFade(fadeOutSeconds, nameof(fadeOutSeconds));
        StopVoice(voice.Id, fadeOutSeconds);
        LoopCount--;
    }

    /// <summary>
    /// Whether <paramref name="voice"/> is a loop of this run that has not been stopped.
    /// </summary>
    public bool IsPlaying(Voice voice)
    {
        var id = voice.Id;
        return (uint)id < (uint)_voices.Count && _voices[id] is { Alive: true } slot && slot.Generation == voice.Generation;
    }

    /// <summary>
    /// Plays <paramref name="sound"/> as the music track, looped on the <see cref="AudioBus.Music"/> bus. The previous
    /// track fades out while the new one fades in. Does nothing when <paramref name="sound"/> already is the current
    /// track.
    /// </summary>
    /// <param name="sound">The track. One that is still decoding starts once it is ready.</param>
    /// <param name="crossfadeSeconds">Time for the fade out of the old track and the fade in of the new one.</param>
    /// <param name="volume">Volume of the track, at least 0.</param>
    /// <exception cref="InvalidOperationException">The handle is stale, or called while rendering.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The fade or the volume is out of range.</exception>
    public void PlayMusic(Sound sound, float crossfadeSeconds = 1, float volume = 1)
    {
        EnsureNotRendering();
        Validate(sound);
        ValidateFade(crossfadeSeconds, nameof(crossfadeSeconds));
        var options = new SoundOptions { Volume = volume };
        ValidateOptions(options);
        if (_hasMusic && _musicSound == sound)
        {
            return;
        }

        StopMusicVoice(crossfadeSeconds);
        _musicVoice = StartLoop(sound, AudioBus.Music, options, crossfadeSeconds);
        _musicSound = sound;
        _hasMusic = true;
    }

    /// <summary>
    /// Fades the music out and stops it. Does nothing when no music plays.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called while rendering.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The fade is out of range.</exception>
    public void StopMusic(float fadeSeconds = 1)
    {
        EnsureNotRendering();
        ValidateFade(fadeSeconds, nameof(fadeSeconds));
        StopMusicVoice(fadeSeconds);
    }

    /// <summary>
    /// Sets the volume of <paramref name="bus"/>. <see cref="AudioBus.Master"/> scales the other three; set it to 0 to
    /// mute everything.
    /// </summary>
    /// <param name="bus">The bus.</param>
    /// <param name="volume">Linear gain, at least 0.</param>
    /// <exception cref="InvalidOperationException">Called while rendering.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The bus is unknown or the volume out of range.</exception>
    public void SetBusVolume(AudioBus bus, float volume)
    {
        EnsureNotRendering();
        ValidateBus(bus);
        ValidateVolume(volume, nameof(volume));
        _busVolumes[(int)bus] = volume;
        WriteBusVolume(bus, volume);
    }

    /// <summary>
    /// The volume last set with <see cref="SetBusVolume"/>; 1 at the start of a run.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The bus is unknown.</exception>
    public float GetBusVolume(AudioBus bus)
    {
        ValidateBus(bus);
        return _busVolumes[(int)bus];
    }

    /// <exception cref="InvalidOperationException">The handle is stale.</exception>
    internal void Validate(Sound sound)
    {
        if (!IsAlive(sound))
        {
            throw new InvalidOperationException("The sound was destroyed, or belongs to an earlier run.");
        }
    }

    /// <summary>
    /// Called by the engine at the start of every frame: sounds can be created from now on, and commands may be recorded.
    /// </summary>
    internal void BeginFrame()
    {
        _started = true;
        _rendering = false;
    }

    /// <summary>
    /// Called by the engine before <see cref="IGame.PrepareRender"/>: recording commands throws until
    /// <see cref="EndFrame"/>.
    /// </summary>
    internal void BeginRender() => _rendering = true;

    /// <summary>
    /// Called by the engine after the frame is rendered: submits the frame's commands, applies the platform's events,
    /// then deletes the sounds destroyed during the frame and frees their ids.
    /// </summary>
    internal void EndFrame()
    {
        _rendering = false;
        var count = Backend.Submit(_commands.AsSpan(0, _commandLength), _events);
        _commandLength = 0;
        if ((uint)count > EventCapacity)
        {
            throw new InvalidOperationException("The audio backend reported more events than the buffer holds.");
        }

        for (var i = 0; i < count; i++)
        {
            ApplyEvent(_events[i]);
        }

        foreach (var id in _pendingDestroys)
        {
            Backend.DestroySound(id);
            _sounds[id] = _sounds[id] with { OnBackend = false };
            _freeSoundIds.Push(id);
        }

        _pendingDestroys.Clear();
    }

    /// <summary>
    /// Called by the engine when the run ends: deletes every sound, live or destroyed during the last frame, and resets
    /// the bus volumes. Every handle of the run becomes stale. A backend that throws does not stop it: the other sounds
    /// are deleted too, and the failures are thrown afterwards as one <see cref="AggregateException"/>.
    /// </summary>
    internal void DestroyAll()
    {
        var started = _started;
        _started = false;
        _rendering = false;
        _commandLength = 0;
        _pendingDestroys.Clear();
        _freeSoundIds.Clear();
        _freeVoiceIds.Clear();
        _hasMusic = false;
        SoundCount = 0;
        LoopCount = 0;
        for (var id = _voices.Count - 1; id >= 0; id--)
        {
            _voices[id] = _voices[id] with { Alive = false };
            _freeVoiceIds.Push(id);
        }

        List<Exception>? failures = null;
        for (var id = _sounds.Count - 1; id >= 0; id--)
        {
            var slot = _sounds[id];
            _sounds[id] = slot with { Alive = false, OnBackend = false };
            _freeSoundIds.Push(id);
            if (!slot.OnBackend)
            {
                continue;
            }

            try
            {
                Backend.DestroySound(id);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                (failures ??= []).Add(exception);
            }
        }

        // The backend outlives the run: put back the bus volumes this run changed, so the next run starts at 1.
        if (started && ResetBusVolumes())
        {
            try
            {
                Backend.Submit(_commands.AsSpan(0, _commandLength), _events);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                (failures ??= []).Add(exception);
            }
        }

        _commandLength = 0;
        if (failures is not null)
        {
            throw new AggregateException("Destroying sounds failed.", failures);
        }
    }

    private bool ResetBusVolumes()
    {
        var changed = false;
        for (var bus = 0; bus < BusCount; bus++)
        {
            if (_busVolumes[bus] != 1)
            {
                _busVolumes[bus] = 1;
                WriteBusVolume((AudioBus)bus, 1);
                changed = true;
            }
        }

        return changed;
    }

    private void ApplyEvent(in AudioEvent e)
    {
        var id = e.SoundId;
        if ((uint)id >= (uint)_sounds.Count || !_sounds[id].Alive)
        {
            // A sound destroyed during this frame: its result no longer matters.
            return;
        }

        var state = e.Kind switch
        {
            AudioEventKind.SoundReady => SoundState.Ready,
            AudioEventKind.SoundFailed => SoundState.Failed,
            _ => throw new InvalidOperationException($"Unknown audio event {e.Kind} for sound {id}."),
        };
        _sounds[id] = _sounds[id] with { State = state };
    }

    private Voice StartLoop(Sound sound, AudioBus bus, in SoundOptions options, float fadeInSeconds)
    {
        if (!_freeVoiceIds.TryPop(out var id))
        {
            id = _voices.Count;
            _voices.Add(default);
        }

        var generation = _voices[id].Generation + 1;
        _voices[id] = new VoiceSlot(generation, Alive: true, sound.Id);

        var command = Reserve(PlayLoopSize);
        WriteOpcode(command, AudioCommandKind.PlayLoop);
        WriteInt(command, 1, id);
        WriteInt(command, 2, sound.Id);
        WriteInt(command, 3, (int)bus);
        WriteOptions(command, 4, options);
        WriteFloat(command, 7, fadeInSeconds);
        return new Voice(id, generation);
    }

    private void StopMusicVoice(float fadeSeconds)
    {
        if (_hasMusic)
        {
            StopVoice(_musicVoice.Id, fadeSeconds);
            _hasMusic = false;
        }
    }

    /// <summary>
    /// Writes the stop and frees the id at once: the backend lets go of a voice id when it reads <c>StopVoice</c>.
    /// </summary>
    private void StopVoice(int id, float fadeOutSeconds)
    {
        var command = Reserve(StopVoiceSize);
        WriteOpcode(command, AudioCommandKind.StopVoice);
        WriteInt(command, 1, id);
        WriteFloat(command, 2, fadeOutSeconds);
        _voices[id] = _voices[id] with { Alive = false };
        _freeVoiceIds.Push(id);
    }

    private void WriteBusVolume(AudioBus bus, float volume)
    {
        var command = Reserve(SetBusVolumeSize);
        WriteOpcode(command, AudioCommandKind.SetBusVolume);
        WriteInt(command, 1, (int)bus);
        WriteFloat(command, 2, volume);
    }

    /// <exception cref="InvalidOperationException">The handle is stale.</exception>
    private void Validate(Voice voice)
    {
        if (!IsPlaying(voice))
        {
            throw new InvalidOperationException("The voice was stopped, its sound was destroyed, or it belongs to an earlier run.");
        }
    }

    private void EnsureNotRendering()
    {
        if (_rendering)
        {
            throw new InvalidOperationException(
                "Audio commands cannot be recorded in PrepareRender or Render; play sounds from FixedUpdate or Update.");
        }
    }

    private Span<byte> Reserve(int size)
    {
        if (_commandLength + size > CommandCapacity)
        {
            throw new InvalidOperationException("The frame has too many audio commands: is something played in a loop?");
        }

        var command = _commands.AsSpan(_commandLength, size);
        _commandLength += size;
        return command;
    }

    private static void ValidateBus(AudioBus bus)
    {
        if ((uint)bus >= BusCount)
        {
            throw new ArgumentOutOfRangeException(nameof(bus), bus, "Unknown audio bus.");
        }
    }

    private static void ValidatePlayBus(AudioBus bus)
    {
        ValidateBus(bus);
        if (bus == AudioBus.Master)
        {
            throw new ArgumentOutOfRangeException(nameof(bus), bus, "Voices play on Sfx, Ambience or Music, not on Master.");
        }
    }

    private static void ValidateOptions(in SoundOptions options)
    {
        ValidateVolume(options.Volume, nameof(options));
        if (options.Pitch is not (> 0 and <= MaxPitch))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Pitch, "Pitch must be in (0, Audio.MaxPitch].");
        }

        if (options.Pan is not (>= -1 and <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Pan, "Pan must be in [-1, 1].");
        }
    }

    private static void ValidateVolume(float volume, string paramName)
    {
        if (!(volume >= 0 && float.IsFinite(volume)))
        {
            throw new ArgumentOutOfRangeException(paramName, volume, "Volume must be finite and at least 0.");
        }
    }

    private static void ValidateFade(float seconds, string paramName)
    {
        if (!(seconds >= 0 && float.IsFinite(seconds)))
        {
            throw new ArgumentOutOfRangeException(paramName, seconds, "A fade must be finite and at least 0 seconds.");
        }
    }

    private static void WriteOptions(Span<byte> command, int index, in SoundOptions options)
    {
        WriteFloat(command, index, options.Volume);
        WriteFloat(command, index + 1, options.Pitch);
        WriteFloat(command, index + 2, options.Pan);
    }

    private static void WriteOpcode(Span<byte> command, AudioCommandKind kind) => WriteInt(command, 0, (int)kind);

    private static void WriteInt(Span<byte> command, int index, int value)
        => BinaryPrimitives.WriteInt32LittleEndian(command[(index * sizeof(int))..], value);

    private static void WriteFloat(Span<byte> command, int index, float value)
        => BinaryPrimitives.WriteSingleLittleEndian(command[(index * sizeof(float))..], value);

    /// <param name="Generation">Bumped by every <see cref="CreateSound"/> of this id; 0 for an id never used.</param>
    /// <param name="Alive">The id holds a live sound.</param>
    /// <param name="OnBackend">The backend holds a sound with this id: a live one, or one destroyed during this frame.</param>
    /// <param name="State">Decode state reported by the backend.</param>
    private readonly record struct SoundSlot(int Generation, bool Alive, bool OnBackend, SoundState State);

    /// <param name="Generation">Bumped by every loop started with this id; 0 for an id never used.</param>
    /// <param name="Alive">The id holds a loop that was not stopped.</param>
    /// <param name="SoundId">The sound the loop plays.</param>
    private readonly record struct VoiceSlot(int Generation, bool Alive, int SoundId);
}
