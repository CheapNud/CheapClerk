using System.Text.Json.Serialization;

namespace CheapClerk.Models;

public sealed class PaperlessPage<T>
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("results")]
    public List<T> Entries { get; set; } = [];
}
