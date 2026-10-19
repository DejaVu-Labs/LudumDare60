using System.Runtime.InteropServices;
using SDL3;

namespace LudumDare60.Engine.Platform.Desktop.Sounds;

/// <summary>
/// <see cref="IAudioBackend"/> on an SDL audio stream: the desktop twin of the browser's <c>webaudio.js</c>. Sounds are
/// decoded on the thread pool (<see cref="SoundDecoder"/>), and a <see cref="Mixer"/> mixes them on SDL's audio
/// thread. The game thread changes the mixer only under the stream's lock, which SDL also holds while it asks for
/// samples.
/// </summary>
/// <remarks>Without an audio device the backend stays silent but works: sounds decode and report, and commands are
/// checked and run.</remarks>
internal sealed unsafe class SdlAudio : IAudioBackend
{
    /// <summary>
    /// Mixer output rate. SDL converts it to whatever the device plays.
    /// </summary>
    public const int SampleRate = 48000;

    private readonly List<PendingDecode> _decoding = [];
    private readonly List<AudioEvent> _events = [];
    private readonly SDL.AudioStreamCallback _callback;
    private float[] _mixBuffer = new float[SampleRate / 10 * 2];
    private nint _stream;

    public SdlAudio() => _callback = OnAudioRequested;

    /// <summary>
    /// Whether a device plays the mix.
    /// </summary>
    public bool HasDevice => _stream != 0;

    /// <summary>
    /// The mixer. Only touch it under <see cref="Lock"/> while a device runs.
    /// </summary>
    internal Mixer Mixer { get; } = new(SampleRate);

    /// <summary>
    /// Opens the default playback device. Failing that (no sound server, no device), the backend stays silent.
    /// </summary>
    /// <returns>Whether a device was opened; SDL's error otherwise.</returns>
    public string? Initialize()
    {
        if (_stream != 0)
        {
            return null;
        }

        var spec = new SDL.AudioSpec { Format = SDL.AudioFormat.AudioF32LE, Channels = 2, Freq = SampleRate };
        _stream = SDL.OpenAudioDeviceStream(SDL.AudioDeviceDefaultPlayback, in spec, _callback, 0);
        if (_stream == 0)
        {
            return SDL.GetError();
        }

        SDL.ResumeAudioStreamDevice(_stream);
        return null;
    }

    /// <summary>
    /// Closes the device. The mixer keeps its sounds, so the engine can still destroy them.
    /// </summary>
    public void Shutdown()
    {
        if (_stream != 0)
        {
            SDL.DestroyAudioStream(_stream);
            _stream = 0;
        }
    }

    public void CreateSound(int id, ReadOnlySpan<byte> data)
    {
        Lock();
        try
        {
            Mixer.AddSound(id);
        }
        finally
        {
            Unlock();
        }

        var file = data.ToArray();
        _decoding.Add(new PendingDecode(id, Task.Run(() => SoundDecoder.Decode(file))));
    }

    public void DestroySound(int id)
    {
        Lock();
        try
        {
            Mixer.RemoveSound(id);
        }
        finally
        {
            Unlock();
        }

        // A decode still running finishes unseen; destroyed sounds report nothing.
        _decoding.RemoveAll(decode => decode.SoundId == id);
        for (var i = _events.Count - 1; i >= 0; i--)
        {
            if (_events[i].SoundId == id)
            {
                _events.RemoveAt(i);
            }
        }
    }

    public int Submit(ReadOnlySpan<byte> commands, Span<AudioEvent> events)
    {
        Lock();
        try
        {
            ApplyFinishedDecodes();
            Mixer.Run(commands);
        }
        finally
        {
            Unlock();
        }

        var count = Math.Min(events.Length, _events.Count);
        for (var i = 0; i < count; i++)
        {
            events[i] = _events[i];
        }

        _events.RemoveRange(0, count);
        return count;
    }

    /// <summary>
    /// Waits until every decode has finished, so the next <see cref="Submit"/> reports all of them. For tests.
    /// </summary>
    internal void WaitForDecodes()
    {
        foreach (var decode in _decoding)
        {
            ((IAsyncResult)decode.Task).AsyncWaitHandle.WaitOne();
        }
    }

    private void ApplyFinishedDecodes()
    {
        for (var i = 0; i < _decoding.Count; i++)
        {
            var decode = _decoding[i];
            if (!decode.Task.IsCompleted)
            {
                continue;
            }

            _decoding.RemoveAt(i--);
            if (decode.Task.IsCompletedSuccessfully)
            {
                Mixer.SetSoundData(decode.SoundId, decode.Task.Result);
                _events.Add(new AudioEvent(AudioEventKind.SoundReady, decode.SoundId));
            }
            else
            {
                var error = decode.Task.Exception?.InnerException?.Message ?? "the decode was cancelled";
                Console.Error.WriteLine($"Sound {decode.SoundId} could not be decoded: {error}");
                _events.Add(new AudioEvent(AudioEventKind.SoundFailed, decode.SoundId));
            }
        }
    }

    private void OnAudioRequested(nint userdata, nint stream, int additionalAmount, int totalAmount)
    {
        var floats = additionalAmount / sizeof(float) / 2 * 2;
        if (floats <= 0)
        {
            return;
        }

        if (_mixBuffer.Length < floats)
        {
            // Grows once to the device's request size.
            _mixBuffer = new float[floats];
        }

        var mix = _mixBuffer.AsSpan(0, floats);
        Mixer.Mix(mix);
        var bytes = MemoryMarshal.AsBytes(mix);
        SDL.PutAudioStreamData(stream, bytes, bytes.Length);
    }

    private void Lock()
    {
        if (_stream != 0)
        {
            SDL.LockAudioStream(_stream);
        }
    }

    private void Unlock()
    {
        if (_stream != 0)
        {
            SDL.UnlockAudioStream(_stream);
        }
    }

    private readonly record struct PendingDecode(int SoundId, Task<SoundData> Task);
}
