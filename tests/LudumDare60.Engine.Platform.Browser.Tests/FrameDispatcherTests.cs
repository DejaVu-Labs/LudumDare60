using System.Runtime.InteropServices;

namespace LudumDare60.Engine.Platform.Browser.Tests;

public sealed class FrameDispatcherTests
{
    private readonly List<string> _calls = [];
    private readonly RecordingFrameSink _sink;

    public FrameDispatcherTests() => _sink = new RecordingFrameSink(_calls);

    private static int NoInput(Span<byte> destination, double frameTimeMs) => 0;

    [Fact]
    public void OnFrame_DrainsInputThenCallsUpdateThenRender()
    {
        var dispatcher = new FrameDispatcher(_sink, (_, _) =>
        {
            _calls.Add("Drain");
            return 0;
        });

        dispatcher.OnFrame(0.016, 1000);

        Assert.Equal(["Drain", "Update", "Render"], _calls);
    }

    [Fact]
    public void OnFrame_PassesDeltaTimeToUpdateUnchanged()
    {
        var dispatcher = new FrameDispatcher(_sink, NoInput);

        dispatcher.OnFrame(0, 0);
        dispatcher.OnFrame(0.016, 16);
        dispatcher.OnFrame(double.NaN, 32);

        Assert.Equal([0, 0.016, double.NaN], _sink.DeltaTimes);
    }

    [Fact]
    public void OnFrame_PassesFrameTimeAndFullBufferToDrain()
    {
        var frameTimes = new List<double>();
        var lengths = new List<int>();
        var dispatcher = new FrameDispatcher(_sink, (destination, frameTimeMs) =>
        {
            frameTimes.Add(frameTimeMs);
            lengths.Add(destination.Length);
            return 0;
        });

        dispatcher.OnFrame(0, 1234.5);
        dispatcher.OnFrame(0.016, 1250.5);

        Assert.Equal([1234.5, 1250.5], frameTimes);
        Assert.All(lengths, static length => Assert.Equal(FrameDispatcher.InputCapacity * RawInputEvent.Size, length));
    }

    [Fact]
    public void OnFrame_PassesDrainedEventsToUpdate()
    {
        RawInputEvent[] queued =
        [
            new(RawInputEventKind.KeyDown, 0, (ushort)Key.Space, 0, -0.01f, 0, 0, 0),
            new(RawInputEventKind.PointerMove, 0, (ushort)PointerType.Mouse, 1, -0.005f, 12.5f, 7, 0),
        ];
        var dispatcher = new FrameDispatcher(_sink, (destination, _) =>
        {
            queued.CopyTo(MemoryMarshal.Cast<byte, RawInputEvent>(destination));
            return queued.Length;
        });

        dispatcher.OnFrame(0.016, 0);

        Assert.Equal(queued, Assert.Single(_sink.Events));
    }

    [Fact]
    public void OnFrame_NoInput_PassesEmptyEvents()
    {
        new FrameDispatcher(_sink, NoInput).OnFrame(0.016, 0);

        Assert.Empty(Assert.Single(_sink.Events));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(FrameDispatcher.InputCapacity + 1)]
    public void OnFrame_DrainReportsInvalidCount_ThrowsBeforeUpdate(int count)
    {
        var dispatcher = new FrameDispatcher(_sink, (_, _) => count);

        Assert.Throws<InvalidOperationException>(() => dispatcher.OnFrame(0.016, 0));
        Assert.Empty(_calls);
    }

    [Fact]
    public void OnFrame_UpdateThrows_SkipsRenderAndPropagates()
    {
        var failure = new InvalidOperationException("frame failure");
        var dispatcher = new FrameDispatcher(new ThrowingFrameSink(failure), NoInput);

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => dispatcher.OnFrame(0.016, 0)));
    }

    private sealed class ThrowingFrameSink(Exception exception) : IFrameSink
    {
        public void Update(double dt, ReadOnlySpan<RawInputEvent> events) => throw exception;

        public void Render() => Assert.Fail("Render must not run after a failed Update.");
    }
}
