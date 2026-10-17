using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Rendering;

/// <summary>
/// Records one frame of sprites into preallocated buffers. The engine passes it to <see cref="IGame.Render"/>, then
/// submits the frame to the GPU in one call. Sprites are drawn in call order, each over the ones before it (painter's
/// order). Consecutive sprites with the same texture share one draw call, so group draws by texture where the order
/// allows it.
/// </summary>
/// <remarks>
/// Positions are in virtual pixels (the low-resolution screen, see <see cref="PixelViewport"/>), y pointing down. A
/// sprite without rotation is snapped to whole virtual pixels, so pixel art stays on the grid.
/// <para>Nothing clears the virtual screen by itself: start <see cref="IGame.Render"/> with <see cref="Clear"/>.</para>
/// </remarks>
public sealed class SpriteBatch
{
    /// <summary>
    /// Sprites per frame.
    /// </summary>
    public const int Capacity = 16384;

    /// <summary>
    /// Command bytes per frame: enough for a texture change on every sprite, plus room for <see cref="Clear"/> calls.
    /// </summary>
    internal const int CommandCapacity = (Capacity * DrawSpritesSize) + 4096;

    private const int SetSurfaceSize = 9 * sizeof(int);
    private const int ClearSize = 2 * sizeof(int);
    private const int DrawSpritesSize = 4 * sizeof(int);
    private const int PresentSize = sizeof(int);

    private readonly Graphics _graphics;
    private readonly byte[] _commands = new byte[CommandCapacity];
    private readonly SpriteInstance[] _instances = new SpriteInstance[Capacity];
    private int _commandLength;
    private int _runTexture;
    private int _runStart;
    private bool _recording;

    internal SpriteBatch(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        _graphics = graphics;
    }

    /// <summary>
    /// Sprites drawn so far this frame.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Draw calls recorded so far this frame: one per run of sprites with the same texture.
    /// </summary>
    public int DrawCallCount { get; private set; }

    /// <summary>
    /// Fills the whole virtual screen with <paramref name="color"/>, covering everything drawn before.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called outside <see cref="IGame.Render"/>, or the frame has too many
    /// commands.</exception>
    public void Clear(Color color)
    {
        EnsureRecording();
        CloseRun();
        var command = Reserve(ClearSize);
        WriteOpcode(command, RenderCommandKind.Clear);
        MemoryMarshal.Write(command[4..], in color);
    }

    /// <summary>
    /// Draws the whole <paramref name="texture"/> with its top-left corner at <paramref name="position"/>.
    /// </summary>
    /// <inheritdoc cref="Draw(Texture, IntRect, Vector2, in SpriteOptions)"/>
    public void Draw(Texture texture, Vector2 position)
        => Draw(texture, new IntRect(0, 0, texture.Width, texture.Height), position, SpriteOptions.Default);

    /// <summary>
    /// Draws the whole <paramref name="texture"/>.
    /// </summary>
    /// <inheritdoc cref="Draw(Texture, IntRect, Vector2, in SpriteOptions)"/>
    public void Draw(Texture texture, Vector2 position, in SpriteOptions options)
        => Draw(texture, new IntRect(0, 0, texture.Width, texture.Height), position, options);

    /// <summary>
    /// Draws the <paramref name="source"/> rectangle of <paramref name="texture"/> with its top-left corner at
    /// <paramref name="position"/>.
    /// </summary>
    /// <inheritdoc cref="Draw(Texture, IntRect, Vector2, in SpriteOptions)"/>
    public void Draw(Texture texture, IntRect source, Vector2 position)
        => Draw(texture, source, position, SpriteOptions.Default);

