using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Engine.Tests;

/// <summary>
/// Keeps running until <see cref="Stop"/> or <see cref="Fail"/> is called.
/// </summary>
internal sealed class ControlledPlatform : IPlatform
{
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IAssetSource Assets { get; init; } = new FakeAssetSource();

    public FakeGraphicsBackend Backend { get; } = new();

    public IGraphicsBackend Graphics => Backend;

    public Task RunAsync(IFrameSink frameSink) => _stopped.Task;

    public void Stop() => _stopped.SetResult();

    public void Fail(Exception exception) => _stopped.SetException(exception);
}
