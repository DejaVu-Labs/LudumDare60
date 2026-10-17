using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;

namespace LudumDare60.Samples.SoundBoard;

/// <summary>
/// Plays every kind of sound the engine supports, so a browser and its speakers can be checked by ear: keys 1-7 play
/// effects with a random pitch, panned by the pointer; L toggles a rain loop that follows the pointer; Q and W crossfade
/// between two music tracks, S stops the music, M mutes everything. The screen shows a pad per effect that lights up
/// when it plays (dark while decoding, red if decoding failed), a bar for the rain loop and a square per track.
/// </summary>
/// <param name="seed">Seed of the pitch variation, so tests can predict it.</param>
public sealed class SoundBoardGame(int seed = 60) : IGame
{
    public const int VirtualWidth = 640;
    public const int VirtualHeight = 360;

    /// <summary>
    /// Pitch of an effect varies by this much either way, so repeated plays do not sound mechanical.
    /// </summary>
    public const float PitchVariation = 0.1f;

    /// <summary>
    /// Fade-in and fade-out of the rain loop, in seconds.
    /// </summary>
    public const float RainFade = 1;

    /// <summary>
    /// Crossfade between music tracks, in seconds.
    /// </summary>
    public const float MusicFade = 2;

    private const int EffectCount = 7;
    private const int RainSlot = EffectCount;
    private const int TrackASlot = EffectCount + 1;
    private const int TrackBSlot = EffectCount + 2;
    private const int SlotCount = EffectCount + 3;

    private static readonly Key[] s_effectKeys = [Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7];
    private static readonly Color s_background = Color.FromRgb(0x14171c);
    private static readonly Color s_idle = Color.FromRgb(0x3a4250);
    private static readonly Color s_lit = Color.FromRgb(0xffd060);
    private static readonly Color s_decoding = Color.FromRgb(0x22262e);
    private static readonly Color s_failed = Color.FromRgb(0xb03030);

    private readonly Random _random = new(seed);
    private readonly SoundSlot[] _slots = new SoundSlot[SlotCount];
    private readonly float[] _flash = new float[EffectCount];
    private AssetManager? _assets;
    private Graphics? _graphics;
    private Audio? _audio;
    private Texture _pixel;
    private bool _hasPixel;
    private Voice _rain;
    private float _pan;
    private float _rainVolume = 1;
    private Vector2 _pointer;

    /// <summary>
    /// Whether M muted the master bus.
    /// </summary>
    public bool IsMuted { get; private set; }

    /// <summary>
    /// Whether the rain loop plays (or fades in).
    /// </summary>
    public bool IsRainPlaying => _audio is not null && _audio.IsPlaying(_rain);

    /// <summary>
    /// Number of sound files handed to the audio device so far.
    /// </summary>
    public int CreatedCount { get; private set; }

    public void Initialize(AssetManager assets, Graphics graphics, Audio audio)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
        _graphics = graphics;
        _audio = audio;
        for (var i = 0; i < EffectCount; i++)
        {
            _slots[i].File = assets.Request<byte[]>(SoundPaths.Effects[i]);
        }

