namespace LudumDare60.Game.Bench;

/// <summary>
/// Where a part in the gun is drawn among the others, back to front. A part out of the gun lies on top of all of them.
/// </summary>
public enum PartLayer
{
    /// <summary>
    /// Under the frame, which hides what of the part is still inside: the base pin.
    /// </summary>
    UnderFrame = 0,

    /// <summary>
    /// Over the frame: the cylinder in its window.
    /// </summary>
    OverFrame = 1,

    /// <summary>
    /// In the grip, under its panel: the mainspring.
    /// </summary>
    InGrip = 2,

    /// <summary>
    /// The grip panel, over the grip.
    /// </summary>
    OverGrip = 3,

    /// <summary>
    /// Over the grip panel: the grip screw.
    /// </summary>
    OverPanel = 4,

    /// <summary>
    /// The hammer, turned to where the hammer is cocked, under its screw.
    /// </summary>
    Hammer = 5,

    /// <summary>
    /// Over the hammer: the hammer screw, its pivot.
    /// </summary>
    OverHammer = 6,
}
