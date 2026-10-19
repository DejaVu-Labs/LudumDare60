using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Input;
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
/// <see cref="Customers.Shop"/> run on the orders of <c>orders.json</c>: a customer comes in through the door and waits at
/// the counter, glowing, until the player clicks them; the revolver flies onto the mat and they show what is wrong
/// with it; the player repairs it on the <see cref="OrderBench"/> (half cock
/// first, then parts come off in the order their blocks allow and can be laid anywhere on the mat, screws by turning
/// them, a dirty part is rubbed clean with the brush, a dry one oiled with the oil can, a broken one swapped in the
/// tray of spares, the parts go back in reverse order, sticking to their rails when brought close), tests
/// it (full cock, pulls the trigger), drags the whole revolver up to the
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
    /// Where the top-left corner of the street is drawn, in virtual pixels: raised so that the embankment with its
    /// lamps, railing and reflections fills the window panes and the door glass of the room.
    /// </summary>
    public static readonly Vector2 StreetOffset = new(0, -125);

    /// <summary>
    /// The tint of a customer waiting out in the street: darker and cooler, so they belong to the rainy night outside
    /// and not to the warm lamp light of the room.
    /// </summary>
    public static readonly Color OutsideTint = Color.FromRgb(0xa5aad2);

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
    /// How much larger than their sheet a customer is drawn, around the bottom middle of the bust.
    /// </summary>
    public const float CustomerScale = 1.4f;

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
    private static readonly SoundOptions s_gunThrown = new() { Pitch = 0.5f, Volume = 0.25f };
    private static readonly SoundOptions s_gunLanded = new() { Pitch = 0.35f, Volume = 0.9f };
    private static readonly SoundOptions s_repaired = new() { Pitch = 1.5f, Volume = 0.5f };
    private static readonly SoundOptions s_handedBack = new() { Pitch = 0.9f, Volume = 0.6f };
    private static readonly SoundOptions s_doorBell = new() { Volume = 0.5f };
    private static readonly SoundOptions s_halfCock = new() { Pitch = 1.1f, Volume = 0.5f };
    private static readonly SoundOptions s_cocked = new() { Pitch = 0.9f, Volume = 0.9f };
    private static readonly SoundOptions s_cylinderTurn = new() { Pitch = 1.3f, Volume = 0.45f };
    private static readonly SoundOptions s_dryFire = new() { Pitch = 1.7f, Volume = 1f };
    private static readonly SoundOptions s_hammerEased = new() { Pitch = 0.6f, Volume = 0.4f };
    private static readonly SoundOptions s_oilCanPickUp = new() { Pitch = 1.2f, Volume = 0.2f };
    private static readonly SoundOptions s_oilCanLand = new() { Pitch = 0.6f, Volume = 0.3f };
    private static readonly SoundOptions s_oilDrop = new() { Pitch = 2.4f, Volume = 0.35f };
    private static readonly SoundOptions s_spareSwapped = new() { Pitch = 0.6f, Volume = 0.6f };

    // A screw ticks every quarter turn, softly.
    private const float ScrewTickVolume = 0.2f;

    // How far back the trigger turns when pulled, in radians (counterclockwise: its tip goes towards the grip).
    private const float TriggerPullAngle = -0.35f;
    private static readonly SoundOptions s_silent = new() { Volume = 0 };

    // The hand held out, and the bubble with the symptom, relative to the customer's bottom middle.
    private static readonly Vector2 s_handOffset = new(-14, 12);
    private static readonly Vector2 s_bubbleOffset = new(48, -120);

    private AssetManager? _assets;
    private Graphics? _graphics;
    private Audio? _audio;
    private AssetHandle<Image> _benchImage;
    private AssetHandle<Image> _streetImage;
    private AssetHandle<OrderBook> _ordersHandle;
    private bool _hasOrders;
    private Texture _bench;
    private Texture _street;
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
    private float _oilCanHint;
    private float _trayHint;
    private float _bodyHint;
    private float _hammerHint;
    private Vector2 _brushDrawPosition;
    private Vector2 _oilCanDrawPosition;
    private byte _brushGlow;
    private byte _oilCanGlow;
    private byte _trayGlow;
    private byte _bodyGlow;
    private byte _hammerGlow;
    private byte _triggerGlow;
    private byte _customerGlow;
    private float _hammerAngle;
    private float _triggerAngle;
    private Vector2 _gunOffset;
    private float _time;

    // The drawn customer sheets, as AssetPaths.CustomerSheets lists them.
    private readonly AssetHandle<Image>[] _customerImages = new AssetHandle<Image>[AssetPaths.CustomerSheets.Count];
    private readonly Texture[] _customerSheets = new Texture[AssetPaths.CustomerSheets.Count];
    private readonly Texture[] _customerGlows = new Texture[AssetPaths.CustomerSheets.Count];
    private readonly bool[] _hasCustomerSheet = new bool[AssetPaths.CustomerSheets.Count];

    // The bench the arrays below belong to, and per part of its order.
    private OrderBench? _shownBench;
    private Vector2[] _drawPositions = [];
    private float[] _rotations = [];
    private byte[] _glowAlphas = [];
    private float[] _hints = [];
    private float[] _dirtFades = [];
    private Texture[] _dirtTextures = [];
    private bool[] _hasDirtTexture = [];

    // Every part of the revolver that has a place on the bench, back to front as the gun draws them: its layout, and
    // its index in the bench, or -1 for a part the order keeps fixed.
    private PartLayout[] _drawLayouts = [];
    private int[] _drawParts = [];

    /// <summary>
    /// Whether the bench background is loaded and drawn.
    /// </summary>
    public bool HasBackground { get; private set; }

    /// <summary>
    /// Whether the street seen through the windows is loaded and drawn.
    /// </summary>
    public bool HasStreet { get; private set; }

    /// <summary>
    /// Whether the debug keys work: 1 to 9 bring in the customer of that order at once, 0 brings the current one in
    /// again. The page turns them on with <c>?debug</c> in its address.
    /// </summary>
    public bool HasDebugKeys { get; init; }

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
        _streetImage = assets.Request<Image>(AssetPaths.Street);
        for (var i = 0; i < _customerImages.Length; i++)
        {
            _customerImages[i] = assets.Request<Image>(AssetPaths.CustomerSheets[i].Value);
        }

        _ordersHandle = assets.Request<OrderBook>(AssetPaths.Orders);
    }

    public void FixedUpdate(in FrameContext context)
    {
        if (Shop is not { } shop)
        {
            return;
        }

        if (HasDebugKeys && DebugOrder(context.Input, shop, Orders!.Orders.Count) is var order and >= 0)
        {
            shop.Restart(order);
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
        _oilCanHint = MathF.Max(0, _oilCanHint - deltaTime);
        _trayHint = MathF.Max(0, _trayHint - deltaTime);
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

        ThrowIfAnyAssetFailed();
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

        if (!HasStreet && _assets!.TryGet(_streetImage, out var street))
        {
            _street = _graphics!.CreateTexture(street);
            _assets.Release(_streetImage);
            HasStreet = true;
        }

        for (var i = 0; i < _customerImages.Length; i++)
        {
            if (!_hasCustomerSheet[i] && _assets!.TryGet(_customerImages[i], out var sheet))
            {
                _customerSheets[i] = _graphics!.CreateTexture(sheet);
                // A bust is cut off at the bottom, so its glow is open there; a parcel is whole and glows all around.
                var openBottom = !CustomerSeats.TryGetFixedPlace(AssetPaths.CustomerSheets[i].Key, out _);
                _customerGlows[i] = _graphics.CreateTexture(CustomerSheet.CreateGlow(sheet, openBottom));
                _assets.Release(_customerImages[i]);
                _hasCustomerSheet[i] = true;
            }
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
        if (Shop is not { } shop)
        {
            return;
        }

        // The customer glows while they wait for the player to take the revolver.
        var idleGlow = (byte)(GlowIdleBase + (GlowIdlePulse * MathF.Sin(_time * GlowPulseRate)));
        _customerGlow = shop.Phase != ShopPhase.Offering ? (byte)0
            : shop.IsCustomerHovered ? GlowActive
            : idleGlow;

        // The tools stay on the bench between customers. Only a bench that is still being stepped moves them between
        // steps; afterwards the last step's motion is stale.
        var working = shop.Phase == ShopPhase.Working;
        var brushPosition = working ? shop.Brush.GetRenderPosition((float)alpha) : shop.Brush.Position;
        _brushDrawPosition = new Vector2(MathF.Round(brushPosition.X), MathF.Round(brushPosition.Y));
        var oilCanPosition = working ? shop.OilCan.GetRenderPosition((float)alpha) : shop.OilCan.Position;
        _oilCanDrawPosition = new Vector2(MathF.Round(oilCanPosition.X), MathF.Round(oilCanPosition.Y));
        if (!shop.HasGun || _shownBench is not { } bench)
        {
            _brushGlow = 0;
            _oilCanGlow = 0;
            _trayGlow = 0;
            return;
        }

        var gunOffset = shop.GetGunOffset((float)alpha);
        _gunOffset = new Vector2(MathF.Round(gunOffset.X), MathF.Round(gunOffset.Y));
        var test = shop.Test!;
        _hammerAngle = test.GetHammerAngle((float)alpha);
        var brushNeeded = false;
        var oilNeeded = false;
        var spareNeeded = false;
        for (var i = 0; i < bench.Count; i++)
        {
            var part = bench.Gun[i];
            var layout = bench.LayoutAt(i);

            // Only a bench that is still being stepped moves between steps; afterwards the last step's motion is stale.
            var position = working ? part.GetRenderPosition((float)alpha) : part.Position;
            var distance = working ? float.Lerp(part.PreviousDistance, part.Distance, (float)alpha) : part.Distance;

            // Whole pixels for the sprite's corner, so a part never shimmers between two columns.
            var corner = position - layout.Origin;
            _drawPositions[i] = new Vector2(MathF.Round(corner.X), MathF.Round(corner.Y)) + layout.Origin;

            _rotations[i] = OrderBench.PartAngle(layout, part.IsFree, distance, _hammerAngle);
            var needsScrubbing = bench.NeedsScrubbing(i);
            var needsOiling = bench.NeedsOiling(i);
            var needsSpare = bench.NeedsSpare(i);
            brushNeeded |= needsScrubbing;
            oilNeeded |= needsOiling;
            spareNeeded |= needsSpare;
            _glowAlphas[i] = !working ? (byte)0
                : _hints[i] > 0 ? Blink(_hints[i])
                : part.IsHeld || part.IsHovered ? GlowActive
                : needsScrubbing ? bench.ScrubbedPart == i ? GlowActive : idleGlow
                : needsOiling || needsSpare ? idleGlow
                : bench.Gun.CanMove(i) ? idleGlow
                : (byte)0;
        }

        var handBack = shop.HandBack!;
        _bodyGlow = shop.Phase != ShopPhase.Ready ? (byte)0
            : _bodyHint > 0 ? Blink(_bodyHint)
            : handBack.IsHeld || handBack.IsHovered ? GlowActive
            : idleGlow;

        // The hammer glows when it is the next step: half cock before anything else, full cock for the test. Once it
        // is at full cock, the trigger glows.
        var testing = shop.Phase == ShopPhase.Testing;
        var hammerNext = (working && test.State == HammerState.Down) || (testing && test.State != HammerState.FullCock);
        _triggerAngle = test.TriggerPull * TriggerPullAngle;
        _hammerGlow = _hammerHint > 0 ? Blink(_hammerHint)
            : !hammerNext ? (byte)0
            : test.IsHammerHeld || test.IsHammerHovered ? GlowActive
            : idleGlow;
        _triggerGlow = !testing || test.State != HammerState.FullCock ? (byte)0
            : test.IsTriggerHovered ? GlowActive
            : idleGlow;

        var brush = bench.Brush;
        _brushGlow = brush.IsHeld ? (byte)0
            : _brushHint > 0 ? Blink(_brushHint)
            : brush.IsHovered ? GlowActive
            : brushNeeded ? idleGlow
            : (byte)0;

        var oilCan = bench.OilCan;
        _oilCanGlow = oilCan.IsHeld ? (byte)0
            : _oilCanHint > 0 ? Blink(_oilCanHint)
            : oilCan.IsHovered ? GlowActive
            : oilNeeded ? idleGlow
            : (byte)0;
        _trayGlow = _trayHint > 0 ? Blink(_trayHint)
            : spareNeeded ? idleGlow
            : (byte)0;
    }

    public void Render(SpriteBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        batch.Clear(MatColor);
        if (HasStreet)
        {
            // Under the room: it shows only through the window panes and the door glass.
            batch.Draw(_street, StreetOffset);
        }

        if (_hasSheets && Shop is { HasCustomer: true, Bench: { } outsider } outsideShop)
        {
            // A customer waiting out in the street is seen through the window: the room covers them.
            DrawCustomer(batch, outsideShop, outsider.Order, outsidePass: true);
        }

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
            DrawCustomer(batch, shop, visitor.Order, outsidePass: false);
        }

        // The tray of spares stays on the mat between customers; while a revolver is on the bench it holds the spare, or,
        // once swapped, the broken part.
        var trayGlow = new SpriteOptions { Origin = Vector2.One, Tint = Color.White.WithAlpha(_trayGlow) };
        batch.Draw(_parts, PlaceholderParts.TrayGlow, BenchLayout.TrayMin, trayGlow);
        batch.Draw(_parts, PlaceholderParts.Tray, BenchLayout.TrayMin);
        if (shop.HasGun && _shownBench is { } bench)
        {
            DrawRevolver(batch, bench);
        }

        // The tools lie on top of everything, and in the hand they are above the parts they work on. They stay on the
        // bench between customers, where the player left them.
        var oilCanGlow = new SpriteOptions { Origin = PlaceholderParts.OilCanOrigin + Vector2.One, Tint = Color.White.WithAlpha(_oilCanGlow) };
        batch.Draw(_parts, PlaceholderParts.OilCanGlow, _oilCanDrawPosition, oilCanGlow);
        batch.Draw(_parts, PlaceholderParts.OilCan, _oilCanDrawPosition, new SpriteOptions { Origin = PlaceholderParts.OilCanOrigin });
        var brushGlow = new SpriteOptions { Origin = PlaceholderParts.BrushOrigin + Vector2.One, Tint = Color.White.WithAlpha(_brushGlow) };
        batch.Draw(_parts, PlaceholderParts.BrushGlow, _brushDrawPosition, brushGlow);
        batch.Draw(_parts, PlaceholderParts.Brush, _brushDrawPosition, new SpriteOptions { Origin = PlaceholderParts.BrushOrigin });
    }

    /// <summary>
    /// Draws what of the revolver lies on the bench: the spare in the tray, the parts in the gun and the ones loose on
    /// the mat.
    /// </summary>
    private void DrawRevolver(SpriteBatch batch, OrderBench bench)
    {
        if (bench.SparePart >= 0)
        {
            var spare = bench.LayoutAt(bench.SparePart);
            var inTray = bench.IsBroken(bench.SparePart) ? spare.Sprite : spare.BrokenSprite ?? spare.Sprite;
            batch.Draw(_parts, inTray, BenchLayout.TraySpare, new SpriteOptions { Origin = spare.Origin });
        }

        // Parts in the gun back to front: under the frame first, so what of them is still inside is hidden, then over
        // it, the hammer and the trigger turning around their pivots, and what sits on the hammer.
        var gun = BenchLayout.GunPosition + _gunOffset;
        var bodyGlow = new SpriteOptions { Origin = Vector2.One, Tint = Color.White.WithAlpha(_bodyGlow) };
        batch.Draw(_parts, PlaceholderParts.BodyGlow, gun, bodyGlow);
        DrawLayers(batch, bench, PartLayer.UnderFrame, PartLayer.UnderFrame);
        batch.Draw(_parts, PlaceholderParts.Body, gun);
        DrawLayers(batch, bench, PartLayer.OverFrame, PartLayer.Hammer);
        DrawTurning(batch, PlaceholderParts.Trigger, PlaceholderParts.TriggerGlow, PlaceholderParts.TriggerOrigin, BenchLayout.TriggerPivot + _gunOffset, _triggerAngle, _triggerGlow);
        DrawLayers(batch, bench, PartLayer.OverHammer, PartLayer.OverHammer);

        // Parts loose on the mat lie on top of everything, even when put down on the revolver, a later one over an
        // earlier one: the order PartAssembly picks them in for the pointer.
        for (var i = 0; i < bench.Count; i++)
        {
            if (bench.Gun[i].IsFree)
            {
                DrawPart(batch, bench, i);
            }
        }
    }

    /// <summary>
    /// The order a debug key pressed in this step asks for, or -1: 1 to 9 the order of that number, 0 the current one.
    /// </summary>
    private static int DebugOrder(InputState input, Shop shop, int orderCount)
    {
        if (orderCount == 0)
        {
            return -1;
        }

        if (input.IsPressed(Key.Digit0) || input.IsPressed(Key.Numpad0))
        {
            // Once the shop has closed, the last order.
            return Math.Min(shop.OrderIndex, orderCount - 1);
        }

        for (var i = 0; i < 9 && i < orderCount; i++)
        {
            if (input.IsPressed(Key.Digit1 + i) || input.IsPressed(Key.Numpad1 + i))
            {
                return i;
            }
        }

        return -1;
    }

    private static byte Blink(float hint) => (int)(hint * HintBlinkRate) % 2 == 0 ? GlowActive : GlowHintLow;

    /// <summary>
    /// Stops the game when a file it requested failed to load, naming the first one in request order. The game needs every
    /// file: the platform has already retried the download, so a failure means the player's network or the build is
    /// broken, and stopping shows the error text and reports it, where going on would show a scene with a missing layer.
    /// </summary>
    private void ThrowIfAnyAssetFailed()
    {
        // Loaded images are released, so only the ones still waiting are checked.
        if (!HasBackground)
        {
            ThrowIfFailed(_benchImage);
        }

        if (!HasStreet)
        {
            ThrowIfFailed(_streetImage);
        }

        for (var i = 0; i < _customerImages.Length; i++)
        {
            if (!_hasCustomerSheet[i])
            {
                ThrowIfFailed(_customerImages[i]);
            }
        }

        if (!_hasOrders)
        {
            ThrowIfFailed(_ordersHandle);
        }
    }

    private void ThrowIfFailed<T>(AssetHandle<T> handle)
        where T : class
    {
        if (_assets!.GetError(handle) is { } error)
        {
            throw new InvalidOperationException($"{error.AssetPath} did not load.", error);
        }
    }

    /// <summary>
    /// Opens the shop once <c>orders.json</c> is loaded. A broken file fails to load, which stops the game in
    /// <see cref="Update"/> with its problems, so they cannot go unnoticed.
    /// </summary>
    private void TakeOrders()
    {
        if (_assets!.TryGet(_ordersHandle, out var orders))
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
        _rotations = new float[count];
        _glowAlphas = new byte[count];
        _hints = new float[count];
        _dirtFades = new float[count];
        _dirtTextures = new Texture[count];
        _hasDirtTexture = new bool[count];
        _brushHint = 0;
        _oilCanHint = 0;
        _trayHint = 0;
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
            _oilCanDrawPosition = bench.OilCan.Position;
        }

        SortParts(bench);
    }

    /// <summary>
    /// Lists every part of the revolver that has a place on the bench, back to front, with its index in
    /// <paramref name="bench"/> or -1 when the order keeps it fixed.
    /// </summary>
    private void SortParts(OrderBench? bench)
    {
        var layouts = new List<PartLayout>(RevolverParts.All.Count);
        var parts = new List<int>(RevolverParts.All.Count);
        foreach (var layer in Enum.GetValues<PartLayer>())
        {
            foreach (var id in RevolverParts.All)
            {
                if (BenchLayout.TryGetPart(id, out var layout) && layout.Layer == layer)
                {
                    layouts.Add(layout);
                    parts.Add(bench?.IndexOf(id) ?? -1);
                }
            }
        }

        _drawLayouts = [.. layouts];
        _drawParts = [.. parts];
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

    /// <summary>
    /// Draws the customer: over the street and under the room when <paramref name="outsidePass"/> is set (only a
    /// customer waiting outside is drawn then), over the room otherwise (everything else of the customer, and the
    /// hand and the bubble of any customer).
    /// </summary>
    private void DrawCustomer(SpriteBatch batch, Shop shop, Order order, bool outsidePass)
    {
        var look = order.Customer.Look;
        var bottom = new Vector2(MathF.Round(shop.CustomerX), Shop.CustomerBottom);
        var sheet = CustomerSheetIndex(look);
        var hasSheet = sheet >= 0 && _hasCustomerSheet[sheet];
        var customerScale = CustomerScale;
        var isOutside = false;
        var isFixed = false;
        if (hasSheet && CustomerSeats.TryGetFixedPlace(look, out var place))
        {
            // A parcel: it lies at its place in every pose.
            bottom = place.Bottom;
            customerScale = place.Scale;
            isFixed = true;
        }
        else if (hasSheet && shop.Pose == CustomerPose.Waiting && CustomerSeats.TryGetWaitingSeat(look, out var seat))
        {
            // Their waiting frame sits down or stands in the street: they wait there, not at the counter.
            bottom = seat.Bottom;
            customerScale = seat.Scale;
            isOutside = seat.IsOutside;
        }

        if (outsidePass && !isOutside)
        {
            return;
        }

        var scale = new Vector2(customerScale);
        var bust = new SpriteOptions { Origin = PlaceholderCustomers.BustOrigin, Scale = scale };
        var glow = new SpriteOptions { Origin = PlaceholderCustomers.BustOrigin + Vector2.One, Scale = scale, Tint = Color.White.WithAlpha(_customerGlow) };
        if (isOutside)
        {
            // Seen through the window: the frame goes under the room, the bubble over it in the other pass.
            if (outsidePass)
            {
                batch.Draw(_customerSheets[sheet], CustomerSheet.Frame(shop.Pose), bottom, bust with { Tint = OutsideTint });
                return;
            }
        }
        else if (hasSheet)
        {
            // A drawn customer: the frame of their pose.
            if (_customerGlow > 0)
            {
                batch.Draw(_customerGlows[sheet], CustomerSheet.Glow(shop.Pose), bottom, glow);
            }

            batch.Draw(_customerSheets[sheet], CustomerSheet.Frame(shop.Pose), bottom, bust);
        }
        else
        {
            if (_customerGlow > 0)
            {
                batch.Draw(_customers, PlaceholderCustomers.BustGlow(look), bottom, glow);
            }

            // The face sits on the placeholder bust, scaled with it from the bust's bottom middle.
            batch.Draw(_customers, PlaceholderCustomers.Bust(look), bottom, bust);
            var face = bottom + ((PlaceholderCustomers.FaceOffset - PlaceholderCustomers.BustOrigin) * CustomerScale);
            batch.Draw(_customers, PlaceholderCustomers.Face(shop.Expression), face, new SpriteOptions { Scale = new Vector2(CustomerScale) });
        }

        if (shop.IsHandOut && !isFixed)
        {
            batch.Draw(_customers, PlaceholderCustomers.Hand, bottom + s_handOffset, new SpriteOptions { Tint = PlaceholderCustomers.Skin(look) });
        }

        if (shop.Phase == ShopPhase.Working)
        {
            // Next to the head, wherever the customer waits and however large they are drawn there.
            var bubble = bottom + (s_bubbleOffset * (customerScale / CustomerScale));
            batch.Draw(_customers, PlaceholderCustomers.Bubble, bubble);
            batch.Draw(_customers, PlaceholderCustomers.Icon(order.Symptom), bubble + PlaceholderCustomers.IconInBubble);
        }
    }

    private static int CustomerSheetIndex(string look)
    {
        for (var i = 0; i < AssetPaths.CustomerSheets.Count; i++)
        {
            if (string.Equals(AssetPaths.CustomerSheets[i].Key, look, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void DrawTurning(SpriteBatch batch, IntRect sprite, IntRect glow, Vector2 origin, Vector2 position, float angle, byte glowAlpha)
    {
        var glowOptions = new SpriteOptions { Origin = origin + Vector2.One, Rotation = angle, Tint = Color.White.WithAlpha(glowAlpha) };
        batch.Draw(_parts, glow, position, glowOptions);
        batch.Draw(_parts, sprite, position, new SpriteOptions { Origin = origin, Rotation = angle });
    }

    /// <summary>
    /// Draws the parts in the gun whose layer lies from <paramref name="first"/> to <paramref name="last"/>: the
    /// order's parts where they are, the fixed ones in their seats. Parts loose on the mat are drawn later, on top.
    /// </summary>
    private void DrawLayers(SpriteBatch batch, OrderBench bench, PartLayer first, PartLayer last)
    {
        for (var k = 0; k < _drawLayouts.Length; k++)
        {
            var layout = _drawLayouts[k];
            var part = _drawParts[k];
            if (layout.Layer < first || layout.Layer > last || (part >= 0 && bench.Gun[part].IsFree))
            {
                continue;
            }

            if (layout.Layer == PartLayer.Hammer)
            {
                DrawHammer(batch, bench, part);
            }
            else if (part >= 0)
            {
                DrawPart(batch, bench, part);
            }
            else
            {
                batch.Draw(_parts, SpriteOf(layout, broken: false), layout.Seat + _gunOffset, new SpriteOptions { Origin = layout.Origin });
            }
        }
    }

    /// <summary>
    /// Draws the hammer on its pivot or lifting off it, cocked as the test has it: the fixed one, or the order's
    /// <paramref name="part"/> while it is not loose on the mat.
    /// </summary>
    private void DrawHammer(SpriteBatch batch, OrderBench bench, int part)
    {
        if (part < 0)
        {
            DrawTurning(batch, PlaceholderParts.Hammer, PlaceholderParts.HammerGlow, PlaceholderParts.HammerOrigin, BenchLayout.HammerPivot + _gunOffset, _hammerAngle, _hammerGlow);
            return;
        }

        // It glows for the test or as a part that can come out, whichever is brighter.
        var position = _drawPositions[part] + (bench.Gun[part].Stop == PartStop.Start ? _gunOffset : Vector2.Zero);
        var glow = Math.Max(_hammerGlow, _glowAlphas[part]);
        DrawTurning(batch, PlaceholderParts.Hammer, PlaceholderParts.HammerGlow, PlaceholderParts.HammerOrigin, position, _rotations[part], glow);
        DrawDirt(batch, part, position, PlaceholderParts.HammerOrigin, _rotations[part]);
    }

    private void DrawPart(SpriteBatch batch, OrderBench bench, int part)
    {
        var layout = bench.LayoutAt(part);
        var sprite = SpriteOf(layout, bench.IsBroken(part));

        // A part in the gun moves with it; one out on the mat stays where it lies.
        var position = _drawPositions[part] + (bench.Gun[part].Stop == PartStop.Start ? _gunOffset : Vector2.Zero);
        var rotation = _rotations[part];
        var glow = new SpriteOptions { Origin = layout.Origin + Vector2.One, Rotation = rotation, Tint = Color.White.WithAlpha(_glowAlphas[part]) };
        batch.Draw(_parts, layout.Glow, position, glow);
        batch.Draw(_parts, sprite, position, new SpriteOptions { Origin = layout.Origin, Rotation = rotation });
        DrawDirt(batch, part, position, layout.Origin, rotation);
    }

    /// <summary>
    /// The sprite <paramref name="layout"/>'s part is drawn with now: the cylinder shows which chamber the last cock
    /// turned it to, a broken part shows the break.
    /// </summary>
    private IntRect SpriteOf(PartLayout layout, bool broken)
    {
        if (layout.TurnedSprite is { } turned && Shop!.Test is { Chamber: var chamber } && chamber % 2 == 1)
        {
            return turned;
        }

        return broken && layout.BrokenSprite is { } brokenSprite ? brokenSprite : layout.Sprite;
    }

    private void DrawDirt(SpriteBatch batch, int part, Vector2 position, Vector2 origin, float rotation)
    {
        if (_hasDirtTexture[part] && _dirtFades[part] > 0)
        {
            var dirt = new SpriteOptions { Origin = origin, Rotation = rotation, Tint = Color.White.WithAlpha((byte)(255 * _dirtFades[part])) };
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

            if ((events[i] & DragEvents.DockRefused) != DragEvents.None)
            {
                // It does not fit yet: a dirty part flashes with the brush, a dry one with the oil can, a broken one
                // with the tray, and a held-back one flashes what holds it.
                if (bench.DirtAt(i) is { IsClean: false })
                {
                    _hints[i] = HintSeconds;
                    _brushHint = HintSeconds;
                }
                else if (bench.NeedsOil(i))
                {
                    _hints[i] = HintSeconds;
                    _oilCanHint = HintSeconds;
                }
                else if (bench.IsBroken(i))
                {
                    _hints[i] = HintSeconds;
                    _trayHint = HintSeconds;
                }
                else
                {
                    Hint(bench, i);
                }
            }

            var layout = bench.LayoutAt(i);
            PlayClick(layout.ClickPitch, events[i]);
            if (layout.Path.IsTurn)
            {
                TickScrew(bench.Gun[i], layout.ClickPitch);
            }
        }

        if ((bench.Events & BenchEvents.HammerFirst) != BenchEvents.None)
        {
            // Nothing comes out with the hammer down: it flashes.
            _hammerHint = HintSeconds;
            PlaySound(s_refusedClick);
        }

        if ((bench.Events & BenchEvents.Cleaned) != BenchEvents.None)
        {
            // The last dirt fades out with a glint and a chime.
            _hints[bench.EventPart] = HintSeconds;
            PlaySound(s_cleanChime);
        }

        if ((bench.Events & BenchEvents.Oiled) != BenchEvents.None)
        {
            // A drop from the spout, and the part glints.
            _hints[bench.EventPart] = HintSeconds;
            PlaySound(s_oilDrop);
        }

        if ((bench.Events & BenchEvents.Replaced) != BenchEvents.None)
        {
            // The broken part goes into the tray, the spare comes out.
            _hints[bench.EventPart] = HintSeconds;
            _trayHint = HintSeconds;
            PlaySound(s_spareSwapped);
        }

        UseBrush(bench.BrushEvents);
        if ((bench.OilCanEvents & BrushEvents.PickedUp) != BrushEvents.None)
        {
            PlaySound(s_oilCanPickUp);
        }

        if ((bench.OilCanEvents & BrushEvents.PutDown) != BrushEvents.None)
        {
            PlaySound(s_oilCanLand);
        }
    }

    /// <summary>
    /// A soft tick for every quarter turn a screw made in the last step, in or out.
    /// </summary>
    private void TickScrew(PartDrag screw, float pitch)
    {
        if (screw.IsFree)
        {
            return;
        }

        var quarter = MathF.PI / 2 * screw.Path.TurnRadius;
        if (MathF.Floor(screw.Distance / quarter) != MathF.Floor(screw.PreviousDistance / quarter))
        {
            PlaySound(new SoundOptions { Pitch = 1.4f * pitch, Volume = ScrewTickVolume });
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
            // Single action, and only from full cock: the hammer comes first, and it flashes.
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

        if ((events & ShopEvents.GunThrown) != ShopEvents.None)
        {
            PlaySound(s_gunThrown);
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

        if ((events & BrushEvents.PutDown) != BrushEvents.None)
        {
            PlaySound(s_brushLand);
            if (_hasScrubVoice)
            {
                _audio!.Stop(_scrubVoice, fadeOutSeconds: 0.06f);
                _hasScrubVoice = false;
            }
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
        else if ((events & DragEvents.Docked) != DragEvents.None)
        {
            PlaySound(new SoundOptions { Pitch = 1.05f * pitch, Volume = 0.35f });
        }
        else if ((events & (DragEvents.Refused | DragEvents.DockRefused)) != DragEvents.None)
        {
            PlaySound(s_refusedClick);
        }
    }
}
