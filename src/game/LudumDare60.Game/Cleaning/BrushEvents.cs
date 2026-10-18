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
    /// The pointer went up and put the brush down where it is.
    /// </summary>
    PutDown = 2,
}
