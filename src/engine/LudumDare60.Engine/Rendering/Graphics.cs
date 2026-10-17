using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Rendering;

/// <summary>
/// Creates and destroys GPU textures for a run. The engine passes it to <see cref="IGame.Initialize"/>; drawing goes
/// through the <see cref="SpriteBatch"/> passed to <see cref="IGame.Render"/>.
/// </summary>
/// <remarks>
/// Textures can be created from the first frame on: the platform sets up the GPU when its frame loop starts, after
/// <see cref="IGame.Initialize"/>. Images arrive in frames anyway (see <see cref="AssetManager.Update"/>). Every texture
/// still alive when the run ends is destroyed.
/// </remarks>
public sealed class Graphics
{
    /// <summary>
    /// Largest texture side in texels. WebGL2 guarantees 2048; practically every device supports 4096.
    /// </summary>
    public const int MaxTextureSize = 4096;

    private readonly List<Slot> _slots = [];
    private readonly Stack<int> _freeIds = new();
    private readonly List<int> _pendingDestroys = [];
    private bool _started;

    internal Graphics(IGraphicsBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        Backend = backend;
    }

    /// <summary>
    /// Number of live textures.
    /// </summary>
    public int TextureCount { get; private set; }

    internal IGraphicsBackend Backend { get; }

    /// <summary>
    /// Uploads <paramref name="image"/> to a new texture. The texture keeps no reference to the image: keep the image
    /// (or its asset handle) as long as you need its pixels, for example for hit tests.
    /// </summary>
    /// <exception cref="InvalidOperationException">Called before the first frame, for example from
    /// <see cref="IGame.Initialize"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A side of the image is larger than
    /// <see cref="MaxTextureSize"/>.</exception>
    public Texture CreateTexture(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!_started)
        {
            throw new InvalidOperationException(
                "Textures can be created from the first frame on, not in Initialize: the GPU is not set up yet.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(image.Width, MaxTextureSize, nameof(image));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(image.Height, MaxTextureSize, nameof(image));

        if (!_freeIds.TryPop(out var id))
        {
            id = _slots.Count;
            _slots.Add(default);
        }

        Backend.CreateTexture(id, image.Width, image.Height, image.Pixels);

        var generation = _slots[id].Generation + 1;
        _slots[id] = new Slot(generation, Alive: true, OnGpu: true);
        TextureCount++;
        return new Texture(id, generation, image.Width, image.Height);
    }

    /// <summary>
    /// Destroys <paramref name="texture"/>. The handle is stale at once; sprites already drawn with it this frame still
    /// show, because the GPU texture is deleted after the frame is submitted.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handle is stale.</exception>
    public void DestroyTexture(Texture texture)
    {
        Validate(texture);
        var id = texture.Id;
        _slots[id] = _slots[id] with { Alive = false };
        _pendingDestroys.Add(id);
        TextureCount--;
    }

    /// <summary>
    /// Whether <paramref name="texture"/> is a live texture of this run.
    /// </summary>
    public bool IsAlive(Texture texture)
    {
        var id = texture.Id;
        return (uint)id < (uint)_slots.Count && _slots[id] is { Alive: true } slot && slot.Generation == texture.Generation;
    }

    /// <exception cref="InvalidOperationException">The handle is stale.</exception>
    internal void Validate(Texture texture)
    {
        if (!IsAlive(texture))
        {
            throw new InvalidOperationException("The texture was destroyed, or belongs to an earlier run.");
        }
    }

    /// <summary>
    /// Called by the engine at the start of every frame: textures can be created from now on.
    /// </summary>
    internal void BeginFrame() => _started = true;

    /// <summary>
    /// Called by the engine after the frame is submitted: deletes the textures destroyed during the frame and frees
    /// their ids.
    /// </summary>
    internal void EndFrame()
    {
        foreach (var id in _pendingDestroys)
        {
            Backend.DestroyTexture(id);
            _slots[id] = _slots[id] with { OnGpu = false };
            _freeIds.Push(id);
        }

        _pendingDestroys.Clear();
    }

    /// <summary>
    /// Called by the engine when the run ends: deletes every texture, live or destroyed during the last frame. Every
    /// handle of the run becomes stale. A backend that throws does not stop it: the other textures are deleted too, and
    /// the failures are thrown afterwards as one <see cref="AggregateException"/>.
    /// </summary>
    internal void DestroyAll()
    {
        _started = false;
        _pendingDestroys.Clear();
        _freeIds.Clear();
        TextureCount = 0;
        List<Exception>? failures = null;
        for (var id = _slots.Count - 1; id >= 0; id--)
        {
            var slot = _slots[id];
            _slots[id] = slot with { Alive = false, OnGpu = false };
            _freeIds.Push(id);
            if (!slot.OnGpu)
            {
                continue;
            }

            try
            {
                Backend.DestroyTexture(id);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                (failures ??= []).Add(exception);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("Destroying textures failed.", failures);
        }
    }

    /// <param name="Generation">Bumped by every <see cref="CreateTexture"/> of this id; 0 for an id never used.</param>
    /// <param name="Alive">The id holds a live texture.</param>
    /// <param name="OnGpu">The backend holds a texture with this id: a live one, or one destroyed during this frame.</param>
    private readonly record struct Slot(int Generation, bool Alive, bool OnGpu);
}
