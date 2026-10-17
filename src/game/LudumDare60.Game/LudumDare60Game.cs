using System.Numerics;
using LudumDare60.Engine;
using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Input;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;

namespace LudumDare60.Game;

/// <summary>
/// The Ludum Dare 60 game. For now the bench background (the cutting mat color until it loads) under a placeholder
/// scene that exercises the renderer and input: a sprite that follows the pointer and grows while the primary button
/// is down, a rotating tinted one and a flipped, scaled one. The placeholders go away once the real art arrives.
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

    private static readonly Color s_brassTint = Color.FromRgb(0xd9ab45).WithAlpha(200);

    private AssetManager? _assets;
    private Graphics? _graphics;
    private AssetHandle<Image> _benchImage;
    private Texture _bench;
    private Texture _placeholder;
    private bool _hasPlaceholder;
    private float _time;

    /// <summary>
    /// The pointer position in virtual pixels, as of the last frame.
    /// </summary>
    public Vector2 PointerPosition { get; private set; } = new(VirtualWidth / 2, VirtualHeight / 2);

    /// <summary>
    /// Whether the primary pointer button was down in the last frame.
    /// </summary>
    public bool IsHolding { get; private set; }

    /// <summary>
    /// Whether the bench background is loaded and drawn.
    /// </summary>
    public bool HasBackground { get; private set; }

    public void Initialize(AssetManager assets, Graphics graphics, Audio audio)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = assets;
        _graphics = graphics;
        assets.Register(new ImageLoader());
        _benchImage = assets.Request<Image>(AssetPaths.Bench);
    }

    public void FixedUpdate(in FrameContext context)
    {
    }

    public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
    {
        if (!_hasPlaceholder)
        {
            // Textures can be created from the first frame on.
            _placeholder = _graphics!.CreateTexture(PlaceholderSprite.Create());
            _hasPlaceholder = true;
        }

        if (!HasBackground && _assets!.TryGet(_benchImage, out var bench))
        {
            // The texture keeps no copy of the pixels, and nothing reads them back, so the image can go.
            _bench = _graphics!.CreateTexture(bench);
            _assets.Release(_benchImage);
            HasBackground = true;
        }

        PointerPosition = context.Input.PointerPosition;
        IsHolding = context.Input.IsDown(PointerButton.Primary);
        _time = (float)context.TotalTime;
    }

    public void PrepareRender(double alpha)
    {
    }

    public void Render(SpriteBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        batch.Clear(MatColor);
        if (HasBackground)
        {
            batch.Draw(_bench, Vector2.Zero);
        }

        if (!_hasPlaceholder)
        {
            return;
        }

        var center = new Vector2(PlaceholderSprite.Size / 2);
        batch.Draw(_placeholder, new Vector2(200, 180), new SpriteOptions { Origin = center, Rotation = _time, Tint = s_brassTint });
        batch.Draw(_placeholder, new Vector2(440, 180), new SpriteOptions { Origin = center, Scale = new Vector2(-3, 3) });
        batch.Draw(_placeholder, PointerPosition, new SpriteOptions { Origin = center, Scale = new Vector2(IsHolding ? 2 : 1) });
    }
}
