using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Engine.Tests;

/// <summary>
/// One scripted platform frame: its delta time and the input events delivered with it.
/// </summary>
internal sealed record ScriptedFrame(double DeltaTime, RawInputEvent[] Events);

/// <summary>
/// Produces one frame (<see cref="IFrameSink.Update"/> then <see cref="IFrameSink.Render"/>) per scripted frame, then
/// stops.
/// </summary>
internal sealed class ScriptedPlatform : IPlatform
{
    private readonly ScriptedFrame[] _frames;

    public IAssetSource Assets { get; init; } = new FakeAssetSource();

    public FakeGraphicsBackend Backend { get; } = new();

    public IGraphicsBackend Graphics => Backend;

    public FakeAudioBackend AudioBackend { get; } = new();

    public IAudioBackend Audio => AudioBackend;

    /// <summary>
    /// One frame per delta time, without input.
    /// </summary>
    public ScriptedPlatform(params double[] deltaTimes)
        : this(Array.ConvertAll(deltaTimes, static dt => new ScriptedFrame(dt, [])))
    {
    }

    private ScriptedPlatform(ScriptedFrame[] frames) => _frames = frames;

    public IFrameSink? FrameSink { get; private set; }

    public static ScriptedPlatform FromFrames(params ScriptedFrame[] frames) => new(frames);

    public Task RunAsync(IFrameSink frameSink)
    {
        FrameSink = frameSink;
        foreach (var frame in _frames)
        {
            frameSink.Update(frame.DeltaTime, frame.Events);
            frameSink.Render();
        }

        return Task.CompletedTask;
    }
}
