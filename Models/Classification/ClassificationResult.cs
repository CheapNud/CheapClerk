using System.ComponentModel;

namespace CheapClerk.Models.Classification;

public sealed class ClassificationResult
{
    [Description("A concise, human-readable title for the document, in the document's own language. Example: 'KBC Woonverzekering polis 2026'.")]
    public string? SuggestedTitle { get; set; }

    [Description("The organization or person the document is FROM, e.g. 'KBC', 'Engie', 'Stad Antwerpen'. Null if unclear.")]
    public string? Correspondent { get; set; }

    [Description("A single document type such as 'Invoice', 'Insurance Policy', 'Contract', 'Receipt', 'Tax Document', 'Warranty', 'Letter'. Prefer one from the provided existing list.")]
    public string? DocumentType { get; set; }

    [Description("Topical tags for retrieval. STRONGLY prefer reusing tags from the provided existing list; only invent a new tag when nothing existing fits.")]
    public List<string> Tags { get; set; } = [];

    [Description("The date the document was issued (not scanned), format yyyy-MM-dd. Null if not determinable.")]
    public string? DocumentDate { get; set; }

    [Description("Classification confidence from 0.0 to 1.0. Use below 0.5 when the text is ambiguous or unreadable.")]
    public double Confidence { get; set; }
}
