using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Tests;

public sealed class AssetManagerTests
{
    private const string Path = "levels/bench.txt";

    private readonly FakeAssetSource _source = new();
    private readonly TextLoader _loader = new();
    private readonly AssetManager _assets;

    public AssetManagerTests()
    {
        _assets = new AssetManager(_source);
        _assets.Register(_loader);
    }

    private static byte[] Utf8(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    private AssetHandle<TextAsset> RequestLoaded(string path, string text)
    {
        var handle = _assets.Request<TextAsset>(path);
        _source.Complete(path, Utf8(text));
        _assets.Update();
        return handle;
    }

    [Fact]
    public void Constructor_NullSource_Throws() => Assert.Throws<ArgumentNullException>(() => new AssetManager(null!));

    [Fact]
    public void Request_StartsReadAndIsLoading()
    {
        var handle = _assets.Request<TextAsset>(Path);

        Assert.Equal([Path], _source.Reads);
        Assert.Equal(AssetState.Loading, _assets.GetState(handle));
        Assert.False(_assets.TryGet(handle, out var asset));
        Assert.Null(asset);
        Assert.Null(_assets.GetError(handle));
    }

    [Fact]
    public void Request_ReadFinished_StaysLoadingUntilUpdate()
    {
        var handle = _assets.Request<TextAsset>(Path);
        _source.Complete(Path, Utf8("bench"));

        Assert.Equal(AssetState.Loading, _assets.GetState(handle));
        Assert.Empty(_loader.Loaded);

        _assets.Update();

        Assert.Equal(AssetState.Loaded, _assets.GetState(handle));
        Assert.Equal(new TextAsset(Path, "bench"), _assets.Get(handle));
        Assert.True(_assets.TryGet(handle, out var asset));
        Assert.Same(_assets.Get(handle), asset);
    }

    [Fact]
    public void Request_RawBytes_ReturnsFileWithoutCopying()
    {
        var data = Utf8("raw");
        _source.Files["config.json"] = data;

        var handle = _assets.Request<byte[]>("config.json");
        _assets.Update();

        Assert.Same(data, _assets.Get(handle));
    }

    [Fact]
    public void Request_SamePathTwice_ReadsOnceAndSharesAsset()
    {
        var first = _assets.Request<TextAsset>(Path);
        var second = _assets.Request<TextAsset>(Path);
        _source.Complete(Path, Utf8("bench"));
        _assets.Update();

        Assert.Equal([Path], _source.Reads);
        Assert.Single(_loader.Loaded);
        Assert.Same(_assets.Get(first), _assets.Get(second));
    }

    [Fact]
    public void Request_AlreadyLoaded_ReturnsCachedAssetAtOnce()
    {
        var first = RequestLoaded(Path, "bench");

        var second = _assets.Request<TextAsset>(Path);

        Assert.Equal(AssetState.Loaded, _assets.GetState(second));
        Assert.Same(_assets.Get(first), _assets.Get(second));
        Assert.Single(_source.Reads);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("bench.png")]
    [InlineData("sounds/cylinder-click.ogg")]
    [InlineData("a_b/c-d/e.f.json")]
    [InlineData("0/1.bin")]
    [InlineData(".gitkeep")]
    public void AssetPath_Valid_IsAccepted(string path) => Assert.True(AssetPath.IsValid(path));

    [Theory]
    [InlineData("")]
    [InlineData("/bench.png")]
    [InlineData("bench/")]
    [InlineData("a//b.png")]
    [InlineData("../bench.png")]
    [InlineData("a/./b.png")]
    [InlineData("a/../b.png")]
    [InlineData("Bench.png")]
    [InlineData("a\\b.png")]
    [InlineData("bench.png?v=1")]
    [InlineData("bench.png#x")]
    [InlineData("my bench.png")]
    [InlineData("https://example.com/a.png")]
    [InlineData("ström.png")]
    public void Request_InvalidPath_ThrowsWithoutReading(string path)
    {
        Assert.False(AssetPath.IsValid(path));
        Assert.Throws<ArgumentException>(() => _assets.Request<TextAsset>(path));
        Assert.Empty(_source.Reads);
    }

    [Fact]
    public void Request_NullPath_Throws() => Assert.Throws<ArgumentNullException>(() => _assets.Request<TextAsset>(null!));

    [Fact]
    public void Request_TypeWithoutLoader_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _assets.Request<string>(Path));
        Assert.Empty(_source.Reads);
    }

    [Fact]
    public void Request_SamePathAsOtherType_Throws()
    {
        _assets.Request<TextAsset>(Path);

        Assert.Throws<InvalidOperationException>(() => _assets.Request<byte[]>(Path));
    }

    [Fact]
    public void Register_SameTypeTwice_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _assets.Register(new TextLoader()));
        Assert.Throws<InvalidOperationException>(() => _assets.Register(new RawBytesLoader()));
    }

    [Fact]
    public void Register_Null_Throws() => Assert.Throws<ArgumentNullException>(() => _assets.Register<TextAsset>(null!));

    [Fact]
    public void Release_LastReference_UnloadsAndMakesHandleStale()
    {
        var handle = RequestLoaded(Path, "bench");

        _assets.Release(handle);

        Assert.Equal("bench", Assert.Single(_loader.Unloaded).Text);
        Assert.Throws<InvalidOperationException>(() => _assets.GetState(handle));
        Assert.Throws<InvalidOperationException>(() => _assets.Get(handle));
        Assert.Throws<InvalidOperationException>(() => _assets.TryGet(handle, out _));
        Assert.Throws<InvalidOperationException>(() => _assets.GetError(handle));
    }

    [Fact]
    public void Release_OtherReferencesLeft_KeepsAsset()
    {
        var first = RequestLoaded(Path, "bench");
        var second = _assets.Request<TextAsset>(Path);

        _assets.Release(first);

        Assert.Empty(_loader.Unloaded);
        Assert.Equal("bench", _assets.Get(second).Text);

        _assets.Release(second);

        Assert.Single(_loader.Unloaded);
    }

    [Fact]
    public void Release_StaleHandle_Throws()
    {
        var handle = RequestLoaded(Path, "bench");
        _assets.Release(handle);

        Assert.Throws<InvalidOperationException>(() => _assets.Release(handle));
        Assert.Single(_loader.Unloaded);
    }

    [Fact]
    public void Handle_Default_IsRejected()
    {
        RequestLoaded(Path, "bench");

        Assert.Throws<InvalidOperationException>(() => _assets.Get(default(AssetHandle<TextAsset>)));
        Assert.Throws<InvalidOperationException>(() => _assets.Release(default(AssetHandle<TextAsset>)));
    }

    [Fact]
    public void Request_AfterRelease_ReadsAgainAndOldHandleStaysStale()
    {
        var old = RequestLoaded(Path, "old");
        _assets.Release(old);

        var fresh = RequestLoaded(Path, "new");

        Assert.Equal([Path, Path], _source.Reads);
        Assert.Equal("new", _assets.Get(fresh).Text);
        Assert.NotEqual(old, fresh);
        Assert.Throws<InvalidOperationException>(() => _assets.Get(old));
    }

    [Fact]
    public void Release_WhileLoading_DropsResultWithoutParsing()
    {
        var handle = _assets.Request<TextAsset>(Path);

        _assets.Release(handle);
        _source.Complete(Path, Utf8("late"));
        _assets.Update();

        Assert.Empty(_loader.Loaded);
        Assert.Empty(_loader.Unloaded);
        Assert.Equal(0, _assets.PendingCount);
    }

    [Fact]
    public void Release_WhileLoading_ThenRequestAgain_IgnoresTheOldRead()
    {
        var old = _assets.Request<TextAsset>(Path);
        _assets.Release(old);
        var fresh = _assets.Request<TextAsset>(Path);

        _source.Complete(Path, Utf8("new"));
        _assets.Update();

        Assert.Equal("new", _assets.Get(fresh).Text);
        Assert.Single(_loader.Loaded);
    }

    [Fact]
    public void Get_WhileLoading_Throws()
    {
        var handle = _assets.Request<TextAsset>(Path);

        Assert.Throws<InvalidOperationException>(() => _assets.Get(handle));
    }

    [Fact]
    public void Update_ReadFailed_CallsOnFailedOnceWithError()
    {
        var failures = new List<AssetLoadException>();
        var assets = new AssetManager(_source, failures.Add);
        assets.Register(_loader);
        var handle = assets.Request<TextAsset>(Path);
        _source.Fail(Path, new FileNotFoundException(Path));

        assets.Update();
        assets.Update();

        var failure = Assert.Single(failures);
        Assert.Same(assets.GetError(handle), failure);
        Assert.Equal(Path, failure.AssetPath);
    }

    [Fact]
    public void Update_ReadSucceeded_DoesNotCallOnFailed()
    {
        var failures = new List<AssetLoadException>();
        var assets = new AssetManager(_source, failures.Add);
        assets.Register(_loader);
        assets.Request<TextAsset>(Path);
        _source.Complete(Path, Utf8("bench"));

        assets.Update();

        Assert.Empty(failures);
    }

    [Fact]
    public void Update_ReadFailedWithAssetLoadException_KeepsIt()
    {
        var handle = _assets.Request<TextAsset>(Path);
        var failure = new AssetLoadException(Path, 404, "Not found.");

        _source.Fail(Path, failure);
        _assets.Update();

        Assert.Equal(AssetState.Failed, _assets.GetState(handle));
        Assert.Same(failure, _assets.GetError(handle));
        Assert.Equal(404, _assets.GetError(handle)!.StatusCode);
        Assert.Throws<InvalidOperationException>(() => _assets.Get(handle));
        Assert.False(_assets.TryGet(handle, out _));
    }

    [Fact]
    public void Update_ReadFailedWithOtherException_WrapsItWithPath()
    {
        var handle = _assets.Request<TextAsset>(Path);
        var failure = new IOException("disk on fire");

        _source.Fail(Path, failure);
        _assets.Update();

        var error = _assets.GetError(handle);
        Assert.NotNull(error);
        Assert.Equal(Path, error.AssetPath);
        Assert.Equal(0, error.StatusCode);
        Assert.Same(failure, error.InnerException);
    }

    [Fact]
    public void Request_SourceThrowsSynchronously_FailsInsteadOfStayingLoading()
    {
        var failure = new InvalidOperationException("source broken");
        _source.ThrowOnRead = failure;

        var handle = _assets.Request<TextAsset>(Path);
        _assets.Update();

        Assert.Equal(AssetState.Failed, _assets.GetState(handle));
        Assert.Equal(0, _assets.PendingCount);
        var error = _assets.GetError(handle);
        Assert.NotNull(error);
        Assert.Equal(Path, error.AssetPath);
        Assert.Same(failure, error.InnerException);
    }

    [Fact]
    public void Request_SourceThrewThenRecovers_RetryLoads()
    {
        _source.ThrowOnRead = new InvalidOperationException("source broken");
        var handle = _assets.Request<TextAsset>(Path);
        _assets.Update();

        _source.ThrowOnRead = null;
        _assets.Request<TextAsset>(Path);
        _source.Complete(Path, Utf8("bench"));
        _assets.Update();

        Assert.Equal("bench", _assets.Get(handle).Text);
    }

    [Fact]
    public void Update_LoaderThrows_FailsWithParserErrorInside()
    {
        var failure = new FormatException("bad header");
        _loader.OnLoad = _ => throw failure;
        var handle = _assets.Request<TextAsset>(Path);

        _source.Complete(Path, Utf8("bench"));
        _assets.Update();

        Assert.Equal(AssetState.Failed, _assets.GetState(handle));
        var error = _assets.GetError(handle);
        Assert.NotNull(error);
        Assert.Equal(Path, error.AssetPath);
        Assert.Same(failure, error.InnerException);
    }

    [Fact]
    public void Update_LoaderReturnsNull_Fails()
    {
        _loader.OnLoad = static _ => null;
        var handle = _assets.Request<TextAsset>(Path);

        _source.Complete(Path, Utf8("bench"));
        _assets.Update();

        Assert.Equal(AssetState.Failed, _assets.GetState(handle));
        Assert.Equal(Path, _assets.GetError(handle)!.AssetPath);
    }

    [Fact]
    public void Release_Failed_DoesNotUnload()
    {
        var handle = _assets.Request<TextAsset>(Path);
        _source.Fail(Path, new IOException("gone"));
        _assets.Update();

        _assets.Release(handle);

        Assert.Empty(_loader.Unloaded);
    }

    [Fact]
    public void Request_Failed_RetriesForEveryHandle()
    {
        var first = _assets.Request<TextAsset>(Path);
        _source.Fail(Path, new IOException("offline"));
        _assets.Update();

        var second = _assets.Request<TextAsset>(Path);

        Assert.Equal([Path, Path], _source.Reads);
        Assert.Equal(AssetState.Loading, _assets.GetState(first));
        Assert.Null(_assets.GetError(first));

        _source.Complete(Path, Utf8("back"));
        _assets.Update();

        Assert.Equal("back", _assets.Get(first).Text);
        Assert.Same(_assets.Get(first), _assets.Get(second));
    }

    [Fact]
    public void PendingAndFailedCount_TrackLiveAssets()
    {
        var a = _assets.Request<TextAsset>("a.txt");
        _assets.Request<TextAsset>("b.txt");
        _assets.Request<TextAsset>("c.txt");

        Assert.Equal(3, _assets.PendingCount);
        Assert.Equal(0, _assets.FailedCount);

        _source.Complete("a.txt", Utf8("a"));
        _source.Fail("b.txt", new IOException("gone"));
        _assets.Update();

        Assert.Equal(1, _assets.PendingCount);
        Assert.Equal(1, _assets.FailedCount);

        _assets.Release(a);
        _assets.UnloadAll();

        Assert.Equal(0, _assets.PendingCount);
        Assert.Equal(0, _assets.FailedCount);
    }

    [Fact]
    public void UnloadAll_UnloadsLoadedAssetsAndMakesEveryHandleStale()
    {
        var loaded = RequestLoaded("a.txt", "a");
        _assets.Request<TextAsset>("a.txt");
        var loading = _assets.Request<TextAsset>("b.txt");

        _assets.UnloadAll();
        _source.Complete("b.txt", Utf8("b"));
        _assets.Update();

        Assert.Equal("a", Assert.Single(_loader.Unloaded).Text);
        Assert.Single(_loader.Loaded);
        Assert.Throws<InvalidOperationException>(() => _assets.GetState(loaded));
        Assert.Throws<InvalidOperationException>(() => _assets.GetState(loading));
    }

    [Fact]
    public void UnloadAll_LoaderUnloadThrows_UnloadsTheRestThenThrows()
    {
        var failure = new InvalidOperationException("unload failed");
        _loader.OnUnload = asset =>
        {
            if (string.Equals(asset.Text, "a", StringComparison.Ordinal))
            {
                throw failure;
            }
        };
        var a = RequestLoaded("a.txt", "a");
        var b = RequestLoaded("b.txt", "b");

        var error = Assert.Throws<AggregateException>(_assets.UnloadAll);

        Assert.Same(failure, Assert.Single(error.InnerExceptions));
        Assert.Collection(
            _loader.Unloaded,
            static asset => Assert.Equal("a", asset.Text),
            static asset => Assert.Equal("b", asset.Text));
        Assert.Throws<InvalidOperationException>(() => _assets.GetState(a));
        Assert.Throws<InvalidOperationException>(() => _assets.GetState(b));
    }

    [Fact]
    public void Update_LoaderRequestsManyAssets_GrowsStorageSafely()
    {
        const int Extra = 40;
        _loader.OnLoad = asset =>
        {
            if (string.Equals(asset.Path, Path, StringComparison.Ordinal))
            {
                for (var i = 0; i < Extra; i++)
                {
                    _assets.Request<byte[]>($"extra/{i}.bin");
                }
            }

            return asset;
        };
        var handle = _assets.Request<TextAsset>(Path);

        _source.Complete(Path, Utf8("bench"));
        _assets.Update();

        Assert.Equal("bench", _assets.Get(handle).Text);
        Assert.Equal(Extra, _assets.PendingCount);
    }

    [Fact]
    public void Get_LoadedAsset_DoesNotAllocate()
    {
        var handle = RequestLoaded(Path, "bench");
        _assets.Get(handle);
        _assets.TryGet(handle, out _);
        _assets.GetState(handle);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            _assets.Get(handle);
            _assets.TryGet(handle, out _);
            _assets.GetState(handle);
        }

        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
