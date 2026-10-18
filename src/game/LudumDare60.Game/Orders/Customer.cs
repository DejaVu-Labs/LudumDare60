namespace LudumDare60.Game.Orders;

/// <summary>
/// Who brings the revolver in.
/// </summary>
/// <param name="Look">Which bust, head, hair, clothes and palette to draw, by name. The customer art decides what names
/// exist.</param>
/// <param name="Description">A few words for the people making the game, never shown to the player.</param>
public sealed record Customer(string Look, string Description);
