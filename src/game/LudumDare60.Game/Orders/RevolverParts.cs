namespace LudumDare60.Game.Orders;

/// <summary>
/// The parts of the one revolver model the shop repairs, by the ids that order data uses, and which part physically
/// blocks which. An order unlocks some of them; the rest stay fixed for that order.
/// </summary>
public static class RevolverParts
{
    public const string BasePin = "base-pin";
    public const string Cylinder = "cylinder";
    public const string GripScrew = "grip-screw";
    public const string GripPanels = "grip-panels";
    public const string Mainspring = "mainspring";
    public const string HammerScrew = "hammer-screw";
    public const string Hammer = "hammer";

    public static IReadOnlyList<string> All { get; } = [BasePin, Cylinder, GripScrew, GripPanels, Mainspring, HammerScrew, Hammer];

    /// <summary>
    /// Each blocker must come off before the part it blocks can, and go back in after it. Reassembly is the same
    /// graph backwards.
    /// </summary>
    public static IReadOnlyList<PartBlock> Blocks { get; } =
    [
        new(BasePin, Cylinder),
        new(GripScrew, GripPanels),
        new(GripPanels, Mainspring),
        new(HammerScrew, Hammer),
    ];

    public static bool IsKnown(string id)
    {
        foreach (var part in All)
        {
            if (string.Equals(part, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
