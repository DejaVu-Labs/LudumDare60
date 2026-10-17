namespace LudumDare60.Engine.Platform.Browser.Tests;

public sealed class BrowserAssetSourceTests
{
    private readonly List<(int Id, string Url, bool NoCache)> _fetches = [];
    private readonly List<(int Id, int Length)> _takes = [];
    private readonly Dictionary<string, byte[]> _files = [];

    private BrowserAssetSource CreateSource(string root = "assets/", string? version = null, bool noCache = false)
        => new(root, version, noCache, Fetch, Take);

    private Task<int> Fetch(int id, string url, bool noCache)
    {
        _fetches.Add((id, url, noCache));
        return Task.FromResult(_files.TryGetValue(url, out var data) ? data.Length : -404);
    }

    private void Take(int id, Span<byte> destination)
    {
        _takes.Add((id, destination.Length));
        var url = _fetches.Single(fetch => fetch.Id == id).Url;
        _files[url].CopyTo(destination);
    }

    [Theory]
    [InlineData("assets/", "bench.png", null, "assets/bench.png")]
    [InlineData("assets", "bench.png", null, "assets/bench.png")]
    [InlineData("", "bench.png", null, "bench.png")]
    [InlineData("./assets/", "sounds/click.ogg", "", "./assets/sounds/click.ogg")]
    [InlineData("assets/", "sounds/click.ogg", "77af928d", "assets/sounds/click.ogg?v=77af928d")]
    [InlineData("assets/", "a.json", "1.0+abc def", "assets/a.json?v=1.0%2Babc%20def")]
    public void GetUrl_JoinsRootPathAndVersion(string root, string path, string? version, string expected)
        => Assert.Equal(expected, BrowserAssetSource.GetUrl(root, path, version));

    [Fact]
    public async Task ReadAsync_Found_TakesBytesOfTheSameRead()
    {
        _files["assets/a.bin"] = [1, 2, 3];

        var data = await CreateSource().ReadAsync("a.bin");

        Assert.Equal([1, 2, 3], data);
        Assert.Equal([(1, "assets/a.bin", false)], _fetches);
        Assert.Equal([(1, 3)], _takes);
    }

    [Fact]
    public async Task ReadAsync_EmptyFile_StillTakesIt()
    {
        _files["assets/empty.txt"] = [];

        var data = await CreateSource().ReadAsync("empty.txt");

        Assert.Empty(data);
        Assert.Equal([(1, 0)], _takes);
    }

    [Fact]
    public async Task ReadAsync_PassesVersionAndNoCache()
    {
        _files["assets/a.bin?v=abc"] = [7];

        await CreateSource(version: "abc", noCache: true).ReadAsync("a.bin");

        Assert.Equal([(1, "assets/a.bin?v=abc", true)], _fetches);
    }

    [Fact]
    public async Task ReadAsync_EachRead_GetsNewId()
    {
        _files["assets/a.bin"] = [1];
        _files["assets/b.bin"] = [2];
        var source = CreateSource();

        await Task.WhenAll(source.ReadAsync("a.bin"), source.ReadAsync("b.bin"), source.ReadAsync("a.bin"));

        Assert.Equal([1, 2, 3], _fetches.Select(static fetch => fetch.Id));
    }

    [Fact]
    public async Task ReadAsync_HttpErrorStatus_ThrowsWithPathAndStatusWithoutTaking()
    {
        var error = await Assert.ThrowsAsync<AssetLoadException>(() => CreateSource().ReadAsync("missing.png"));

        Assert.Equal("missing.png", error.AssetPath);
        Assert.Equal(404, error.StatusCode);
        Assert.Empty(_takes);
    }

    [Fact]
    public async Task ReadAsync_NetworkError_WrapsItWithPath()
    {
        var failure = new InvalidOperationException("Failed to fetch");
        var source = new BrowserAssetSource("assets/", null, false, (_, _, _) => Task.FromException<int>(failure), Take);

        var error = await Assert.ThrowsAsync<AssetLoadException>(() => source.ReadAsync("a.bin"));

        Assert.Equal("a.bin", error.AssetPath);
        Assert.Equal(0, error.StatusCode);
        Assert.Same(failure, error.InnerException);
    }
}
