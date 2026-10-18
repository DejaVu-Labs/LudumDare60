using System.Text.Json.Serialization;

namespace LudumDare60.Game.Orders;

/// <summary>
/// What the customer says is wrong, shown as an icon. In JSON: the kebab-case name, for example <c>jammed-cylinder</c>.
/// </summary>
[JsonConverter(typeof(KebabCaseEnumConverter<Symptom>))]
public enum Symptom
{
    JammedCylinder = 0,
    LooseGrip = 1,
    BrokenSpring = 2,
    StiffAction = 3,

    /// <summary>
    /// "Make it work again": everything.
    /// </summary>
    Everything = 4,
}
