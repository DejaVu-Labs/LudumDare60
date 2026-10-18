using LudumDare60.Engine.Assets;
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
    /// How many poses a sheet has.
    /// </summary>
    public const int PoseCount = 3;

    /// <summary>
    /// The frame of <paramref name="pose"/> in the sheet.
    /// </summary>
    public static IntRect Frame(CustomerPose pose) => new((int)pose * FrameWidth, 0, FrameWidth, FrameHeight);

    /// <summary>
    /// The outline of <paramref name="pose"/> in the sheet <see cref="CreateGlow"/> makes: one pixel larger than the
    /// frame on every side, so it is drawn one pixel up and left of it.
    /// </summary>
    public static IntRect Glow(CustomerPose pose) => new((int)pose * (FrameWidth + 2), 0, FrameWidth + 2, FrameHeight + 2);

    /// <summary>
    /// The glow sheet of a customer sheet: an outline around each pose, open at the bottom where the bust is cut off.
    /// </summary>
    public static Image CreateGlow(Image sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var width = PoseCount * (FrameWidth + 2);
        var height = FrameHeight + 2;
        var pixels = new byte[width * height * Image.BytesPerPixel];
        for (var pose = 0; pose < PoseCount; pose++)
        {
            var glow = Glow((CustomerPose)pose);
            SpriteOutline.Draw(sheet.Pixels, sheet.Width, Frame((CustomerPose)pose), pixels, width, glow.X, glow.Y, openBottom: true);
        }

        return new Image(width, height, pixels);
    }
}
