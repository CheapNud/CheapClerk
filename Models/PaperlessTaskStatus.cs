using System.Text.Json;
using System.Text.Json.Serialization;

namespace CheapClerk.Models;

public sealed class PaperlessTaskStatus
{
    [JsonPropertyName("task_id")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;   // PENDING | STARTED | SUCCESS | FAILURE

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("related_document")]
    [JsonConverter(typeof(LenientStringConverter))]
    public string? RelatedDocument { get; set; }          // version-variant type; parse leniently
}

internal sealed class LenientStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions serializerSettings) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var numeric)
                ? numeric.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => throw new JsonException($"Unexpected token {reader.TokenType} for related_document")
        };

    public override void Write(Utf8JsonWriter writer, string? textValue, JsonSerializerOptions serializerSettings)
    {
        if (textValue is null) writer.WriteNullValue();
        else writer.WriteStringValue(textValue);
    }
}
