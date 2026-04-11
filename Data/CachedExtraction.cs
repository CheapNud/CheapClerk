using CheapClerk.Models.Extraction;

namespace CheapClerk.Data;

public sealed class CachedExtraction
{
    public int DocumentId { get; set; }
    public DocumentCategory Category { get; set; }
    public double Confidence { get; set; }
    public string? Summary { get; set; }

    /// <summary>Parsed expiry date if the document has one (insurance end, contract end, invoice due). yyyy-MM-dd.</summary>
    public DateTime? ExpiryDate { get; set; }

    /// <summary>Full ExtractionResult serialized as JSON for round-tripping.</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public DateTime ExtractedAt { get; set; }
}
