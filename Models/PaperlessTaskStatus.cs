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

    [JsonPropertyName("status_display")]
    public string? StatusDisplay { get; set; }            // Paperless 3.x pre-humanized label

    [JsonPropertyName("task_type")]
    public string? TaskType { get; set; }                 // consume_file | scheduled housekeeping types

    [JsonPropertyName("task_file_name")]
    public string? TaskFileName { get; set; }             // pre-3.x filename location

    [JsonPropertyName("input_data")]
    public TaskInputData? InputData { get; set; }         // 3.x filename location

    [JsonPropertyName("date_created")]
    public DateTimeOffset? DateCreated { get; set; }

    [JsonPropertyName("date_done")]
    public DateTimeOffset? DateDone { get; set; }

    /// <summary>Filename across Paperless versions (3.x nests it, older tops it).</summary>
    [JsonIgnore]
    public string? Filename => InputData?.Filename ?? TaskFileName;
}

public sealed class TaskInputData
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
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
