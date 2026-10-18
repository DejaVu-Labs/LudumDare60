namespace LudumDare60.Game.Orders;

/// <summary>
/// A dirty part of an order.
/// </summary>
/// <param name="Part">The part, by <see cref="RevolverParts"/> id.</param>
/// <param name="Kind">What covers it.</param>
/// <param name="Threshold">The share of the dirt, in (0, 1], that must come off for the part to count as clean.</param>
public sealed record DirtSpec(string Part, DirtKind Kind, float Threshold = 0.9f);
