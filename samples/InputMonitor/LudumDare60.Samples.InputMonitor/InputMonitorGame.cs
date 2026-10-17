using LudumDare60.Engine;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Samples.InputMonitor;

/// <summary>
/// A game that shows its own input: <see cref="InputTracker"/> state, rewritten only when an event arrives.
/// </summary>
public sealed class InputMonitorGame(ITextDisplay display) : IGame
{
    /// <summary>
    /// Report buffer size in bytes. A report that does not fit is cut off.
    /// </summary>
    public const int ReportCapacity = 4096;

    private readonly byte[] _report = new byte[ReportCapacity];
    private int _reportLength;
    private bool _changed = true;
    private bool _prepared;

    public InputTracker Tracker { get; } = new();

    public void FixedUpdate(in FrameContext context)
    {
    }

    public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
    {
        if (!events.IsEmpty)
        {
            Tracker.Apply(events, context.FrameIndex);
            _changed = true;
        }
    }

    public void PrepareRender(double alpha)
    {
        if (_changed)
        {
            _reportLength = Tracker.WriteReport(_report);
            _changed = false;
            _prepared = true;
        }
    }

    public void Render()
    {
        // One interop call per changed frame, none while nothing happens.
        if (_prepared)
        {
            display.Show(_report.AsSpan(0, _reportLength));
            _prepared = false;
        }
    }
}
