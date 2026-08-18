using System.Text.Json.Serialization;

namespace CheapClerk.Models;

public sealed class PaperlessStatistics
{
    [JsonPropertyName("documents_total")]
    public int DocumentsTotal { get; set; }

    [JsonPropertyName("documents_inbox")]
    public int DocumentsInbox { get; set; }

    [JsonPropertyName("character_count")]
    public long CharacterCount { get; set; }

    [JsonPropertyName("tag_count")]
    public int TagCount { get; set; }

    [JsonPropertyName("correspondent_count")]
    public int CorrespondentCount { get; set; }

    [JsonPropertyName("document_type_count")]
    public int DocumentTypeCount { get; set; }

    [JsonPropertyName("document_file_type_counts")]
    public List<FileTypeCount> FileTypeCounts { get; set; } = [];
}

public sealed class FileTypeCount
{
    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; } = string.Empty;

    [JsonPropertyName("mime_type_count")]
    public int Count { get; set; }
}
