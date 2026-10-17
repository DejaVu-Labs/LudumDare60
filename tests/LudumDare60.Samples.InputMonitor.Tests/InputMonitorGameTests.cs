using System.Text;
using LudumDare60.Engine;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Testing;

namespace LudumDare60.Samples.InputMonitor.Tests;

public sealed class InputMonitorGameTests
{
    private readonly RecordingDisplay _display = new();
    private readonly ManualPlatform _platform = new();
    private readonly InputMonitorGame _game;

    public InputMonitorGameTests()
    {
        _game = new InputMonitorGame(_display);
        _ = new GameEngine(640, 360).RunAsync(_game, _platform);
    }

    private void Frame(params RawInputEvent[] events) => _platform.Frame(1.0 / 60, events);

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

    [Fact]
    public void Render_ClearsToPageBackground()
    {
        Frame();

        var clear = _platform.Backend.Frames[0].Decode()[1];
        Assert.Equal(RenderCommandKind.Clear, clear.Kind);
        Assert.Equal(BitConverter.ToInt32([0x0b, 0x0f, 0x0b, 0xff]), clear.Arguments[0]);
    }

    private sealed class RecordingDisplay : ITextDisplay
    {
        public List<string> Shown { get; } = [];

        public void Show(ReadOnlySpan<byte> utf8) => Shown.Add(Encoding.UTF8.GetString(utf8));
    }
}
