namespace LudumDare60.Game.Orders;

/// <summary>
/// What the revolver hides: it shows when <paramref name="Part"/> comes off.
/// </summary>
/// <param name="Id">Which find to draw, by name: lowercase kebab-case.</param>
/// <param name="Part">The part it hides in, by <see cref="RevolverParts"/> id.</param>
/// <param name="Text">The handwriting on the find, if it has any. Shown when the player opens it.</param>
public sealed record Find(string Id, string Part, string? Text = null);
