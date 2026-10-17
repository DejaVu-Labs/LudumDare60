namespace LudumDare60.Engine.Tests;

/// <summary>
/// Records every call the engine makes, in order.
/// </summary>
/// <remarks>
/// Fails fast after <see cref="MaxCalls"/> calls: a broken delta-time clamp turns the fixed-step loop into an endless
/// one, and recording it without a limit exhausts the machine's memory instead of failing the test.
/// </remarks>
internal sealed class RecordingGame : IGame
{
    public const int MaxCalls = 10_000;

    public List<string> Calls { get; } = [];

    public List<FrameContext> FixedUpdates { get; } = [];

    public List<FrameContext> Updates { get; } = [];

    public List<double> Alphas { get; } = [];

    public void FixedUpdate(in FrameContext context)
    {
        Record(nameof(FixedUpdate));
        FixedUpdates.Add(context);
    }

    public void Update(in FrameContext context)
    {
        Record(nameof(Update));
        Updates.Add(context);
    }

    public void PrepareRender(double alpha)
    {
        Record(nameof(PrepareRender));
        Alphas.Add(alpha);
    }

    public void Render() => Record(nameof(Render));

    private void Record(string call)
    {
        if (Calls.Count >= MaxCalls)
        {
            Assert.Fail($"The engine made more than {MaxCalls} calls; the fixed-step loop is likely unbounded.");
        }

        Calls.Add(call);
    }
}
