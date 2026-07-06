using System.Text.Json.Serialization;

namespace CheapClerk.Models;

public sealed class DocumentUpdate
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("correspondent")]
    public int? CorrespondentId { get; set; }

    [JsonPropertyName("document_type")]
    public int? DocumentTypeId { get; set; }

    [JsonPropertyName("tags")]
    public List<int>? TagIds { get; set; }

    [JsonPropertyName("created")]
    public string? CreatedDate { get; set; }
}
