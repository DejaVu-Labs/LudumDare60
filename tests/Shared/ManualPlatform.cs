using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Testing;

/// <summary>
/// A platform whose frames the test drives: start <see cref="GameEngine.RunAsync"/> with it, call
/// <see cref="Frame"/> as often as needed, then <see cref="Stop"/>. It has no asset files unless a test sets
/// <see cref="Assets"/>.
/// </summary>
internal sealed class ManualPlatform : IPlatform
{
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IFrameSink? _frameSink;

    public IAssetSource Assets { get; init; } = new NoAssets();

    public FakeGraphicsBackend Backend { get; } = new();

    public IGraphicsBackend Graphics => Backend;

    public FakeAudioBackend AudioBackend { get; } = new();

    public IAudioBackend Audio => AudioBackend;

    public Task RunAsync(IFrameSink frameSink)
    {
        _frameSink = frameSink;
        return _stopped.Task;
    }

    /// <summary>
    /// Runs one frame: <see cref="IFrameSink.Update"/>, then <see cref="IFrameSink.Render"/>.
    /// </summary>
    public void Frame(double dt, params RawInputEvent[] events)
    {
        var sink = _frameSink ?? throw new InvalidOperationException("Start GameEngine.RunAsync with this platform first.");
        sink.Update(dt, events);
        sink.Render();
    }

    public void Stop() => _stopped.SetResult();

    private sealed class NoAssets : IAssetSource
    {
        public Task<byte[]> ReadAsync(string path) => Task.FromException<byte[]>(new FileNotFoundException(path));
    }
}
