namespace LudumDare60.Engine.Platform.Browser.Tests;

/// <summary>
/// Records every <see cref="IFrameSink"/> call, in order.
/// </summary>
internal sealed class RecordingFrameSink : IFrameSink
{
    public List<string> Calls { get; } = [];

    public List<double> DeltaTimes { get; } = [];

    public void Update(double dt)
    {
        Calls.Add(nameof(Update));
        DeltaTimes.Add(dt);
    }

    public void Render() => Calls.Add(nameof(Render));
}
