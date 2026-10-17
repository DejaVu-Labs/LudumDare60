using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game;

/// <summary>
/// The Ludum Dare 60 game. For now the bench background (the cutting mat color until it loads) with one touch-loop
/// prototype on it: a base pin the player pulls out of a block of frame steel and pushes back in, with breakaway,
/// friction, a glow and a click at either end. The parts and the click are placeholders until the real art and
/// recordings arrive.
/// </summary>
public sealed class LudumDare60Game : IGame
{
    /// <summary>
    /// Width of the virtual screen in virtual pixels (see docs/gamedesign/art-direction.md).
    /// </summary>
    public const int VirtualWidth = 640;

    /// <inheritdoc cref="VirtualWidth"/>
    public const int VirtualHeight = 360;

    /// <summary>
    /// The cutting mat, the screen color until the background is loaded.
    /// </summary>
    public static readonly Color MatColor = Color.FromRgb(0x0b4f45);

    /// <summary>
    /// Top-left corner of the frame block on the mat.
    /// </summary>
    public static readonly Vector2 FramePosition = new(250, 226);

    /// <summary>
    /// The base pin's head when it is seated: just outside the bore in the frame's left edge.
    /// </summary>
    public static readonly Vector2 PinSeated = FramePosition + new Vector2(-2, PlaceholderParts.BoreRow);

    /// <summary>
    /// The base pin's head when it is pulled out: the whole rod is out of the frame.
    /// </summary>
    public static readonly Vector2 PinOut = PinSeated - new Vector2(PlaceholderParts.Pin.Width - 4, 0);

    private const float PinGrabRadius = 7;

    // Glow alpha of a part that can move, pulsing, and of the part under the pointer or in the hand.
    private const float GlowIdleBase = 70;
    private const float GlowIdlePulse = 50;
    private const float GlowPulseRate = 4;
    private const byte GlowActive = 255;

    private static readonly SoundOptions s_seatClick = new() { Pitch = 0.75f };
    private static readonly SoundOptions s_outClick = new() { Pitch = 1.25f };
    private static readonly SoundOptions s_breakClick = new() { Pitch = 0.55f, Volume = 0.4f };

    private AssetManager? _assets;
    private Graphics? _graphics;
    private Audio? _audio;
    private AssetHandle<Image> _benchImage;
    private Texture _bench;
    private Texture _parts;
    private bool _hasParts;
    private Sound _click;
    private bool _hasClick;
    private float _time;
    private Vector2 _pinDrawPosition;
    private byte _pinGlow;

    /// <summary>
    /// Whether the bench background is loaded and drawn.
    /// </summary>
    public bool HasBackground { get; private set; }

    /// <summary>
    /// The base pin, seated at <see cref="PinSeated"/> and free at <see cref="PinOut"/>.
    /// </summary>
    public PartDrag BasePin { get; } = new(new LinePath(PinSeated, PinOut), PinGrabRadius, DragFeel.Default);

    public void Initialize(AssetManager assets, Graphics graphics, Audio audio)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
        _graphics = graphics;
        _audio = audio;
        assets.Register(new ImageLoader());
        _benchImage = assets.Request<Image>(AssetPaths.Bench);
    }

    public void FixedUpdate(in FrameContext context)
    {
        var events = BasePin.Step(PointerSample.From(context.Input), (float)context.DeltaTime);
        if (!_hasClick)
        {
            return;
        }

        if ((events & DragEvents.SnappedStart) != DragEvents.None)
        {
            _audio!.Play(_click, AudioBus.Sfx, s_seatClick);
        }
        else if ((events & DragEvents.SnappedEnd) != DragEvents.None)
        {
            _audio!.Play(_click, AudioBus.Sfx, s_outClick);
        }
        else if ((events & DragEvents.BrokeFree) != DragEvents.None)
        {
            _audio!.Play(_click, AudioBus.Sfx, s_breakClick);
        }
    }

    public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
    {
        if (!_hasParts)
        {
            // Textures and sounds can be created from the first frame on.
            _parts = _graphics!.CreateTexture(PlaceholderParts.Create());
            _hasParts = true;
            _click = _audio!.CreateSound(PlaceholderSounds.CreateClick());
            _hasClick = true;
        }

        if (!HasBackground && _assets!.TryGet(_benchImage, out var bench))
        {
            // The texture keeps no copy of the pixels, and nothing reads them back, so the image can go.
            _bench = _graphics!.CreateTexture(bench);
            _assets.Release(_benchImage);
            HasBackground = true;
        }

        _time = (float)context.TotalTime;
    }

    public void PrepareRender(double alpha)
    {
        var position = BasePin.GetRenderPosition((float)alpha);

        // Whole pixels, so the pin never shimmers between two columns.
        _pinDrawPosition = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
        _pinGlow = BasePin.IsHeld || BasePin.IsHovered
            ? GlowActive
            : (byte)(GlowIdleBase + (GlowIdlePulse * MathF.Sin(_time * GlowPulseRate)));
    }

    public void Render(SpriteBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        batch.Clear(MatColor);
        if (HasBackground)
        {
            batch.Draw(_bench, Vector2.Zero);
        }

        if (!_hasParts)
        {
            return;
        }

        // The frame goes on top, so the part of the pin still in the bore is hidden.
        batch.Draw(_parts, PlaceholderParts.PinGlow, _pinDrawPosition, new SpriteOptions { Origin = PlaceholderParts.PinGlowOrigin, Tint = Color.White.WithAlpha(_pinGlow) });
        batch.Draw(_parts, PlaceholderParts.Pin, _pinDrawPosition, new SpriteOptions { Origin = PlaceholderParts.PinOrigin });
        batch.Draw(_parts, PlaceholderParts.Frame, FramePosition);
    }
}
