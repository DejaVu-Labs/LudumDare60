using System.Text;
using LudumDare60.Engine;
using LudumDare60.Engine.Platform;

namespace LudumDare60.Samples.InputMonitor.Tests;

public sealed class InputMonitorGameTests
{
    private readonly RecordingDisplay _display = new();
    private readonly InputMonitorGame _game;

    public InputMonitorGameTests() => _game = new InputMonitorGame(_display);

    private void Frame(params RawInputEvent[] events)
    {
        var context = new FrameContext(1.0 / 60, 0, 0);
        _game.Update(in context, events);
        _game.PrepareRender(0);
        _game.Render();
    }

    [Fact]
    public void Render_FirstFrame_ShowsReport()
    {
        Frame();

        Assert.Contains("LD60 input monitor", Assert.Single(_display.Shown), StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ShowsAgainOnlyAfterEvents()
    {
        Frame();
        Frame();
        Frame(new RawInputEvent(RawInputEventKind.KeyDown, 0, (ushort)Key.A, 0, 0, 0, 0, 0));
        Frame();

        Assert.Equal(2, _display.Shown.Count);
        Assert.Contains("Keys       A \n", _display.Shown[1], StringComparison.Ordinal);
        Assert.True(_game.Tracker.IsKeyDown(Key.A));
    }

    private sealed class RecordingDisplay : ITextDisplay
    {
        public List<string> Shown { get; } = [];

        public void Show(ReadOnlySpan<byte> utf8) => Shown.Add(Encoding.UTF8.GetString(utf8));
    }
}
