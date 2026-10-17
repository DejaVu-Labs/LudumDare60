namespace LudumDare60.Engine;

/// <summary>
/// A game hosted by <see cref="GameEngine"/>. Methods are listed in the order the engine calls them each frame.
/// </summary>
public interface IGame
{
    /// <summary>
    /// Advances the simulation by one fixed step (<see cref="GameEngine.FixedDeltaTime"/>). Called zero or more times per frame.
    /// </summary>
    void FixedUpdate(in FrameContext context);

    /// <summary>
    /// Per-frame logic with a variable time step. Called once per frame, after the fixed steps.
    /// </summary>
    void Update(in FrameContext context);

    /// <summary>
    /// Builds render data for this frame.
    /// </summary>
    /// <param name="alpha">How far the current time is between the last fixed step and the next one, in [0, 1).
    /// Use it to interpolate between the previous and the current simulation state.</param>
    void PrepareRender(double alpha);

    /// <summary>
    /// Submits the data built by <see cref="PrepareRender"/>. Called once per frame, after <see cref="PrepareRender"/>.
    /// </summary>
    void Render();
}
