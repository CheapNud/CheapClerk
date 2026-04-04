using System.Text.Json.Serialization;

namespace CheapClerk.Models;

public sealed class PaperlessDocument
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("correspondent")]
    public int? CorrespondentId { get; set; }

    [JsonPropertyName("document_type")]
    public int? DocumentTypeId { get; set; }

    [JsonPropertyName("tags")]
    public List<int> Tags { get; set; } = [];

    [JsonPropertyName("created")]
    public DateTime? Created { get; set; }

    [JsonPropertyName("added")]
    public DateTime? Added { get; set; }

    [JsonPropertyName("modified")]
    public DateTime? Modified { get; set; }

    [JsonPropertyName("archive_serial_number")]
    public int? ArchiveSerialNumber { get; set; }

    [JsonPropertyName("original_file_name")]
    public string? OriginalFileName { get; set; }
}
