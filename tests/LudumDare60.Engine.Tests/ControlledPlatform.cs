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

    public FakeAudioBackend AudioBackend { get; } = new();

    public IAudioBackend Audio => AudioBackend;

    public Task RunAsync(IFrameSink frameSink) => _stopped.Task;

    /// <summary>
    /// Every <see cref="ReportError"/> call, in order.
    /// </summary>
    public List<(Exception Exception, bool Handled)> ReportedErrors { get; } = [];

    public void ReportError(Exception exception, bool handled) => ReportedErrors.Add((exception, handled));

    public void Stop() => _stopped.SetResult();

    public void Fail(Exception exception) => _stopped.SetException(exception);
}
