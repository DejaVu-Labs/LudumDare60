using LudumDare60.Engine.Rendering;

namespace LudumDare60.Game.Customers;

/// <summary>
/// The layout of a customer sheet: one bust per <see cref="CustomerPose"/>, side by side, from the chest up, standing
/// on the bottom edge.
/// </summary>
public static class CustomerSheet
{
    public const int FrameWidth = 72;
    public const int FrameHeight = 96;

    /// <summary>
    /// The frame of <paramref name="pose"/> in the sheet.
    /// </summary>
    public static IntRect Frame(CustomerPose pose) => new((int)pose * FrameWidth, 0, FrameWidth, FrameHeight);
}
