namespace CheapClerk.Data;

public sealed class NameTranslation
{
    public string Kind { get; set; } = string.Empty;          // "tag" | "document_type"
    public string CanonicalName { get; set; } = string.Empty; // exactly as stored in Paperless
    public string Culture { get; set; } = string.Empty;       // "nl" | "en"
    public string Label { get; set; } = string.Empty;
    public DateTime TranslatedAtUtc { get; set; }
}
