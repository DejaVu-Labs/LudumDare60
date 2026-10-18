using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game;

/// <summary>
/// The Ludum Dare 60 game. For now the bench background (the cutting mat color until it loads) with the parts of
/// order 1 on it: a block of frame steel holding a rusty cylinder on a base pin. The pin comes out first, then the
/// cylinder; out on the mat, the rusty cylinder is scrubbed instead of dragged until it is clean; then the parts go back
/// in reverse order. Parts that can move glow, a press on a blocked part flashes what blocks it, and every snap clicks.
/// The parts, the rust and the sounds are placeholders until the real art and recordings arrive.
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
    /// Index of the base pin in <see cref="Gun"/>.
    /// </summary>
    public const int BasePinIndex = 0;

    /// <summary>
    /// Index of the cylinder in <see cref="Gun"/>.
    /// </summary>
    public const int CylinderIndex = 1;

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

    /// <summary>
    /// The cylinder's middle when it is seated in the frame's window, on the pin's axis.
    /// </summary>
    public static readonly Vector2 CylinderSeated = FramePosition + PlaceholderParts.CylinderInFrame + PlaceholderParts.CylinderOrigin;

    /// <summary>
    /// The cylinder's middle when it is out: below the frame, on the mat.
    /// </summary>
    public static readonly Vector2 CylinderOut = CylinderSeated + new Vector2(0, 36);

    /// <summary>
    /// How long the parts that block a refused part flash, in seconds.
    /// </summary>
    public const float HintSeconds = 0.6f;

    /// <summary>
    /// Radius of the scrubbing brush in virtual pixels.
    /// </summary>
    public const float BrushRadius = 2.5f;

    /// <summary>
    /// How long the last rust takes to fade once the cylinder counts as clean, in seconds.
    /// </summary>
    public const float RustFadeSeconds = 0.5f;

    private const int PartCount = 2;

    // At most one scratch sound per this many seconds while the brush erases rust.
    private const float BrushTickSeconds = 0.07f;

    // Glow alpha of a part that can move, pulsing, and of the part under the pointer or in the hand.
    private const float GlowIdleBase = 70;
    private const float GlowIdlePulse = 50;
    private const float GlowPulseRate = 4;
    private const byte GlowActive = 255;
    private const byte GlowHintLow = 60;
    private const float HintBlinkRate = 10;

    private static readonly IntRect[] s_sources = [PlaceholderParts.Pin, PlaceholderParts.Cylinder];
    private static readonly IntRect[] s_glows = [PlaceholderParts.PinGlow, PlaceholderParts.CylinderGlow];
    private static readonly Vector2[] s_origins = [PlaceholderParts.PinOrigin, PlaceholderParts.CylinderOrigin];

    // The cylinder is heavier: its clicks are lower.
    private static readonly float[] s_pitches = [1, 0.8f];

    private static readonly SoundOptions s_refusedClick = new() { Pitch = 0.4f, Volume = 0.3f };
    private static readonly SoundOptions s_brushTick = new() { Pitch = 2.4f, Volume = 0.12f };
    private static readonly SoundOptions s_cleanChime = new() { Pitch = 2f, Volume = 0.6f };

    private readonly Vector2[] _drawPositions = new Vector2[PartCount];
    private readonly byte[] _glowAlphas = new byte[PartCount];
    private readonly float[] _hints = new float[PartCount];

    private AssetManager? _assets;
    private Graphics? _graphics;
    private Audio? _audio;
    private AssetHandle<Image> _benchImage;
    private Texture _bench;
    private Texture _parts;
    private bool _hasParts;
    private Sound _click;
    private bool _hasClick;
    private Texture _rust;
    private float _rustFade = 1;
    private bool _brushing;
    private bool _brushHover;
    private Vector2 _lastBrush;
    private float _brushCooldown;
    private float _time;

    public LudumDare60Game()
    {
        Gun = new PartAssembly(
            new PartDrag(new LinePath(PinSeated, PinOut), grabRadius: 7, DragFeel.Default),
            new PartDrag(new LinePath(CylinderSeated, CylinderOut), grabRadius: 11, new DragFeel { BreakawayAtStart = 4 }));
        Gun.AddBlock(BasePinIndex, CylinderIndex);
    }

    /// <summary>
    /// Whether the bench background is loaded and drawn.
    /// </summary>
    public bool HasBackground { get; private set; }

    /// <summary>
    /// The parts of the gun on the bench, indexed by <see cref="BasePinIndex"/> and <see cref="CylinderIndex"/>.
    /// </summary>
    public PartAssembly Gun { get; }

    /// <summary>
    /// The rust on the cylinder. The cylinder cannot go back in before it is clean.
    /// </summary>
    public DirtLayer CylinderRust { get; } = new(PlaceholderParts.CreateCylinderRust());

    /// <summary>
    /// Whether the cylinder is out and rusty, so the pointer scrubs it instead of dragging it.
    /// </summary>
    public bool IsScrubbingCylinder => Cylinder.Stop == PartStop.End && !Cylinder.IsHeld && !CylinderRust.IsClean;

    /// <summary>
    /// The base pin, seated at <see cref="PinSeated"/> and out at <see cref="PinOut"/>.
    /// </summary>
    public PartDrag BasePin => Gun[BasePinIndex];

    /// <summary>
    /// The cylinder, seated at <see cref="CylinderSeated"/> and out at <see cref="CylinderOut"/>.
    /// </summary>
    public PartDrag Cylinder => Gun[CylinderIndex];

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
        var deltaTime = (float)context.DeltaTime;
        var input = PointerSample.From(context.Input);
        var scrubbing = IsScrubbingCylinder;
        Gun.SetLocked(CylinderIndex, scrubbing);
        Gun.Step(input, deltaTime);
        for (var i = 0; i < PartCount; i++)
        {
            _hints[i] = MathF.Max(0, _hints[i] - deltaTime);
        }

        _brushCooldown = MathF.Max(0, _brushCooldown - deltaTime);
        if (scrubbing)
        {
            Scrub(input);
        }
        else
        {
            _brushing = _brushHover = false;
        }

        if (CylinderRust.IsClean)
        {
            _rustFade = MathF.Max(0, _rustFade - (deltaTime / RustFadeSeconds));
        }

        var events = Gun.Events;
        for (var i = 0; i < PartCount; i++)
        {
            if ((events[i] & DragEvents.Refused) != DragEvents.None)
            {
                Hint(i);
            }

            PlayClick(i, events[i]);
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
            _rust = _graphics.CreateTexture(CylinderRust.Image);
        }
        else if (CylinderRust.TryTakeChangedRows(out var firstRow, out var rowCount))
        {
            // The rows the brush erased during this frame's steps, in one upload.
            _graphics!.UpdateTexture(_rust, CylinderRust.Image, firstRow, rowCount);
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
        var idleGlow = (byte)(GlowIdleBase + (GlowIdlePulse * MathF.Sin(_time * GlowPulseRate)));
        for (var i = 0; i < PartCount; i++)
        {
            var part = Gun[i];
            var position = part.GetRenderPosition((float)alpha);

            // Whole pixels, so a part never shimmers between two columns.
            _drawPositions[i] = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
            _glowAlphas[i] = _hints[i] > 0
                ? (int)(_hints[i] * HintBlinkRate) % 2 == 0 ? GlowActive : GlowHintLow
                : part.IsHeld || part.IsHovered ? GlowActive
                : Gun.CanMove(i) ? idleGlow
                : (byte)0;
        }

        if (IsScrubbingCylinder)
        {
            _glowAlphas[CylinderIndex] = _brushing || _brushHover ? GlowActive : idleGlow;
        }
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

        // The pin goes under the frame, so the part of it still in the bore is hidden; the cylinder sits in the
        // frame's window, over the pin.
        DrawPart(batch, BasePinIndex);
        batch.Draw(_parts, PlaceholderParts.Frame, FramePosition);
        DrawPart(batch, CylinderIndex);
        if (_rustFade > 0)
        {
            var rust = new SpriteOptions { Origin = PlaceholderParts.CylinderOrigin, Tint = Color.White.WithAlpha((byte)(255 * _rustFade)) };
            batch.Draw(_rust, _drawPositions[CylinderIndex], rust);
        }
    }

    /// <summary>
    /// Rubs the brush over the rusty cylinder: a press on it starts a stroke, and the stroke erases rust along the
    /// pointer's path for as long as the button stays down.
    /// </summary>
    private void Scrub(in PointerSample input)
    {
        // Cylinder pixels: the cylinder lies still at its end while it is scrubbed.
        var local = input.Position - CylinderOut + PlaceholderParts.CylinderOrigin;
        var image = CylinderRust.Image;
        _brushHover = local.X >= -BrushRadius && local.Y >= -BrushRadius
            && local.X <= image.Width + BrushRadius && local.Y <= image.Height + BrushRadius;
        if (!input.IsDown)
        {
            _brushing = false;
            return;
        }

        if (input.IsPressed && _brushHover)
        {
            _brushing = true;
            _lastBrush = local;
        }

        if (!_brushing)
        {
            return;
        }

        var erased = CylinderRust.Scrub(_lastBrush, local, BrushRadius);
        _lastBrush = local;
        if (CylinderRust.IsClean)
        {
            // The last rust fades out with a glint and a chime.
            _brushing = false;
            _hints[CylinderIndex] = HintSeconds;
            PlaySound(s_cleanChime);
        }
        else if (erased > 0 && _brushCooldown <= 0)
        {
            _brushCooldown = BrushTickSeconds;
            PlaySound(s_brushTick);
        }
    }

    private void PlaySound(in SoundOptions options)
    {
        if (_hasClick)
        {
            _audio!.Play(_click, AudioBus.Sfx, options);
        }
    }

    private void DrawPart(SpriteBatch batch, int part)
    {
        var position = _drawPositions[part];
        var glow = new SpriteOptions { Origin = s_origins[part] + Vector2.One, Tint = Color.White.WithAlpha(_glowAlphas[part]) };
        batch.Draw(_parts, s_glows[part], position, glow);
        batch.Draw(_parts, s_sources[part], position, new SpriteOptions { Origin = s_origins[part] });
    }

    /// <summary>
    /// Flashes the parts that keep <paramref name="refused"/> from moving.
    /// </summary>
    private void Hint(int refused)
    {
        for (var i = 0; i < PartCount; i++)
        {
            if (Gun.Holds(i, refused))
            {
                _hints[i] = HintSeconds;
            }
        }
    }

    private void PlayClick(int part, DragEvents events)
    {
        if (!_hasClick || events == DragEvents.None)
        {
            return;
        }

        var pitch = s_pitches[part];
        if ((events & DragEvents.SnappedStart) != DragEvents.None)
        {
            _audio!.Play(_click, AudioBus.Sfx, new SoundOptions { Pitch = 0.75f * pitch });
        }
        else if ((events & DragEvents.SnappedEnd) != DragEvents.None)
        {
            _audio!.Play(_click, AudioBus.Sfx, new SoundOptions { Pitch = 1.25f * pitch });
        }
        else if ((events & DragEvents.BrokeFree) != DragEvents.None)
        {
            _audio!.Play(_click, AudioBus.Sfx, new SoundOptions { Pitch = 0.55f * pitch, Volume = 0.4f });
        }
        else if ((events & DragEvents.Refused) != DragEvents.None)
        {
            _audio!.Play(_click, AudioBus.Sfx, s_refusedClick);
        }
    }
}
