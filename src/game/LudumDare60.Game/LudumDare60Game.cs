using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game;

/// <summary>
/// The Ludum Dare 60 game. For now the bench background (the cutting mat color until it loads) with the first order of
/// <c>orders.json</c> on it, as an <see cref="OrderBench"/>: its parts come off in the order their blocks allow, a dirty
/// part out on the mat stays put until the player takes the brush beside it and rubs it clean, and the parts go back in
/// reverse order. Parts that can move glow, a press on a blocked part flashes what blocks it, every snap clicks, and the
/// brush rasps louder the faster it rubs. The parts, the brush, the dirt and the sounds are placeholders until the real
/// art and recordings arrive.
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
    /// How long the parts that block a refused part flash, in seconds.
    /// </summary>
    public const float HintSeconds = 0.6f;

    /// <summary>
    /// How long the last dirt takes to fade once a part counts as clean, in seconds.
    /// </summary>
    public const float DirtFadeSeconds = 0.5f;

    /// <summary>
    /// Brush speed over a part, in virtual pixels per second, at which the scrub sound is at full volume.
    /// </summary>
    public const float BrushSpeedForFullVolume = 160;

    // Glow alpha of a part that can move, pulsing, and of the part under the pointer or in the hand.
    private const float GlowIdleBase = 70;
    private const float GlowIdlePulse = 50;
    private const float GlowPulseRate = 4;
    private const byte GlowActive = 255;
    private const byte GlowHintLow = 60;
    private const float HintBlinkRate = 10;

    private static readonly SoundOptions s_refusedClick = new() { Pitch = 0.4f, Volume = 0.3f };
    private static readonly SoundOptions s_cleanChime = new() { Pitch = 2f, Volume = 0.6f };
    private static readonly SoundOptions s_brushPickUp = new() { Pitch = 1.6f, Volume = 0.2f };
    private static readonly SoundOptions s_brushLand = new() { Pitch = 0.7f, Volume = 0.3f };
    private static readonly SoundOptions s_silent = new() { Volume = 0 };

    private AssetManager? _assets;
    private Graphics? _graphics;
    private Audio? _audio;
    private AssetHandle<Image> _benchImage;
    private AssetHandle<OrderBook> _ordersHandle;
    private bool _hasOrders;
    private Texture _bench;
    private Texture _parts;
    private bool _hasParts;
    private Sound _click;
    private Sound _scrubLoop;
    private bool _hasSounds;
    private Voice _scrubVoice;
    private bool _hasScrubVoice;
    private float _brushHint;
    private Vector2 _brushDrawPosition;
    private byte _brushGlow;
    private float _time;

    // Per part of the current order.
    private Vector2[] _drawPositions = [];
    private byte[] _glowAlphas = [];
    private float[] _hints = [];
    private float[] _dirtFades = [];
    private Texture[] _dirtTextures = [];

    /// <summary>
    /// Whether the bench background is loaded and drawn.
    /// </summary>
    public bool HasBackground { get; private set; }

    /// <summary>
    /// Every order, once <c>orders.json</c> is loaded.
    /// </summary>
    public OrderBook? Orders { get; private set; }

    /// <summary>
    /// The order on the bench, once <c>orders.json</c> is loaded.
    /// </summary>
    public OrderBench? Bench { get; private set; }

    public void Initialize(AssetManager assets, Graphics graphics, Audio audio)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
        _graphics = graphics;
        _audio = audio;
        assets.Register(new ImageLoader());
        assets.Register(new OrderBookLoader());
        _benchImage = assets.Request<Image>(AssetPaths.Bench);
        _ordersHandle = assets.Request<OrderBook>(AssetPaths.Orders);
    }

    public void FixedUpdate(in FrameContext context)
    {
        if (Bench is not { } bench)
        {
            return;
        }

        var deltaTime = (float)context.DeltaTime;
        bench.Step(PointerSample.From(context.Input), deltaTime);
        for (var i = 0; i < bench.Count; i++)
        {
            _hints[i] = MathF.Max(0, _hints[i] - deltaTime);
            if (bench.DirtAt(i) is { IsClean: true })
            {
                _dirtFades[i] = MathF.Max(0, _dirtFades[i] - (deltaTime / DirtFadeSeconds));
            }
        }

        _brushHint = MathF.Max(0, _brushHint - deltaTime);
        var events = bench.Gun.Events;
        for (var i = 0; i < bench.Count; i++)
        {
            if ((events[i] & DragEvents.Refused) != DragEvents.None)
            {
                Hint(bench, i);
            }

            PlayClick(bench.LayoutAt(i).ClickPitch, events[i]);
        }

        if ((bench.Events & BenchEvents.BareHands) != BenchEvents.None)
        {
            // Bare hands do nothing to dirt: the brush flashes.
            _brushHint = HintSeconds;
            PlaySound(s_refusedClick);
        }

        if ((bench.Events & BenchEvents.Cleaned) != BenchEvents.None)
        {
            // The last dirt fades out with a glint and a chime.
            _hints[bench.EventPart] = HintSeconds;
            PlaySound(s_cleanChime);
        }

        UseBrush(bench.BrushEvents);
    }

    public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
    {
        if (!_hasParts)
        {
            // Textures and sounds can be created from the first frame on.
            _parts = _graphics!.CreateTexture(PlaceholderParts.Create());
            _hasParts = true;
            _click = _audio!.CreateSound(PlaceholderSounds.CreateClick());
            _scrubLoop = _audio.CreateSound(PlaceholderSounds.CreateScrubLoop());
            _hasSounds = true;
        }

        if (!_hasOrders)
        {
            TakeOrders();
        }
        else
        {
            UploadScrubbedDirt();
        }

        if (!HasBackground && _assets!.TryGet(_benchImage, out var bench))
        {
            // The texture keeps no copy of the pixels, and nothing reads them back, so the image can go.
            _bench = _graphics!.CreateTexture(bench);
            _assets.Release(_benchImage);
            HasBackground = true;
        }

        if (_hasScrubVoice && Bench is { } orderBench)
        {
            // Louder and a little higher the faster the bristles move over the part; dirt rasps more than bare steel.
            var level = MathF.Sqrt(Math.Clamp(orderBench.BrushSpeed / BrushSpeedForFullVolume, 0, 1));
            var dirty = orderBench.ScrubbedPart >= 0 && orderBench.DirtAt(orderBench.ScrubbedPart) is { IsClean: false };
            var pitch = (dirty ? 0.85f : 1f) + (0.3f * level);
            _audio!.SetVoice(_scrubVoice, new SoundOptions { Volume = level * (dirty ? 0.9f : 0.45f), Pitch = pitch });
        }

        _time = (float)context.TotalTime;
    }

    public void PrepareRender(double alpha)
    {
        if (Bench is not { } bench)
        {
            return;
        }

        var idleGlow = (byte)(GlowIdleBase + (GlowIdlePulse * MathF.Sin(_time * GlowPulseRate)));
        var brushNeeded = false;
        for (var i = 0; i < bench.Count; i++)
        {
            var part = bench.Gun[i];
            var position = part.GetRenderPosition((float)alpha);

            // Whole pixels, so a part never shimmers between two columns.
            _drawPositions[i] = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
            var needsScrubbing = bench.NeedsScrubbing(i);
            brushNeeded |= needsScrubbing;
            _glowAlphas[i] = _hints[i] > 0 ? Blink(_hints[i])
                : part.IsHeld || part.IsHovered ? GlowActive
                : needsScrubbing ? bench.ScrubbedPart == i ? GlowActive : idleGlow
                : bench.Gun.CanMove(i) ? idleGlow
                : (byte)0;
        }

        var brush = bench.Brush;
        var brushPosition = brush.GetRenderPosition((float)alpha);
        _brushDrawPosition = new Vector2(MathF.Round(brushPosition.X), MathF.Round(brushPosition.Y));
        _brushGlow = brush.IsHeld ? (byte)0
            : _brushHint > 0 ? Blink(_brushHint)
            : brush.IsHovered ? GlowActive
            : brushNeeded ? idleGlow
            : (byte)0;
    }

    public void Render(SpriteBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        batch.Clear(MatColor);
        if (HasBackground)
        {
            batch.Draw(_bench, Vector2.Zero);
        }

        if (!_hasParts || Bench is not { } bench)
        {
            return;
        }

        // Parts under the frame first, so what of them is still inside is hidden; the rest over it.
        for (var i = 0; i < bench.Count; i++)
        {
            if (bench.LayoutAt(i).UnderFrame)
            {
                DrawPart(batch, bench, i);
            }
        }

        batch.Draw(_parts, PlaceholderParts.Frame, BenchLayout.FramePosition);
        for (var i = 0; i < bench.Count; i++)
        {
            if (!bench.LayoutAt(i).UnderFrame)
            {
                DrawPart(batch, bench, i);
            }
        }

        // The brush lies on top of everything, and in the hand it is above the parts it scrubs.
        var brushGlow = new SpriteOptions { Origin = PlaceholderParts.BrushOrigin + Vector2.One, Tint = Color.White.WithAlpha(_brushGlow) };
        batch.Draw(_parts, PlaceholderParts.BrushGlow, _brushDrawPosition, brushGlow);
        batch.Draw(_parts, PlaceholderParts.Brush, _brushDrawPosition, new SpriteOptions { Origin = PlaceholderParts.BrushOrigin });
    }

    private static byte Blink(float hint) => (int)(hint * HintBlinkRate) % 2 == 0 ? GlowActive : GlowHintLow;

    /// <summary>
    /// Sets up the first order once <c>orders.json</c> is loaded. A broken file stops the game with its problems, so
    /// they cannot go unnoticed.
    /// </summary>
    private void TakeOrders()
    {
        if (_assets!.GetState(_ordersHandle) == AssetState.Failed)
        {
            throw new InvalidOperationException($"{AssetPaths.Orders} did not load.", _assets.GetError(_ordersHandle));
        }

        if (!_assets.TryGet(_ordersHandle, out var orders))
        {
            return;
        }

        Orders = orders;
        _hasOrders = true;
        var bench = new OrderBench(orders.Orders[0]);
        var count = bench.Count;
        _drawPositions = new Vector2[count];
        _glowAlphas = new byte[count];
        _hints = new float[count];
        _dirtFades = new float[count];
        _dirtTextures = new Texture[count];
        for (var i = 0; i < count; i++)
        {
            _drawPositions[i] = bench.Gun[i].Position;
            if (bench.DirtAt(i) is { } dirt)
            {
                _dirtTextures[i] = _graphics!.CreateTexture(dirt.Image);
                _dirtFades[i] = 1;
            }
        }

        Bench = bench;
    }

    /// <summary>
    /// Uploads the rows of dirt the brush erased during this frame's steps, one range per dirty part.
    /// </summary>
    private void UploadScrubbedDirt()
    {
        var bench = Bench!;
        for (var i = 0; i < bench.Count; i++)
        {
            if (bench.DirtAt(i) is { } dirt && dirt.TryTakeChangedRows(out var firstRow, out var rowCount))
            {
                _graphics!.UpdateTexture(_dirtTextures[i], dirt.Image, firstRow, rowCount);
            }
        }
    }

    private void DrawPart(SpriteBatch batch, OrderBench bench, int part)
    {
        var layout = bench.LayoutAt(part);
        var position = _drawPositions[part];
        var glow = new SpriteOptions { Origin = layout.Origin + Vector2.One, Tint = Color.White.WithAlpha(_glowAlphas[part]) };
        batch.Draw(_parts, layout.Glow, position, glow);
        batch.Draw(_parts, layout.Sprite, position, new SpriteOptions { Origin = layout.Origin });
        if (bench.DirtAt(part) is not null && _dirtFades[part] > 0)
        {
            var dirt = new SpriteOptions { Origin = layout.Origin, Tint = Color.White.WithAlpha((byte)(255 * _dirtFades[part])) };
            batch.Draw(_dirtTextures[part], position, dirt);
        }
    }

    /// <summary>
    /// Flashes the parts that keep <paramref name="refused"/> from moving.
    /// </summary>
    private void Hint(OrderBench bench, int refused)
    {
        for (var i = 0; i < bench.Count; i++)
        {
            if (bench.Gun.Holds(i, refused))
            {
                _hints[i] = HintSeconds;
            }
        }
    }

    /// <summary>
    /// Sounds for taking and putting back the brush, and the scrub loop that plays while it is in the hand.
    /// </summary>
    private void UseBrush(BrushEvents events)
    {
        if (!_hasSounds)
        {
            return;
        }

        if ((events & BrushEvents.PickedUp) != BrushEvents.None)
        {
            PlaySound(s_brushPickUp);
            if (!_hasScrubVoice)
            {
                // Silent until the bristles move over a part; Update sets the volume every frame.
                _scrubVoice = _audio!.PlayLoop(_scrubLoop, AudioBus.Sfx, s_silent);
                _hasScrubVoice = true;
            }
        }

        if ((events & BrushEvents.PutDown) != BrushEvents.None && _hasScrubVoice)
        {
            _audio!.Stop(_scrubVoice, fadeOutSeconds: 0.06f);
            _hasScrubVoice = false;
        }

        if ((events & BrushEvents.Landed) != BrushEvents.None)
        {
            PlaySound(s_brushLand);
        }
    }

    private void PlaySound(in SoundOptions options)
    {
        if (_hasSounds)
        {
            _audio!.Play(_click, AudioBus.Sfx, options);
        }
    }

    private void PlayClick(float pitch, DragEvents events)
    {
        if (events == DragEvents.None)
        {
            return;
        }

        if ((events & DragEvents.SnappedStart) != DragEvents.None)
        {
            PlaySound(new SoundOptions { Pitch = 0.75f * pitch });
        }
        else if ((events & DragEvents.SnappedEnd) != DragEvents.None)
        {
            PlaySound(new SoundOptions { Pitch = 1.25f * pitch });
        }
        else if ((events & DragEvents.BrokeFree) != DragEvents.None)
        {
            PlaySound(new SoundOptions { Pitch = 0.55f * pitch, Volume = 0.4f });
        }
        else if ((events & DragEvents.Refused) != DragEvents.None)
        {
            PlaySound(s_refusedClick);
        }
    }
}
