using System.Diagnostics.CodeAnalysis;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Assets;

/// <summary>
/// Loads asset files through an <see cref="IAssetSource"/>, parses them with registered loaders and caches the result
/// by path. Assets are reference counted: every <see cref="Request{T}"/> needs one <see cref="Release{T}"/>, and the
/// last release unloads the asset.
/// </summary>
/// <remarks>
/// Requests never block. Reads run in the background, and their results are applied only in <see cref="Update"/>,
/// which <see cref="GameEngine"/> calls at the start of every frame, so an asset changes state at one known point of the
/// frame. Resolving a handle (<see cref="Get{T}"/>, <see cref="TryGet{T}"/>, <see cref="GetState{T}"/>) is an array
/// index without allocations. Not thread-safe: the browser build has one thread.
/// </remarks>
public sealed class AssetManager
{
    private readonly IAssetSource _source;
    private readonly Dictionary<Type, ILoaderAdapter> _loaders = [];
    private readonly Dictionary<string, int> _slotsByPath = new(StringComparer.Ordinal);
    private readonly Stack<int> _freeSlots = new();
    private readonly List<PendingRead> _pendingReads = [];
    private Slot[] _slots = new Slot[16];
    private int _slotCount;

    /// <summary>
    /// Creates a manager with <see cref="RawBytesLoader"/> registered for <c>byte[]</c>.
    /// </summary>
    public AssetManager(IAssetSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Register(new RawBytesLoader());
    }

    /// <summary>
    /// Number of live assets in <see cref="AssetState.Loading"/>. A loading screen waits until it reaches zero.
    /// </summary>
    public int PendingCount => CountLive(AssetState.Loading);

    /// <summary>
    /// Number of live assets in <see cref="AssetState.Failed"/>.
    /// </summary>
    public int FailedCount => CountLive(AssetState.Failed);