    /// <summary>
    /// Draws the <paramref name="source"/> rectangle of <paramref name="texture"/>, for example one frame of a sprite
    /// sheet.
    /// </summary>
    /// <param name="texture">A live texture.</param>
    /// <param name="source">Texels to draw, inside the texture, at least 1x1.</param>
    /// <param name="position">Where <see cref="SpriteOptions.Origin"/> lands, in virtual pixels.</param>
    /// <param name="options">Origin, scale, rotation and tint.</param>
    /// <exception cref="InvalidOperationException">Called outside <see cref="IGame.Render"/>, the texture is stale, or
    /// the frame already holds <see cref="Capacity"/> sprites.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is empty or reaches outside the
    /// texture.</exception>
    public void Draw(Texture texture, IntRect source, Vector2 position, in SpriteOptions options)
    {
        EnsureRecording();
        _graphics.Validate(texture);
        if (source.Width < 1 || source.Height < 1 || source.X < 0 || source.Y < 0
            || source.Right > texture.Width || source.Bottom > texture.Height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source), source, "The source rectangle must be non-empty and inside the texture.");
        }

        if (Count == Capacity)
        {
            throw new InvalidOperationException("The frame already holds SpriteBatch.Capacity sprites.");
        }

        if (texture.Id != _runTexture)
        {
            CloseRun();
            _runTexture = texture.Id;
        }

        // The texture size is at most Graphics.MaxTextureSize, so the checked rectangle fits in ushort.
        _instances[Count++] = new SpriteInstance(
            position,
            options.Origin,
            options.Scale,
            options.Rotation,
            options.Tint,
            (ushort)source.X,
            (ushort)source.Y,
            (ushort)source.Width,
            (ushort)source.Height);
    }

    /// <summary>
    /// Starts a frame: forgets the previous one and records the surface.
    /// </summary>
    internal void Begin(in PixelViewport viewport)
    {
        _commandLength = 0;
        Count = 0;
        _runStart = 0;
        _runTexture = -1;
        DrawCallCount = 0;
        _recording = true;

        var command = Reserve(SetSurfaceSize);
        WriteOpcode(command, RenderCommandKind.SetSurface);
        WriteInt(command, 1, viewport.SurfaceWidth);
        WriteInt(command, 2, viewport.SurfaceHeight);
        WriteInt(command, 3, viewport.VirtualWidth);
        WriteInt(command, 4, viewport.VirtualHeight);
        WriteInt(command, 5, viewport.X);
        WriteInt(command, 6, viewport.Y);
        WriteInt(command, 7, viewport.Width);
        WriteInt(command, 8, viewport.Height);
    }

    /// <summary>
    /// Ends the frame and submits it to the GPU.
    /// </summary>
    internal void End()
    {
        EnsureRecording();
        CloseRun();
        WriteOpcode(Reserve(PresentSize), RenderCommandKind.Present);
        _recording = false;
        _graphics.Backend.Submit(_commands.AsSpan(0, _commandLength), _instances.AsSpan(0, Count));
    }

    private void CloseRun()
    {
        if (Count > _runStart)
        {
            var command = Reserve(DrawSpritesSize);
            WriteOpcode(command, RenderCommandKind.DrawSprites);
            WriteInt(command, 1, _runTexture);
            WriteInt(command, 2, _runStart);
            WriteInt(command, 3, Count - _runStart);
            DrawCallCount++;
        }

        _runStart = Count;
        _runTexture = -1;
    }

    private Span<byte> Reserve(int size)
    {
        if (_commandLength + size > CommandCapacity)
        {
            throw new InvalidOperationException("The frame has too many render commands: too many Clear calls?");
        }

        var command = _commands.AsSpan(_commandLength, size);
        _commandLength += size;
        return command;
    }

    private void EnsureRecording()
    {
        if (!_recording)
        {
            throw new InvalidOperationException("Sprites can be drawn only inside IGame.Render.");
        }
    }

    private static void WriteOpcode(Span<byte> command, RenderCommandKind kind)
        => BinaryPrimitives.WriteInt32LittleEndian(command, (int)kind);

    private static void WriteInt(Span<byte> command, int index, int value)
        => BinaryPrimitives.WriteInt32LittleEndian(command[(index * sizeof(int))..], value);
}
