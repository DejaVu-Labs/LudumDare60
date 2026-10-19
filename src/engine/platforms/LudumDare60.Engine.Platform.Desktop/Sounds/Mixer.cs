using System.Buffers.Binary;

namespace LudumDare60.Engine.Platform.Desktop.Sounds;

/// <summary>
/// Mixes voices into stereo float samples: the desktop twin of the browser's <c>webaudio.js</c> graph. It runs the
/// audio command stream (see <see cref="AudioCommandKind"/>) with the same checks and the same behavior: a voice is
/// sound -> gain -> stereo panner -> bus gain -> master gain, changes glide over about 10 ms, and fades are linear.
/// </summary>
/// <remarks>Not thread-safe: <see cref="SdlAudio"/> calls it under the audio stream's lock. <see cref="Mix"/> does not
/// allocate.</remarks>
internal sealed class Mixer
{
    /// <summary>
    /// Time constant of a glide in seconds, as <c>SMOOTHING</c> in <c>webaudio.js</c>: a change settles in about 10 ms.
    /// </summary>
    public const float SmoothingSeconds = 0.01f / 3;

    /// <summary>
    /// Shortest fade-out of a stopped loop, in seconds, so a stop never clicks.
    /// </summary>
    public const float MinFadeSeconds = 0.01f;

    private const int BusCount = 4;

    private readonly float _smoothing;
    private readonly Dictionary<int, SoundData?> _sounds = [];
    private readonly Dictionary<int, Voice> _loops = [];
    private readonly List<Voice> _playing = [];
    private readonly Stack<Voice> _pool = new();
    private readonly SmoothParam[] _buses = new SmoothParam[BusCount];
    private float[] _busMix = [];

