using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;
using LudumDare60.Game.Bench;
using LudumDare60.Game.Cleaning;
using LudumDare60.Game.Customers;
using LudumDare60.Game.Orders;
using LudumDare60.Game.Parts;

namespace LudumDare60.Game;

/// <summary>
/// The Ludum Dare 60 game. For now the bench background (the cutting mat color until it loads) and the
/// <see cref="Customers.Shop"/> run on the orders of <c>orders.json</c>: a customer comes in through the door, puts the
/// revolver on the mat and shows what is wrong with it; the player repairs it on the <see cref="OrderBench"/> (parts
/// come off in the order their blocks allow, a dirty part out on the mat is rubbed clean with the brush, the parts go
/// back in reverse order), tests it (cocks the hammer, pulls the trigger), drags the whole revolver up to the
/// customer's hand, and the customer thanks the player and leaves. Parts that can move glow, a press on a blocked part flashes what blocks it, every snap clicks, and the
/// brush rasps louder the faster it rubs. Customers, parts, the brush, the dirt and the sounds are placeholders until
/// the real art and recordings arrive.
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

    /// <summary>
    /// Where a customer's bust stands, in virtual pixels: on the far edge of the bench.
    /// </summary>
    public const float CustomerBottom = 150;

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
    private static readonly SoundOptions s_gunLanded = new() { Pitch = 0.35f, Volume = 0.9f };
    private static readonly SoundOptions s_repaired = new() { Pitch = 1.5f, Volume = 0.5f };
    private static readonly SoundOptions s_handedBack = new() { Pitch = 0.9f, Volume = 0.6f };
    private static readonly SoundOptions s_doorBell = new() { Volume = 0.5f };
    private static readonly SoundOptions s_halfCock = new() { Pitch = 1.1f, Volume = 0.5f };
    private static readonly SoundOptions s_cocked = new() { Pitch = 0.9f, Volume = 0.9f };
    private static readonly SoundOptions s_cylinderTurn = new() { Pitch = 1.3f, Volume = 0.45f };
    private static readonly SoundOptions s_dryFire = new() { Pitch = 1.7f, Volume = 1f };
    private static readonly SoundOptions s_hammerEased = new() { Pitch = 0.6f, Volume = 0.4f };

    // How far back the trigger turns when pulled, in radians (counterclockwise: its tip goes towards the grip).
    private const float TriggerPullAngle = -0.35f;
    private static readonly SoundOptions s_silent = new() { Volume = 0 };

    // The hand held out, and the bubble with the symptom, relative to the customer's bottom middle.
    private static readonly Vector2 s_handOffset = new(-14, 12);
    private static readonly Vector2 s_bubbleOffset = new(16, -104);

    private AssetManager? _assets;
    private Graphics? _graphics;
    private Audio? _audio;
    private AssetHandle<Image> _benchImage;
    private AssetHandle<OrderBook> _ordersHandle;
    private bool _hasOrders;
    private Texture _bench;
    private Texture _parts;
    private Texture _customers;
    private bool _hasSheets;
    private Sound _click;
    private Sound _scrubLoop;
    private Sound _doorBell;
    private bool _hasSounds;
    private Voice _scrubVoice;
    private bool _hasScrubVoice;
    private float _brushHint;
    private float _bodyHint;
    private float _hammerHint;
    private Vector2 _brushDrawPosition;
    private byte _brushGlow;
    private byte _bodyGlow;
    private byte _hammerGlow;
    private byte _triggerGlow;
    private float _hammerAngle;
    private float _triggerAngle;
    private Vector2 _gunOffset;
    private float _time;

    // The bench the arrays below belong to, and per part of its order.
    private OrderBench? _shownBench;
    private Vector2[] _drawPositions = [];
    private byte[] _glowAlphas = [];
    private float[] _hints = [];
    private float[] _dirtFades = [];
    private Texture[] _dirtTextures = [];
    private bool[] _hasDirtTexture = [];

    /// <summary>
    /// Whether the bench background is loaded and drawn.
    /// </summary>
    public bool HasBackground { get; private set; }

    /// <summary>
    /// Every order, once <c>orders.json</c> is loaded.
    /// </summary>
    public OrderBook? Orders { get; private set; }

    /// <summary>
    /// The shop, once <c>orders.json</c> is loaded.
    /// </summary>
    public Shop? Shop { get; private set; }

    /// <summary>
    /// The current customer's order on the bench, while there is a customer.
    /// </summary>
    public OrderBench? Bench => Shop?.Bench;

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
        if (Shop is not { } shop)
        {
            return;
        }

        var deltaTime = (float)context.DeltaTime;
        shop.Step(PointerSample.From(context.Input), deltaTime);
        PlayShopSounds(shop.Events);
        _bodyHint = MathF.Max(0, _bodyHint - deltaTime);
        _hammerHint = MathF.Max(0, _hammerHint - deltaTime);
        if ((shop.Events & ShopEvents.Repaired) != ShopEvents.None)
        {
            // The revolver shines once; the customer holds out a hand.
            _bodyHint = HintSeconds;
        }

        if (shop.TestStepped)
        {
            PlayTestSounds(shop.Test!.Events);
        }

        if (shop.Bench is not { } bench || bench != _shownBench)
        {
            // The arrays are set up for a new bench in Update.
            return;
        }

        for (var i = 0; i < bench.Count; i++)
        {
            _hints[i] = MathF.Max(0, _hints[i] - deltaTime);
            if (bench.DirtAt(i) is { IsClean: true })
            {
                _dirtFades[i] = MathF.Max(0, _dirtFades[i] - (deltaTime / DirtFadeSeconds));
            }
        }

        _brushHint = MathF.Max(0, _brushHint - deltaTime);
        if (shop.BenchStepped)
        {
            HandleBenchEvents(bench);
        }
    }

    public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
    {
        if (!_hasSheets)
        {
            // Textures and sounds can be created from the first frame on.
            _parts = _graphics!.CreateTexture(PlaceholderParts.Create());
            _customers = _graphics.CreateTexture(PlaceholderCustomers.Create());
            _hasSheets = true;
            _click = _audio!.CreateSound(PlaceholderSounds.CreateClick());
            _scrubLoop = _audio.CreateSound(PlaceholderSounds.CreateScrubLoop());
            _doorBell = _audio.CreateSound(PlaceholderSounds.CreateDoorBell());
            _hasSounds = true;
        }

        if (!_hasOrders)
        {
            TakeOrders();
        }

        if (Bench != _shownBench)
        {
            ShowBench(Bench);
        }

        UploadScrubbedDirt();
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
        if (Shop is not { HasGun: true } shop || _shownBench is not { } bench)
        {
            return;
        }

        var idleGlow = (byte)(GlowIdleBase + (GlowIdlePulse * MathF.Sin(_time * GlowPulseRate)));
        var working = shop.Phase == ShopPhase.Working;
        var gunOffset = shop.GetGunOffset((float)alpha);
        _gunOffset = new Vector2(MathF.Round(gunOffset.X), MathF.Round(gunOffset.Y));
        var brushNeeded = false;
        for (var i = 0; i < bench.Count; i++)
        {
            var part = bench.Gun[i];

            // Only a bench that is still being stepped moves between steps; afterwards the last step's motion is stale.
            var position = working ? part.GetRenderPosition((float)alpha) : part.Position;

            // Whole pixels, so a part never shimmers between two columns.
            _drawPositions[i] = new Vector2(MathF.Round(position.X), MathF.Round(position.Y));
            var needsScrubbing = bench.NeedsScrubbing(i);
            brushNeeded |= needsScrubbing;
            _glowAlphas[i] = !working ? (byte)0
                : _hints[i] > 0 ? Blink(_hints[i])
                : part.IsHeld || part.IsHovered ? GlowActive
                : needsScrubbing ? bench.ScrubbedPart == i ? GlowActive : idleGlow
                : bench.Gun.CanMove(i) ? idleGlow
                : (byte)0;
        }

        var handBack = shop.HandBack!;
        _bodyGlow = shop.Phase != ShopPhase.Ready ? (byte)0
            : _bodyHint > 0 ? Blink(_bodyHint)
            : handBack.IsHeld || handBack.IsHovered ? GlowActive
            : idleGlow;

        // The test: first the hammer glows, once it is cocked the trigger.
        var test = shop.Test!;
        var testing = shop.Phase == ShopPhase.Testing;
        _hammerAngle = test.GetHammerAngle((float)alpha);
        _triggerAngle = test.TriggerPull * TriggerPullAngle;
        _hammerGlow = !testing || test.IsCocked ? (byte)0
            : _hammerHint > 0 ? Blink(_hammerHint)
            : test.Hammer.IsHeld || test.Hammer.IsHovered ? GlowActive
            : idleGlow;
        _triggerGlow = !testing || !test.IsCocked ? (byte)0
            : test.IsTriggerHovered ? GlowActive
            : idleGlow;

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

        if (!_hasSheets || Shop is not { } shop)
        {
            return;
        }

        if (shop.HasCustomer && shop.Bench is { } visitor)
        {
            DrawCustomer(batch, shop, visitor.Order);
        }

        if (!shop.HasGun || _shownBench is not { } bench)
        {
            return;
        }

        // Parts under the frame first, so what of them is still inside is hidden; the rest over it.
        var gun = BenchLayout.GunPosition + _gunOffset;
        var bodyGlow = new SpriteOptions { Origin = Vector2.One, Tint = Color.White.WithAlpha(_bodyGlow) };
        batch.Draw(_parts, PlaceholderParts.BodyGlow, gun, bodyGlow);
        for (var i = 0; i < bench.Count; i++)
        {
            if (bench.LayoutAt(i).UnderFrame)
            {
                DrawPart(batch, bench, i);
            }
        }

        batch.Draw(_parts, PlaceholderParts.Body, gun);
        for (var i = 0; i < bench.Count; i++)
        {
            if (!bench.LayoutAt(i).UnderFrame)
            {
                DrawPart(batch, bench, i);
            }
        }

        // The hammer and the trigger turn around their pivots.
        DrawTurning(batch, PlaceholderParts.Hammer, PlaceholderParts.HammerGlow, PlaceholderParts.HammerOrigin, BenchLayout.HammerPivot, _hammerAngle, _hammerGlow);
        DrawTurning(batch, PlaceholderParts.Trigger, PlaceholderParts.TriggerGlow, PlaceholderParts.TriggerOrigin, BenchLayout.TriggerPivot, _triggerAngle, _triggerGlow);

        // The brush lies on top of everything, and in the hand it is above the parts it scrubs.
        var brushGlow = new SpriteOptions { Origin = PlaceholderParts.BrushOrigin + Vector2.One, Tint = Color.White.WithAlpha(_brushGlow) };
        batch.Draw(_parts, PlaceholderParts.BrushGlow, _brushDrawPosition, brushGlow);
        batch.Draw(_parts, PlaceholderParts.Brush, _brushDrawPosition, new SpriteOptions { Origin = PlaceholderParts.BrushOrigin });
    }

    private static byte Blink(float hint) => (int)(hint * HintBlinkRate) % 2 == 0 ? GlowActive : GlowHintLow;

    /// <summary>
    /// Opens the shop once <c>orders.json</c> is loaded. A broken file stops the game with its problems, so they cannot
    /// go unnoticed.
    /// </summary>
    private void TakeOrders()
    {
        if (_assets!.GetState(_ordersHandle) == AssetState.Failed)
        {
            throw new InvalidOperationException($"{AssetPaths.Orders} did not load.", _assets.GetError(_ordersHandle));
        }

        if (_assets.TryGet(_ordersHandle, out var orders))
        {
            Orders = orders;
            Shop = new Shop(orders);
            _hasOrders = true;
        }
    }

    /// <summary>
    /// Sets up drawing for a new customer's bench, or none, and lets go of the last one's dirt textures.
    /// </summary>
    private void ShowBench(OrderBench? bench)
    {
        for (var i = 0; i < _hasDirtTexture.Length; i++)
        {
            if (_hasDirtTexture[i])
            {
                _graphics!.DestroyTexture(_dirtTextures[i]);
            }
        }

        if (_hasScrubVoice)
        {
            _audio!.Stop(_scrubVoice);
            _hasScrubVoice = false;
        }

        _shownBench = bench;
        var count = bench?.Count ?? 0;
        _drawPositions = new Vector2[count];
        _glowAlphas = new byte[count];
        _hints = new float[count];
        _dirtFades = new float[count];
        _dirtTextures = new Texture[count];
        _hasDirtTexture = new bool[count];
        _brushHint = 0;
        for (var i = 0; i < count; i++)
        {
            _drawPositions[i] = bench!.Gun[i].Position;
            if (bench.DirtAt(i) is { } dirt)
            {
                _dirtTextures[i] = _graphics!.CreateTexture(dirt.Image);
                _hasDirtTexture[i] = true;
                _dirtFades[i] = 1;
            }
        }

        if (bench is not null)
        {
            _brushDrawPosition = bench.Brush.Position;
        }
    }

    /// <summary>
    /// Uploads the rows of dirt the brush erased during this frame's steps, one range per dirty part.
    /// </summary>
    private void UploadScrubbedDirt()
    {
        if (_shownBench is not { } bench)
        {
            return;
        }

        for (var i = 0; i < bench.Count; i++)
        {
            if (bench.DirtAt(i) is { } dirt && dirt.TryTakeChangedRows(out var firstRow, out var rowCount))
            {
                _graphics!.UpdateTexture(_dirtTextures[i], dirt.Image, firstRow, rowCount);
            }
        }
    }

    private void DrawCustomer(SpriteBatch batch, Shop shop, Order order)
    {
        var look = order.Customer.Look;
        var bottom = new Vector2(MathF.Round(shop.CustomerX), CustomerBottom);
        batch.Draw(_customers, PlaceholderCustomers.Bust(look), bottom, new SpriteOptions { Origin = PlaceholderCustomers.BustOrigin });
        var bustTopLeft = bottom - PlaceholderCustomers.BustOrigin;
        batch.Draw(_customers, PlaceholderCustomers.Face(shop.Expression), bustTopLeft + PlaceholderCustomers.FaceOffset);
        if (shop.IsHandOut)
        {
            batch.Draw(_customers, PlaceholderCustomers.Hand, bottom + s_handOffset, new SpriteOptions { Tint = PlaceholderCustomers.Skin(look) });
        }

        if (shop.Phase == ShopPhase.Working)
        {
            var bubble = bottom + s_bubbleOffset;
            batch.Draw(_customers, PlaceholderCustomers.Bubble, bubble);
            batch.Draw(_customers, PlaceholderCustomers.Icon(order.Symptom), bubble + PlaceholderCustomers.IconInBubble);
        }
    }

    private void DrawTurning(SpriteBatch batch, IntRect sprite, IntRect glow, Vector2 origin, Vector2 pivot, float angle, byte glowAlpha)
    {
        var position = pivot + _gunOffset;
        var glowOptions = new SpriteOptions { Origin = origin + Vector2.One, Rotation = angle, Tint = Color.White.WithAlpha(glowAlpha) };
        batch.Draw(_parts, glow, position, glowOptions);
        batch.Draw(_parts, sprite, position, new SpriteOptions { Origin = origin, Rotation = angle });
    }

    private void DrawPart(SpriteBatch batch, OrderBench bench, int part)
    {
        var layout = bench.LayoutAt(part);

        // The cylinder shows which chamber the last cock turned it to.
        var sprite = layout.TurnedSprite is { } turned && Shop!.Test is { Chamber: var chamber } && chamber % 2 == 1 ? turned : layout.Sprite;

        // A part in the gun moves with it; one out on the mat stays where it lies.
        var position = _drawPositions[part] + (bench.Gun[part].Stop == PartStop.Start ? _gunOffset : Vector2.Zero);
        var glow = new SpriteOptions { Origin = layout.Origin + Vector2.One, Tint = Color.White.WithAlpha(_glowAlphas[part]) };
        batch.Draw(_parts, layout.Glow, position, glow);
        batch.Draw(_parts, sprite, position, new SpriteOptions { Origin = layout.Origin });
        if (_hasDirtTexture[part] && _dirtFades[part] > 0)
        {
            var dirt = new SpriteOptions { Origin = layout.Origin, Tint = Color.White.WithAlpha((byte)(255 * _dirtFades[part])) };
            batch.Draw(_dirtTextures[part], position, dirt);
        }
    }

    private void HandleBenchEvents(OrderBench bench)
    {
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

    private void PlayTestSounds(TestEvents events)
    {
        if (events == TestEvents.None)
        {
            return;
        }

        if ((events & TestEvents.HalfCock) != TestEvents.None)
        {
            PlaySound(s_halfCock);
        }

        if ((events & TestEvents.Cocked) != TestEvents.None)
        {
            // Full cock, and the cylinder's ratchet turns it one chamber.
            PlaySound(s_cocked);
            PlaySound(s_cylinderTurn);
        }

        if ((events & TestEvents.HammerEased) != TestEvents.None)
        {
            PlaySound(s_hammerEased);
        }

        if ((events & TestEvents.DryFire) != TestEvents.None)
        {
            PlaySound(s_dryFire);
        }

        if ((events & TestEvents.TriggerRefused) != TestEvents.None)
        {
            // Single action: the hammer comes first, and it flashes.
            _hammerHint = HintSeconds;
            PlaySound(s_refusedClick);
        }
    }

    private void PlayShopSounds(ShopEvents events)
    {
        if (!_hasSounds || events == ShopEvents.None)
        {
            return;
        }

        if ((events & (ShopEvents.CustomerEntered | ShopEvents.CustomerLeft)) != ShopEvents.None)
        {
            _audio!.Play(_doorBell, AudioBus.Sfx, s_doorBell);
        }

        if ((events & ShopEvents.GunLanded) != ShopEvents.None)
        {
            PlaySound(s_gunLanded);
        }

        if ((events & ShopEvents.Repaired) != ShopEvents.None)
        {
            PlaySound(s_repaired);
        }

        if ((events & ShopEvents.HandedBack) != ShopEvents.None)
        {
            PlaySound(s_handedBack);
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
