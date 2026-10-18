using System.Numerics;
using System.Runtime.InteropServices;

namespace LudumDare60.Game.Customers;

/// <summary>
/// A place where a customer waits while the player works on the revolver, instead of standing at the counter: in the
/// shop, or out in the street.
/// </summary>
/// <param name="Bottom">Where the bottom middle of the frame is drawn, in virtual pixels.</param>
/// <param name="Scale">How many times larger than in the sheet the frame is drawn there.</param>
/// <param name="IsOutside">Whether the place is out in the street, seen through the window: the customer is drawn
/// over the street and under the room, so the window frame and the wall cover them.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct CustomerSeat(Vector2 Bottom, float Scale, bool IsOutside = false);