    /// <param name="sampleRate">Output frames per second.</param>
    public Mixer(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 1);
        SampleRate = sampleRate;
        _smoothing = 1 - MathF.Exp(-1f / (SmoothingSeconds * sampleRate));
        for (var bus = 0; bus < BusCount; bus++)
        {
            _buses[bus].Set(1);
        }
    }

    public int SampleRate { get; }

    /// <summary>
    /// Voices on <see cref="AudioBus.Sfx"/> and <see cref="AudioBus.Ambience"/> that count against
    /// <see cref="AudioLimits.MaxVoices"/>: one-shots and loops that have not been stopped.
    /// </summary>
    public int ActiveVoices { get; private set; }

    /// <summary>
    /// Voices still audible, stopped loops that fade out included.
    /// </summary>
    public int PlayingVoices => _playing.Count;

    public int SoundCount => _sounds.Count;

    /// <summary>
    /// Adds sound <paramref name="id"/>, still decoding: loops of it wait, one-shots of it are dropped.
    /// </summary>
    /// <exception cref="InvalidOperationException">The sound exists.</exception>
    public void AddSound(int id)
    {
        if (!_sounds.TryAdd(id, null))
        {
            throw new InvalidOperationException($"Sound {id} already exists.");
        }
    }

    /// <summary>
    /// Makes sound <paramref name="id"/> ready and starts the loops that wait for it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The sound does not exist.</exception>
    public void SetSoundData(int id, SoundData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        GetSound(id);
        _sounds[id] = data;
        foreach (var voice in _loops.Values)
        {
            if (voice.SoundId == id && voice.Data is null)
            {
                StartLoop(voice, data);
            }
        }
    }

    public bool HasSound(int id) => _sounds.ContainsKey(id);

    /// <summary>
    /// Removes sound <paramref name="id"/>: its loops fade out over <see cref="MinFadeSeconds"/>, its one-shots stop.
    /// </summary>
    /// <exception cref="InvalidOperationException">The sound does not exist.</exception>
    public void RemoveSound(int id)
    {
        GetSound(id);
        foreach (var (voiceId, voice) in _loops)
        {
            if (voice.SoundId == id)
            {
                _loops.Remove(voiceId);
                StopLoop(voice, 0);
            }
        }

        for (var i = _playing.Count - 1; i >= 0; i--)
        {
            var voice = _playing[i];
            if (voice.SoundId == id && voice.LoopId < 0)
            {
                Finish(i);
            }
        }

        _sounds.Remove(id);
    }

    /// <summary>
    /// Runs one frame of audio commands.
    /// </summary>
    /// <exception cref="InvalidOperationException">A command is malformed or refers to a sound, voice or bus that does
    /// not exist. Commands before it have run.</exception>
    public void Run(ReadOnlySpan<byte> commands)
    {
        var offset = 0;
        while (offset < commands.Length)
        {
            var opcode = (AudioCommandKind)ReadInt32(commands, offset, offset);
            switch (opcode)
            {
                case AudioCommandKind.Play:
                    Play(
                        ReadInt32(commands, offset + 4, offset),
                        ReadInt32(commands, offset + 8, offset),
                        ReadSingle(commands, offset + 12, offset),
                        ReadSingle(commands, offset + 16, offset),
                        ReadSingle(commands, offset + 20, offset));
                    offset += 24;
                    break;
                case AudioCommandKind.PlayLoop:
                    PlayLoop(
                        ReadInt32(commands, offset + 4, offset),
                        ReadInt32(commands, offset + 8, offset),
                        ReadInt32(commands, offset + 12, offset),
                        ReadSingle(commands, offset + 16, offset),
                        ReadSingle(commands, offset + 20, offset),
                        ReadSingle(commands, offset + 24, offset),
                        ReadSingle(commands, offset + 28, offset));
                    offset += 32;
                    break;
                case AudioCommandKind.SetVoice:
                    SetVoice(
                        ReadInt32(commands, offset + 4, offset),
                        ReadSingle(commands, offset + 8, offset),
                        ReadSingle(commands, offset + 12, offset),
                        ReadSingle(commands, offset + 16, offset));
                    offset += 20;
                    break;
                case AudioCommandKind.StopVoice:
                    var id = ReadInt32(commands, offset + 4, offset);
                    var fade = ReadSingle(commands, offset + 8, offset);
                    var voice = GetVoice(id);
                    _loops.Remove(id);
                    StopLoop(voice, fade);
                    offset += 12;
                    break;
                case AudioCommandKind.SetBusVolume:
                    var bus = ReadInt32(commands, offset + 4, offset);
                    var volume = ReadSingle(commands, offset + 8, offset);
                    if ((uint)bus >= BusCount)
                    {
                        throw new InvalidOperationException($"Unknown audio bus {bus}.");
                    }

                    _buses[bus].Glide(Math.Max(0, volume));
                    offset += 12;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown audio command {(int)opcode} at byte {offset}.");
            }
        }
    }

    /// <summary>
    /// Writes the next <c>output.Length / 2</c> frames, interleaved left and right, over <paramref name="output"/>.
    /// </summary>
    public void Mix(Span<float> output)
    {
        output.Clear();
        var frames = output.Length / 2;
        var busLength = (BusCount - 1) * output.Length;
        if (_busMix.Length < busLength)
        {
            // Grows once to the device's buffer size, then never again.
            _busMix = new float[busLength];
        }

        var busMix = _busMix.AsSpan(0, busLength);
        busMix.Clear();
        for (var i = _playing.Count - 1; i >= 0; i--)
        {
            var voice = _playing[i];
            var start = (voice.Bus - 1) * output.Length;
            if (MixVoice(voice, busMix.Slice(start, output.Length), frames))
            {
                Finish(i);
            }
        }

        for (var frame = 0; frame < frames; frame++)
        {
            var master = _buses[(int)AudioBus.Master].Next(_smoothing);
            var left = 0f;
            var right = 0f;
            for (var bus = 1; bus < BusCount; bus++)
            {
                var gain = _buses[bus].Next(_smoothing);
                var mix = busMix.Slice((bus - 1) * output.Length, output.Length);
                left += mix[2 * frame] * gain;
                right += mix[(2 * frame) + 1] * gain;
            }

            output[2 * frame] = Math.Clamp(left * master, -1f, 1f);
            output[(2 * frame) + 1] = Math.Clamp(right * master, -1f, 1f);
        }
    }

    /// <returns>Whether the voice is done and can be removed.</returns>
    private bool MixVoice(Voice voice, Span<float> output, int frames)
    {
        var data = voice.Data;
        if (data is null)
        {
            return false;
        }

        var samples = data.Samples;
        var length = data.FrameCount;
        var stereo = data.Channels == 2;
        var rateScale = (double)data.SampleRate / SampleRate;
        for (var frame = 0; frame < frames; frame++)
        {
            if (!voice.IsLoop && voice.Position >= length)
            {
                return true;
            }

            var gain = voice.Gain.Next(_smoothing);
            var pan = Math.Clamp(voice.Pan.Next(_smoothing), -1f, 1f);
            var pitch = voice.Pitch.Next(_smoothing);

            var index = (int)voice.Position;
            var next = index + 1 < length ? index + 1 : (voice.IsLoop ? 0 : index);
            var t = (float)(voice.Position - index);
            float left;
            float right;
            if (stereo)
            {
                left = Lerp(samples[2 * index], samples[2 * next], t) * gain;
                right = Lerp(samples[(2 * index) + 1], samples[(2 * next) + 1], t) * gain;
                Pan(ref left, ref right, pan);
            }
            else
            {
                // StereoPannerNode's equal-power law for mono input.
                var mono = Lerp(samples[index], samples[next], t) * gain;
                var angle = (pan + 1) * 0.25f * MathF.PI;
                left = mono * MathF.Cos(angle);
                right = mono * MathF.Sin(angle);
            }

            output[2 * frame] += left;
            output[(2 * frame) + 1] += right;

            voice.Position += Math.Max(pitch, 0) * rateScale;
            if (voice.IsLoop && voice.Position >= length)
            {
                voice.Position %= length;
            }

            if (voice.Stopping && voice.Gain.RampLeft == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// StereoPannerNode's equal-power law for stereo input: panning left moves the right channel into the left one.
    /// </summary>
    private static void Pan(ref float left, ref float right, float pan)
    {
        if (pan <= 0)
        {
            var angle = (pan + 1) * 0.5f * MathF.PI;
            left += right * MathF.Cos(angle);
            right *= MathF.Sin(angle);
        }
        else
        {
            var angle = pan * 0.5f * MathF.PI;
            right += left * MathF.Sin(angle);
            left *= MathF.Cos(angle);
        }
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    private void Play(int soundId, int bus, float volume, float pitch, float pan)
    {
        CheckBus(bus);
        var data = GetSound(soundId);
        var counted = bus != (int)AudioBus.Music;
        if (data is null || (counted && ActiveVoices >= AudioLimits.MaxVoices))
        {
            return;
        }

        var voice = Rent(soundId, bus, loopId: -1, counted);
        voice.Data = data;
        voice.Gain.Set(Math.Max(0, volume));
        voice.Pitch.Set(pitch);
        voice.Pan.Set(pan);
        _playing.Add(voice);
    }

    private void PlayLoop(int id, int soundId, int bus, float volume, float pitch, float pan, float fadeIn)
    {
        if (_loops.ContainsKey(id))
        {
            throw new InvalidOperationException($"Voice {id} already plays.");
        }

        CheckBus(bus);
        var data = GetSound(soundId);
        var voice = Rent(soundId, bus, id, bus != (int)AudioBus.Music);
        voice.Volume = Math.Max(0, volume);
        voice.FadeIn = fadeIn;
        voice.Gain.Set(0);
        voice.Pitch.Set(pitch);
        voice.Pan.Set(pan);
        _loops.Add(id, voice);
        _playing.Add(voice);
        if (data is not null)
        {
            StartLoop(voice, data);
        }
    }

    private void SetVoice(int id, float volume, float pitch, float pan)
    {
        var voice = GetVoice(id);
        voice.Volume = Math.Max(0, volume);
        voice.Pan.Glide(pan);
        if (voice.Data is null)
        {
            // Not started: the loop starts with these values.
            voice.Pitch.Set(pitch);
            return;
        }

        voice.Gain.Glide(voice.Volume);
        voice.Pitch.Glide(pitch);
    }

    private void StartLoop(Voice voice, SoundData data)
    {
        voice.Data = data;
        voice.Position = 0;
        if (voice.FadeIn > 0)
        {
            voice.Gain.Set(0);
            voice.Gain.Ramp(voice.Volume, Samples(voice.FadeIn));
        }
        else
        {
            voice.Gain.Set(voice.Volume);
        }
    }

    private void StopLoop(Voice voice, float fade)
    {
        if (voice.Counted)
        {
            voice.Counted = false;
            ActiveVoices--;
        }

        if (voice.Data is null)
        {
            Finish(_playing.IndexOf(voice));
            return;
        }

        voice.Stopping = true;
        voice.Gain.Ramp(0, Samples(Math.Max(fade, MinFadeSeconds)));
    }

    private Voice Rent(int soundId, int bus, int loopId, bool counted)
    {
        var voice = _pool.Count > 0 ? _pool.Pop() : new Voice();
        voice.SoundId = soundId;
        voice.Bus = bus;
        voice.LoopId = loopId;
        voice.Counted = counted;
        voice.Data = null;
        voice.Position = 0;
        voice.Stopping = false;
        voice.Volume = 0;
        voice.FadeIn = 0;
        if (counted)
        {
            ActiveVoices++;
        }

        return voice;
    }

    private void Finish(int index)
    {
        var voice = _playing[index];
        _playing.RemoveAt(index);
        if (voice.Counted)
        {
            ActiveVoices--;
        }

        if (voice.LoopId >= 0 && _loops.TryGetValue(voice.LoopId, out var loop) && loop == voice)
        {
            _loops.Remove(voice.LoopId);
        }

        voice.Data = null;
        _pool.Push(voice);
    }

    private int Samples(float seconds) => (int)Math.Min(seconds * SampleRate, int.MaxValue);

    private static void CheckBus(int bus)
    {
        if (bus is <= (int)AudioBus.Master or > (int)AudioBus.Music)
        {
            throw new InvalidOperationException($"Voices cannot play on bus {bus}.");
        }
    }

    private SoundData? GetSound(int id)
        => _sounds.TryGetValue(id, out var data) ? data : throw new InvalidOperationException($"Sound {id} does not exist.");

    private Voice GetVoice(int id)
        => _loops.TryGetValue(id, out var voice) ? voice : throw new InvalidOperationException($"Voice {id} does not exist.");

    private static int ReadInt32(ReadOnlySpan<byte> commands, int position, int commandOffset)
        => position + 4 <= commands.Length
            ? BinaryPrimitives.ReadInt32LittleEndian(commands[position..])
            : throw new InvalidOperationException($"The audio command at byte {commandOffset} is cut off.");

    private static float ReadSingle(ReadOnlySpan<byte> commands, int position, int commandOffset)
        => BitConverter.Int32BitsToSingle(ReadInt32(commands, position, commandOffset));

    private sealed class Voice
    {
        public SmoothParam Gain;
        public SmoothParam Pitch;
        public SmoothParam Pan;

        public int SoundId { get; set; }

        /// <summary>
        /// The <see cref="AudioBus"/>: Sfx, Ambience or Music.
        /// </summary>
        public int Bus { get; set; }

        /// <summary>
        /// The engine's voice id of a loop; -1 for a one-shot.
        /// </summary>
        public int LoopId { get; set; }

        public bool IsLoop => LoopId >= 0;

        public bool Counted { get; set; }

        /// <summary>
        /// The samples; null while a loop waits for its sound to decode.
        /// </summary>
        public SoundData? Data { get; set; }

        /// <summary>
        /// Read position in source frames.
        /// </summary>
        public double Position { get; set; }

        public bool Stopping { get; set; }

        /// <summary>
        /// The loop's volume, which its gain fades in to and glides to.
        /// </summary>
        public float Volume { get; set; }

        public float FadeIn { get; set; }
    }
}
