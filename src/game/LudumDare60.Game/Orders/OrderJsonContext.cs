using System.Text.Json;
using System.Text.Json.Serialization;

namespace LudumDare60.Game.Orders;

/// <summary>
/// Source-generated JSON for the order data: no reflection, safe to trim and compile ahead of time. Strict: an
/// unknown property, a missing property without a default, or a null where none is allowed is an error, so a typo in
/// the data fails at load time instead of quietly dropping a field.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(OrderBook))]
internal sealed partial class OrderJsonContext : JsonSerializerContext;