        _slots[RainSlot].File = assets.Request<byte[]>(SoundPaths.Rain);
        _slots[TrackASlot].File = assets.Request<byte[]>(SoundPaths.TrackA);
        _slots[TrackBSlot].File = assets.Request<byte[]>(SoundPaths.TrackB);
    }

    public void FixedUpdate(in FrameContext context)
    {
    }

    public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
    {
        var audio = _audio!;
        if (!_hasPixel)
        {
            _pixel = _graphics!.CreateTexture(new Image(1, 1, [255, 255, 255, 255]));
            _hasPixel = true;
        }

        CreateLoadedSounds(audio);

        var input = context.Input;
        _pointer = input.PointerPosition;
        _pan = Math.Clamp((_pointer.X / VirtualWidth * 2) - 1, -1, 1);
        _rainVolume = Math.Clamp(1 - (0.8f * _pointer.Y / VirtualHeight), 0.2f, 1);

        for (var i = 0; i < EffectCount; i++)
        {
            _flash[i] = Math.Max(0, _flash[i] - ((float)context.DeltaTime * 3));
            if (input.IsPressed(s_effectKeys[i]) && _slots[i].Created)
            {
                var pitch = 1 + (PitchVariation * ((_random.NextSingle() * 2) - 1));
                audio.Play(_slots[i].Sound, AudioBus.Sfx, new SoundOptions { Pitch = pitch, Pan = _pan });
                _flash[i] = 1;
            }
        }

        UpdateRain(audio, input.IsPressed(Key.L));

        if (input.IsPressed(Key.Q) && _slots[TrackASlot].Created)
        {
            audio.PlayMusic(_slots[TrackASlot].Sound, MusicFade);
        }

        if (input.IsPressed(Key.W) && _slots[TrackBSlot].Created)
        {
            audio.PlayMusic(_slots[TrackBSlot].Sound, MusicFade);
        }

        if (input.IsPressed(Key.S))
        {
            audio.StopMusic(MusicFade);
        }

        if (input.IsPressed(Key.M))
        {
            IsMuted = !IsMuted;
            audio.SetBusVolume(AudioBus.Master, IsMuted ? 0 : 1);
        }
    }

    public void PrepareRender(double alpha)
    {
    }

    public void Render(SpriteBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        batch.Clear(s_background);
        if (!_hasPixel)
        {
            return;
        }

        for (var i = 0; i < EffectCount; i++)
        {
            Rect(batch, 40 + (i * 80), 60, 56, 56, SlotColor(i, _flash[i]));
        }

        // The rain bar, with a marker where the loop is panned.
        var rainColor = IsRainPlaying ? Mix(s_idle, s_lit, _rainVolume) : SlotColor(RainSlot, 0);
        Rect(batch, 40, 170, 536, 24, rainColor);
        if (IsRainPlaying)
        {
            Rect(batch, 40 + ((_pan + 1) / 2 * 532), 166, 4, 32, Color.White);
        }

        var music = _audio!.Music;
        Rect(batch, 40, 230, 56, 56, SlotColor(TrackASlot, IsCurrent(music, TrackASlot) ? 1 : 0));
        Rect(batch, 120, 230, 56, 56, SlotColor(TrackBSlot, IsCurrent(music, TrackBSlot) ? 1 : 0));

        if (IsMuted)
        {
            Rect(batch, 576, 16, 24, 24, s_failed);
        }

        Rect(batch, _pointer.X - 1, _pointer.Y - 1, 3, 3, Color.White);
    }

    private void CreateLoadedSounds(Audio audio)
    {
        var assets = _assets!;
        for (var i = 0; i < SlotCount; i++)
        {
            ref var slot = ref _slots[i];
            if (slot.Created || !assets.TryGet(slot.File, out var bytes))
            {
                continue;
            }

            // The platform copies the bytes, so the file can be released at once.
            slot.Sound = audio.CreateSound(bytes);
            slot.Created = true;
            assets.Release(slot.File);
            CreatedCount++;
        }
    }

    private void UpdateRain(Audio audio, bool toggle)
    {
        var options = new SoundOptions { Volume = _rainVolume, Pan = _pan };
        if (audio.IsPlaying(_rain))
        {
            if (toggle)
            {
                audio.Stop(_rain, RainFade);
            }
            else
            {
                audio.SetVoice(_rain, options);
            }
        }
        else if (toggle && _slots[RainSlot].Created)
        {
            _rain = audio.PlayLoop(_slots[RainSlot].Sound, AudioBus.Ambience, options, RainFade);
        }
    }

    private bool IsCurrent(Sound? music, int slot) => _slots[slot].Created && music == _slots[slot].Sound;

    private Color SlotColor(int slot, float light)
    {
        ref readonly var s = ref _slots[slot];
        if (!s.Created)
        {
            return s_decoding;
        }

        return _audio!.GetState(s.Sound) switch
        {
            SoundState.Ready => Mix(s_idle, s_lit, light),
            SoundState.Failed => s_failed,
            _ => s_decoding,
        };
    }

    private void Rect(SpriteBatch batch, float x, float y, float width, float height, Color color)
        => batch.Draw(_pixel, new Vector2(x, y), new SpriteOptions { Scale = new Vector2(width, height), Tint = color });

    private static Color Mix(Color from, Color to, float amount)
        => new(Lerp(from.R, to.R, amount), Lerp(from.G, to.G, amount), Lerp(from.B, to.B, amount));

    private static byte Lerp(byte from, byte to, float amount) => (byte)(from + ((to - from) * amount));

    private struct SoundSlot
    {
        public AssetHandle<byte[]> File;
        public Sound Sound;
        public bool Created;
    }
}
