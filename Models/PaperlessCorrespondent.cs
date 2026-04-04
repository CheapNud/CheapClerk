using System.Text.Json.Serialization;

namespace CheapClerk.Models;

public sealed class PaperlessCorrespondent
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("document_count")]
    public int DocumentCount { get; set; }
}
