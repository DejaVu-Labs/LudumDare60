namespace LudumDare60.Game.Cleaning;

/// <summary>
/// What happened to a <see cref="BrushTool"/> during one <see cref="BrushTool.Step"/>.
/// </summary>
[Flags]
public enum BrushEvents
{
    None = 0,

    /// <summary>
    /// The pointer went down on the brush and took it.
    /// </summary>
    PickedUp = 1,

    /// <summary>
    /// The pointer went up and let the brush go; it slides back to its place.
    /// </summary>
    PutDown = 2,

    /// <summary>
    /// The brush came to rest at its place.
    /// </summary>
    Landed = 4,
}
