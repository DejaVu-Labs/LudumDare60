namespace LudumDare60.Engine.Platform.Browser.Tests;

public sealed class LazyImportTests
{
    [Fact]
    public void EnsureAsync_CalledTwice_ImportsOnceAndSharesTheTask()
    {
        var imports = 0;
        var pending = new TaskCompletionSource();
        var import = new LazyImport(() =>
        {
            imports++;
            return pending.Task;
        });

        var first = import.EnsureAsync();
        var second = import.EnsureAsync();

        Assert.Equal(1, imports);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task EnsureAsync_AfterSuccess_DoesNotImportAgain()
    {
        var imports = 0;
        var import = new LazyImport(() =>
        {
            imports++;
            return Task.CompletedTask;
        });

        await import.EnsureAsync();
        await import.EnsureAsync();

        Assert.Equal(1, imports);
    }

    [Fact]
    public async Task EnsureAsync_AfterFailure_ImportsAgain()
    {
        var imports = 0;
        var import = new LazyImport(() =>
        {
            imports++;
            return imports == 1 ? Task.FromException(new InvalidOperationException("offline")) : Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(import.EnsureAsync);
        await import.EnsureAsync();

        Assert.Equal(2, imports);
    }

    [Fact]
    public async Task EnsureAsync_AfterCancellation_ImportsAgain()
    {
        var imports = 0;
        var import = new LazyImport(() =>
        {
            imports++;
            return imports == 1 ? Task.FromCanceled(new CancellationToken(canceled: true)) : Task.CompletedTask;
        });

        await Assert.ThrowsAsync<TaskCanceledException>(import.EnsureAsync);
        await import.EnsureAsync();

        Assert.Equal(2, imports);
    }
}
