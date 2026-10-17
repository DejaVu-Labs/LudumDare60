using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Tests;

/// <summary>
/// Keeps running until <see cref="Stop"/> or <see cref="Fail"/> is called.
/// </summary>
internal sealed class ControlledPlatform : IPlatform
{
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task RunAsync(IFrameSink frameSink) => _stopped.Task;

    public void Stop() => _stopped.SetResult();

    public void Fail(Exception exception) => _stopped.SetException(exception);
}
