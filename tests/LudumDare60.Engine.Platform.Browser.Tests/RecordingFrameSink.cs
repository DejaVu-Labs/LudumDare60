namespace LudumDare60.Engine.Platform.Browser.Tests;

/// <summary>
/// Records every <see cref="IFrameSink"/> call, in order.
/// </summary>
/// <param name="calls">Where calls are recorded; pass a shared list to interleave them with other recorders.</param>
internal sealed class RecordingFrameSink(List<string> calls) : IFrameSink
{
    public RecordingFrameSink()
        : this([])
    {
    }

    public List<string> Calls { get; } = calls;

    public List<double> DeltaTimes { get; } = [];

    /// <summary>
    /// A copy of the events passed to each <see cref="Update"/>.
    /// </summary>
    public List<RawInputEvent[]> Events { get; } = [];

    public void Update(double dt, ReadOnlySpan<RawInputEvent> events)
    {
        Calls.Add(nameof(Update));
        DeltaTimes.Add(dt);
        Events.Add(events.ToArray());
    }

    public void Render() => Calls.Add(nameof(Render));
}