    /// <summary>
    /// Registers the loader that parses files into <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">A loader for <typeparamref name="T"/> is already registered.</exception>
    public void Register<T>(IAssetLoader<T> loader)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(loader);
        if (!_loaders.TryAdd(typeof(T), new LoaderAdapter<T>(loader)))
        {
            throw new InvalidOperationException($"A loader for {typeof(T).Name} is already registered.");
        }
    }

    /// <summary>
    /// Requests the asset at <paramref name="path"/> and adds a reference to it. The first request starts reading the
    /// file; later requests share the cached asset, or the read in progress. A request for a failed asset retries it.
    /// </summary>
    /// <param name="path">Path relative to the asset root; see <see cref="AssetPath.IsValid"/>.</param>
    /// <returns>A handle to pass to <see cref="Get{T}"/> and, once no longer needed, to <see cref="Release{T}"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is not a valid asset path.</exception>
    /// <exception cref="InvalidOperationException">No loader is registered for <typeparamref name="T"/>, or the path is
    /// already loaded as another type.</exception>
    public AssetHandle<T> Request<T>(string path)
        where T : class
    {
        AssetPath.Validate(path);
        if (!_loaders.TryGetValue(typeof(T), out var loader))
        {
            throw new InvalidOperationException(
                $"No loader is registered for {typeof(T).Name}. Call {nameof(Register)} first.");
        }

        if (_slotsByPath.TryGetValue(path, out var index))
        {
            ref var slot = ref _slots[index];
            if (slot.Loader != loader)
            {
                throw new InvalidOperationException($"'{path}' is already requested as {slot.Loader!.AssetType.Name}.");
            }

            slot.RefCount++;
            if (slot.State == AssetState.Failed)
            {
                StartRead(index);
            }

            return new AssetHandle<T>(index, slot.Generation);
        }

        index = AllocateSlot();
        ref var created = ref _slots[index];
        created.Path = path;
        created.Loader = loader;
        created.RefCount = 1;
        _slotsByPath.Add(path, index);
        StartRead(index);
        return new AssetHandle<T>(index, created.Generation);
    }

    /// <summary>
    /// Removes the reference added by the <see cref="Request{T}"/> that returned <paramref name="handle"/>. The last
    /// release unloads the asset and makes every handle to it stale. A read still in progress finishes, and its result
    /// is dropped.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handle is stale or was never valid.</exception>
    public void Release<T>(AssetHandle<T> handle)
        where T : class
    {
        ref var slot = ref GetSlot(handle);
        slot.RefCount--;
        if (slot.RefCount == 0)
        {
            Free(handle.Slot);
        }
    }

    /// <summary>
    /// Unloads every asset, whatever its reference count. Every handle becomes stale.
    /// </summary>
    public void UnloadAll()
    {
        for (var i = 0; i < _slotCount; i++)
        {
            if (_slots[i].Path is not null)
            {
                Free(i);
            }
        }
    }

    /// <exception cref="InvalidOperationException">The handle is stale or was never valid.</exception>
    public AssetState GetState<T>(AssetHandle<T> handle)
        where T : class
        => GetSlot(handle).State;

    /// <summary>
    /// Returns the loaded asset.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handle is stale, or the asset is not
    /// <see cref="AssetState.Loaded"/>.</exception>
    public T Get<T>(AssetHandle<T> handle)
        where T : class
    {
        ref var slot = ref GetSlot(handle);
        if (slot.State != AssetState.Loaded)
        {
            throw new InvalidOperationException($"'{slot.Path}' is not loaded; its state is {slot.State}.");
        }

        return (T)slot.Asset!;
    }

    /// <summary>
    /// Returns the asset if it is loaded.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handle is stale or was never valid.</exception>
    public bool TryGet<T>(AssetHandle<T> handle, [NotNullWhen(true)] out T? asset)
        where T : class
    {
        ref var slot = ref GetSlot(handle);
        asset = slot.State == AssetState.Loaded ? (T)slot.Asset! : null;
        return asset is not null;
    }

    /// <summary>
    /// Returns why the asset failed, or <see langword="null"/> unless it is <see cref="AssetState.Failed"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The handle is stale or was never valid.</exception>
    public AssetLoadException? GetError<T>(AssetHandle<T> handle)
        where T : class
        => GetSlot(handle).Error;

    /// <summary>
    /// Applies the reads that finished since the last call: parses the files of live assets and marks failures.
    /// <see cref="GameEngine"/> calls it at the start of every frame.
    /// </summary>
    public void Update()
    {
        for (var i = _pendingReads.Count - 1; i >= 0; i--)
        {
            var read = _pendingReads[i];
            if (!read.Task.IsCompleted)
            {
                continue;
            }

            _pendingReads.RemoveAt(i);
            var slot = _slots[read.Slot];
            if (slot.Generation != read.Generation || slot.State != AssetState.Loading)
            {
                // Released or unloaded while reading; reading Exception marks a failure as observed.
                _ = read.Task.Exception;
                continue;
            }

            // A loader may request other assets and grow _slots, so no ref into the array is held across Load.
            var (asset, error) = Complete(slot.Path!, slot.Loader!, read.Task);
            ref var completed = ref _slots[read.Slot];
            if (completed.Generation != read.Generation)
            {
                // The loader released this very asset.
                if (asset is not null)
                {
                    slot.Loader!.Unload(asset);
                }

                continue;
            }

            completed.Asset = asset;
            completed.Error = error;
            completed.State = error is null ? AssetState.Loaded : AssetState.Failed;
        }
    }

    private static (object? Asset, AssetLoadException? Error) Complete(string path, ILoaderAdapter loader, Task<byte[]> read)
    {
        if (!read.IsCompletedSuccessfully)
        {
            var cause = read.Exception?.InnerException;
            return (null, cause as AssetLoadException ?? new AssetLoadException(path, 0, $"Reading '{path}' failed.", cause));
        }

        try
        {
            var asset = loader.Load(path, read.Result);
            return asset is null
                ? (null, new AssetLoadException(path, 0, $"The {loader.AssetType.Name} loader returned null for '{path}'."))
                : (asset, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return (null, new AssetLoadException(path, 0, $"Parsing '{path}' failed: {exception.Message}", exception));
        }
    }

    private void StartRead(int index)
    {
        ref var slot = ref _slots[index];
        slot.State = AssetState.Loading;
        slot.Error = null;

        Task<byte[]> read;
        try
        {
            read = _source.ReadAsync(slot.Path!);
        }
        catch (AssetLoadException exception)
        {
            read = Task.FromException<byte[]>(exception);
        }

        _pendingReads.Add(new PendingRead(index, slot.Generation, read));
    }

    private int AllocateSlot()
    {
        if (_freeSlots.TryPop(out var index))
        {
            return index;
        }

        if (_slotCount == _slots.Length)
        {
            Array.Resize(ref _slots, _slots.Length * 2);
        }

        index = _slotCount++;
        _slots[index].Generation = 1;
        return index;
    }

    private void Free(int index)
    {
        ref var slot = ref _slots[index];
        var asset = slot.Asset;
        var loader = slot.Loader!;
        var wasLoaded = slot.State == AssetState.Loaded;

        _slotsByPath.Remove(slot.Path!);
        slot = new Slot { Generation = slot.Generation + 1 };
        _freeSlots.Push(index);

        if (wasLoaded)
        {
            loader.Unload(asset!);
        }
    }

    private ref Slot GetSlot<T>(AssetHandle<T> handle)
        where T : class
    {
        if ((uint)handle.Slot < (uint)_slotCount)
        {
            ref var slot = ref _slots[handle.Slot];
            if (slot.Generation == handle.Generation && slot.Path is not null)
            {
                return ref slot;
            }
        }

        throw new InvalidOperationException("The asset handle is stale: it was released, unloaded or never requested.");
    }

    private int CountLive(AssetState state)
    {
        var count = 0;
        for (var i = 0; i < _slotCount; i++)
        {
            if (_slots[i].Path is not null && _slots[i].State == state)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// One cached asset. A free slot has no <see cref="Path"/>.
    /// </summary>
    private struct Slot
    {
        public string? Path;
        public ILoaderAdapter? Loader;
        public object? Asset;
        public AssetLoadException? Error;
        public int RefCount;
        public int Generation;
        public AssetState State;
    }

    private readonly record struct PendingRead(int Slot, int Generation, Task<byte[]> Task);

    /// <summary>
    /// Calls a typed <see cref="IAssetLoader{T}"/> for an asset stored as <see cref="object"/>.
    /// </summary>
    private interface ILoaderAdapter
    {
        Type AssetType { get; }

        object Load(string path, byte[] data);

        void Unload(object asset);
    }

    private sealed class LoaderAdapter<T>(IAssetLoader<T> loader) : ILoaderAdapter
        where T : class
    {
        public Type AssetType => typeof(T);

        public object Load(string path, byte[] data) => loader.Load(path, data);

        public void Unload(object asset) => loader.Unload((T)asset);
    }
}
