using System.Numerics;

namespace LudumDare60.Game;

/// <summary>
/// Things in the room that answer a click without being part of a repair.
/// </summary>
public static class ShopDecor
{
    /// <summary>
    /// The top-left corner of the elephant statuette on the lit shelf on the left of the background, in virtual pixels.
    /// </summary>
    public static readonly Vector2 ElephantMin = new(22, 30);

    /// <summary>
    /// The bottom-right corner of the elephant statuette, from the tip of its raised trunk to its feet.
    /// </summary>
    public static readonly Vector2 ElephantMax = new(60, 60);

    /// <summary>
    /// How long the elephant's trumpet lasts, in seconds (<see cref="AssetPaths.ElephantSound"/>): a press while it
    /// plays does not start another.
    /// </summary>
    public const float ElephantSeconds = 2.4f;

    /// <summary>
    /// Whether <paramref name="point"/>, in virtual pixels, lies on the elephant statuette.
    /// </summary>
    public static bool IsOnElephant(Vector2 point)
        => point.X >= ElephantMin.X && point.Y >= ElephantMin.Y && point.X < ElephantMax.X && point.Y < ElephantMax.Y;
}
