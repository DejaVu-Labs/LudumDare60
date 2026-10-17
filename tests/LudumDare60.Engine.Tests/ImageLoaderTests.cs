using LudumDare60.Engine.Assets;

namespace LudumDare60.Engine.Tests;

public sealed class ImageLoaderTests
{
    private const string Path = "sprites/revolver.png";

    private readonly ImageLoader _loader = new();

    [Fact]
    public void Load_RgbaPng_ReturnsPixelsRowByRowFromTop()
    {
        byte[] samples =
        [
            255, 0, 0, 255, /**/ 0, 255, 0, 128, /**/ 0, 0, 255, 0,
            10, 20, 30, 40, /**/ 50, 60, 70, 80, /**/ 90, 100, 110, 120,
        ];

        var image = _loader.Load(Path, TestPng.Encode(3, 2, TestPng.Rgba, samples));

        Assert.Equal(3, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(samples, image.Pixels.ToArray());
    }

    [Fact]
    public void Load_RgbPng_AddsOpaqueAlpha()
    {
        var image = _loader.Load(Path, TestPng.Encode(2, 1, TestPng.Rgb, [1, 2, 3, 4, 5, 6]));

        Assert.Equal([1, 2, 3, 255, 4, 5, 6, 255], image.Pixels.ToArray());
    }

    [Fact]
    public void Load_GrayPng_CopiesGrayToEveryColorChannel()
    {
        var image = _loader.Load(Path, TestPng.Encode(2, 1, TestPng.Gray, [0, 200]));

        Assert.Equal([0, 0, 0, 255, 200, 200, 200, 255], image.Pixels.ToArray());
    }

    [Fact]
    public void Load_GrayAlphaPng_KeepsAlpha()
    {
        var image = _loader.Load(Path, TestPng.Encode(1, 1, TestPng.GrayAlpha, [7, 9]));

        Assert.Equal([7, 7, 7, 9], image.Pixels.ToArray());
    }

    [Fact]
    public void Load_TransparentPixel_IsNotPremultiplied()
    {
        var image = _loader.Load(Path, TestPng.Encode(1, 1, TestPng.Rgba, [200, 100, 50, 0]));

        Assert.Equal([200, 100, 50, 0], image.Pixels.ToArray());
    }

    [Fact]
    public void Load_EmptyFile_Throws()
        => Assert.Throws<InvalidOperationException>(() => _loader.Load(Path, []));

    [Fact]
    public void Load_NotAnImage_Throws()
        => Assert.Throws<InvalidOperationException>(() => _loader.Load(Path, "not an image"u8.ToArray()));

    [Fact]
    public void Load_TruncatedPng_Throws()
    {
        var png = TestPng.Encode(4, 4, TestPng.Rgba, new byte[4 * 4 * 4]);

        Assert.Throws<InvalidOperationException>(() => _loader.Load(Path, png[..^20]));
    }

    [Fact]
    public void Load_NullData_Throws() => Assert.Throws<ArgumentNullException>(() => _loader.Load(Path, null!));

    [Fact]
    public void Request_PngThroughAssetManager_IsLoaded()
    {
        var source = new FakeAssetSource();
        source.Files[Path] = TestPng.Encode(1, 1, TestPng.Rgba, [1, 2, 3, 4]);
        var assets = new AssetManager(source);
        assets.Register(_loader);

        var handle = assets.Request<Image>(Path);
        assets.Update();

        Assert.Equal(AssetState.Loaded, assets.GetState(handle));
        Assert.Equal([1, 2, 3, 4], assets.Get(handle).Pixels.ToArray());
    }

    [Fact]
    public void Request_CorruptFileThroughAssetManager_FailsWithPath()
    {
        var source = new FakeAssetSource();
        source.Files[Path] = "not an image"u8.ToArray();
        var assets = new AssetManager(source);
        assets.Register(_loader);

        var handle = assets.Request<Image>(Path);
        assets.Update();

        Assert.Equal(AssetState.Failed, assets.GetState(handle));
        var error = assets.GetError(handle);
        Assert.NotNull(error);
        Assert.Equal(Path, error.AssetPath);
        Assert.Contains(Path, error.Message, StringComparison.Ordinal);
    }
}
