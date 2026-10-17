namespace LudumDare60.Engine.Platform.Browser.Tests;

public sealed class FrameDispatcherTests
{
    private readonly RecordingFrameSink _sink = new();

    [Fact]
    public void OnFrame_CallsUpdateThenRender()
    {
        new FrameDispatcher(_sink).OnFrame(0.016);

        Assert.Equal(["Update", "Render"], _sink.Calls);
    }

    [Fact]
    public void OnFrame_PassesDeltaTimeToUpdateUnchanged()
    {
        var dispatcher = new FrameDispatcher(_sink);

        dispatcher.OnFrame(0);
        dispatcher.OnFrame(0.016);
        dispatcher.OnFrame(double.NaN);

        Assert.Equal([0, 0.016, double.NaN], _sink.DeltaTimes);
    }

    [Fact]
    public void OnFrame_UpdateThrows_SkipsRenderAndPropagates()
    {
        var failure = new InvalidOperationException("frame failure");
        var dispatcher = new FrameDispatcher(new ThrowingFrameSink(failure));

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => dispatcher.OnFrame(0.016)));
    }

    private sealed class ThrowingFrameSink(Exception exception) : IFrameSink
    {
        public void Update(double dt) => throw exception;

        public void Render() => Assert.Fail("Render must not run after a failed Update.");
    }
}
