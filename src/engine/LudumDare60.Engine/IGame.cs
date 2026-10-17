using LudumDare60.Engine.Assets;
using LudumDare60.Engine.Platform;
using LudumDare60.Engine.Rendering;

namespace LudumDare60.Engine;

/// <summary>
/// A game hosted by <see cref="GameEngine"/>. Methods are listed in the order the engine calls them: <see cref="Initialize"/>
/// once, then the others every frame.
/// </summary>
public interface IGame
{
    /// <summary>
    /// Called once per run, before the first frame. Register asset loaders and request the first assets here.
    /// </summary>
    /// <param name="assets">The run's asset manager. Keep it: the engine updates it at the start of every frame and
    /// unloads everything when the run ends.</param>
    /// <param name="graphics">The run's texture factory. Keep it: textures can be created from the first frame on, not
    /// here, and the engine destroys the remaining ones when the run ends.</param>
    void Initialize(AssetManager assets, Graphics graphics);

    /// <summary>
    /// Advances the simulation by one fixed step (<see cref="GameEngine.FixedDeltaTime"/>). Called zero or more times per frame.
    /// </summary>
    void FixedUpdate(in FrameContext context);

    /// <summary>
    /// Per-frame logic with a variable time step. Called once per frame, after the fixed steps.
    /// </summary>
    /// <param name="context">Timing of this frame.</param>
    /// <param name="events">Raw input events of this frame, in arrival order, valid only during this call. Temporary:
    /// an input state layer will replace them (see docs/backlog.md).</param>
    void Update(in FrameContext context, ReadOnlySpan<RawInputEvent> events);

    /// <summary>
    /// Builds render data for this frame.
    /// </summary>
    /// <param name="alpha">How far the current time is between the last fixed step and the next one, in [0, 1).
    /// Use it to interpolate between the previous and the current simulation state.</param>
    void PrepareRender(double alpha);

    /// <summary>
    /// Draws the data built by <see cref="PrepareRender"/>. Called once per frame, after <see cref="PrepareRender"/>.
    /// The engine submits the frame to the GPU after this call returns.
    /// </summary>
    /// <param name="batch">Records this frame's sprites. Valid only during this call.</param>
    void Render(SpriteBatch batch);
}
