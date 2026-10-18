using System.Text.Json.Serialization;

namespace LudumDare60.Game.Orders;

/// <summary>
/// What covers a dirty part. In JSON: the kebab-case name.
/// </summary>
[JsonConverter(typeof(KebabCaseEnumConverter<DirtKind>))]
public enum DirtKind
{
    Rust = 0,
    Grime = 1,
}
