using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;
using LudumDare60.Engine.Sounds;

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

    /// <summary>
    /// A copy of the events passed to each <see cref="Update"/>.
    /// </summary>
    public List<RawInputEvent[]> UpdateEvents { get; } = [];

    public List<double> Alphas { get; } = [];

    /// <summary>
    /// The asset manager passed to <see cref="Initialize"/>.
    /// </summary>
    public AssetManager? Assets { get; private set; }

    /// <summary>
    /// The graphics passed to <see cref="Initialize"/>.
    /// </summary>
    public Graphics? Graphics { get; private set; }

    /// <summary>
    /// The audio passed to <see cref="Initialize"/>.
    /// </summary>
    public Audio? Audio { get; private set; }

    /// <summary>
    /// Runs inside <see cref="Initialize"/>, after the call is recorded.
    /// </summary>
    public Action<AssetManager, Graphics>? OnInitialize { get; set; }

    /// <summary>
    /// Runs inside <see cref="FixedUpdate"/>, after the call is recorded.
    /// </summary>
    public Action<FrameContext>? OnFixedUpdate { get; set; }

    /// <summary>
    /// Runs inside <see cref="Update"/>, after the call is recorded.
    /// </summary>
    public Action<Graphics>? OnUpdate { get; set; }

    /// <summary>
    /// Runs inside <see cref="Update"/>, after <see cref="OnUpdate"/>.
    /// </summary>
    public Action<FrameContext>? OnUpdateContext { get; set; }

    /// <summary>
    /// Runs inside <see cref="Render"/>, after the call is recorded.
    /// </summary>
    public Action<SpriteBatch>? OnRender { get; set; }

    /// <summary>
    /// Runs inside <see cref="Initialize"/>, after <see cref="OnInitialize"/>.
    /// </summary>
    public Action<Audio>? OnInitializeAudio { get; set; }

    /// <summary>
    /// Runs inside <see cref="Update"/>, after <see cref="OnUpdateContext"/>.
    /// </summary>
    public Action<Audio>? OnUpdateAudio { get; set; }

    /// <summary>
    /// Runs inside <see cref="PrepareRender"/>, after the call is recorded.
    /// </summary>
    public Action<Audio>? OnPrepareRenderAudio { get; set; }

    /// <summary>
    /// Runs inside <see cref="Render"/>, after <see cref="OnRender"/>.
    /// </summary>
    public Action<Audio>? OnRenderAudio { get; set; }

    public void Initialize(AssetManager assets, Graphics graphics, Audio audio)
    {
        Record(nameof(Initialize));
        Assets = assets;
        Graphics = graphics;
        Audio = audio;
        OnInitialize?.Invoke(assets, graphics);
        OnInitializeAudio?.Invoke(audio);
    }

    public void FixedUpdate(in FrameContext context)
    {
        Record(nameof(FixedUpdate));
        FixedUpdates.Add(context);
        OnFixedUpdate?.Invoke(context);
    }

    public void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events)
    {
        Record(nameof(Update));
        Updates.Add(context);
        UpdateEvents.Add(events.ToArray());
        OnUpdate?.Invoke(Graphics!);
        OnUpdateContext?.Invoke(context);
        OnUpdateAudio?.Invoke(Audio!);
    }

    public void PrepareRender(double alpha)
    {
        Record(nameof(PrepareRender));
        Alphas.Add(alpha);
        OnPrepareRenderAudio?.Invoke(Audio!);
    }

    public void Render(SpriteBatch batch)
    {
        Record(nameof(Render));
        OnRender?.Invoke(batch);
        OnRenderAudio?.Invoke(Audio!);
    }

    private void Record(string call)
    {
        if (Calls.Count >= MaxCalls)
        {
            Assert.Fail($"The engine made more than {MaxCalls} calls; the fixed-step loop is likely unbounded.");
        }

        Calls.Add(call);
    }
}
