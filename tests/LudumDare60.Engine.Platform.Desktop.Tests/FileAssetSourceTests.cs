namespace LudumDare60.Engine.Platform.Desktop.Tests;

public sealed class FileAssetSourceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ld60-assets-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task ReadAsync_ExistingFile_ReturnsItsBytes()
    {
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        await File.WriteAllBytesAsync(Path.Combine(_root, "data", "a.bin"), [1, 2, 3], TestContext.Current.CancellationToken);

        var data = await new FileAssetSource(_root).ReadAsync("data/a.bin");

        Assert.Equal([1, 2, 3], data);
    }

    [Theory]
    [InlineData("missing.png")]
    [InlineData("missing-folder/a.png")]
    public async Task ReadAsync_Missing_ThrowsAssetLoadExceptionWithPath(string path)
    {
        var exception = await Assert.ThrowsAsync<AssetLoadException>(() => new FileAssetSource(_root).ReadAsync(path));

        Assert.Equal(path, exception.AssetPath);
        Assert.Equal(0, exception.StatusCode);
        Assert.IsAssignableFrom<IOException>(exception.InnerException);
    }

    [Fact]
    public async Task ReadAsync_Null_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => new FileAssetSource(_root).ReadAsync(null!));
}
