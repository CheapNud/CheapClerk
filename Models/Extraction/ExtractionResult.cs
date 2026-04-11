using System.ComponentModel;

namespace CheapClerk.Models.Extraction;

public sealed class ExtractionResult
{
    [Description("The detected document category.")]
    public DocumentCategory Category { get; set; }

    [Description("Confidence score of the classification, from 0.0 to 1.0.")]
    public double Confidence { get; set; }

    [Description("Extracted invoice fields, populated when Category is Invoice.")]
    public ExtractedInvoice? Invoice { get; set; }

    [Description("Extracted insurance fields, populated when Category is Insurance.")]
    public ExtractedInsurance? Insurance { get; set; }

    [Description("Extracted contract fields, populated when Category is Contract.")]
    public ExtractedContract? Contract { get; set; }

    [Description("A one-sentence summary of the document.")]
    public string? Summary { get; set; }
}
