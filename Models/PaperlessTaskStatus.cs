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
    public string? RelatedDocument { get; set; }          // version-variant type; parse leniently
}
