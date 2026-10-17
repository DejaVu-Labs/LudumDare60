using LudumDare60.Engine.Platform;

namespace LudumDare60.Engine.Tests;

/// <summary>
/// Produces one frame (<see cref="IFrameSink.Update"/> then <see cref="IFrameSink.Render"/>) per scripted delta time,
/// then stops.
/// </summary>
internal sealed class ScriptedPlatform(params double[] deltaTimes) : IPlatform
{
    public IFrameSink? FrameSink { get; private set; }

    public Task RunAsync(IFrameSink frameSink)
    {
        FrameSink = frameSink;
        foreach (var dt in deltaTimes)
        {
            frameSink.Update(dt);
            frameSink.Render();
        }

        return Task.CompletedTask;
    }
}
