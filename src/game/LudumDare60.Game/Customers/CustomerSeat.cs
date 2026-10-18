using System.Numerics;

namespace LudumDare60.Game.Customers;

/// <summary>
/// A place in the shop where a customer sits while the player works on the revolver, instead of standing at the
/// counter.
/// </summary>
/// <param name="Bottom">Where the bottom middle of the bust is drawn, in virtual pixels.</param>
/// <param name="Scale">How many times larger than in the sheet the bust is drawn there.</param>
public readonly record struct CustomerSeat(Vector2 Bottom, float Scale);
