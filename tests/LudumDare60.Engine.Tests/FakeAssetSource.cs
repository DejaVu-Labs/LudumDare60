using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Tests;

/// <summary>
/// An <see cref="IAssetSource"/> the test drives: reads of <see cref="Files"/> finish at once, other reads stay pending
/// until <see cref="Complete"/> or <see cref="Fail"/>.
/// </summary>
internal sealed class FakeAssetSource : IAssetSource
{
    private readonly Dictionary<string, TaskCompletionSource<byte[]>> _pending = [];

    /// <summary>
    /// Files whose reads finish immediately.
    /// </summary>
    public Dictionary<string, byte[]> Files { get; } = [];

    /// <summary>
    /// Every path read, in order.
    /// </summary>
    public List<string> Reads { get; } = [];

    /// <summary>
    /// When set, <see cref="ReadAsync"/> throws it at once instead of returning a task.
    /// </summary>
    public Exception? ThrowOnRead { get; set; }

    public Task<byte[]> ReadAsync(string path)
    {
        Reads.Add(path);
        if (ThrowOnRead is not null)
        {
            throw ThrowOnRead;
        }

        if (Files.TryGetValue(path, out var data))
        {
            return Task.FromResult(data);
        }

        var read = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[path] = read;
        return read.Task;
    }

    public void Complete(string path, byte[] data) => Take(path).SetResult(data);

    public void Fail(string path, Exception exception) => Take(path).SetException(exception);

    private TaskCompletionSource<byte[]> Take(string path)
    {
        Assert.True(_pending.Remove(path, out var read), $"No pending read of '{path}'.");
        return read;
    }
}
