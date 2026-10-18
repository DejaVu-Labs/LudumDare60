using System.Text.Json;
using System.Text.Json.Serialization;

namespace LudumDare60.Game.Orders;

/// <summary>
/// Reads and writes an enum as its kebab-case name (<c>JammedCylinder</c> as <c>jammed-cylinder</c>) and nothing else:
/// not the C# name, not another casing, not a number. A wrong name fails with the list of valid ones.
/// </summary>
public sealed class KebabCaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly TEnum[] s_values = Enum.GetValues<TEnum>();
    private static readonly string[] s_names = Array.ConvertAll(
        s_values, value => JsonNamingPolicy.KebabCaseLower.ConvertName(Enum.GetName(value)!));

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            for (var i = 0; i < s_names.Length; i++)
            {
                if (reader.ValueTextEquals(s_names[i]))
                {
                    return s_values[i];
                }
            }
        }

        throw new JsonException($"Expected one of {string.Join(", ", s_names)} as a string.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(s_names[Array.IndexOf(s_values, value)]);
    }
}
