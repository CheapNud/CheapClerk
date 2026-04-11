using System.ComponentModel;

namespace CheapClerk.Models.Extraction;

public sealed class ExtractedInvoice
{
    [Description("The name of the vendor or company that issued the invoice.")]
    public string? Vendor { get; set; }

    [Description("The invoice number as printed on the document.")]
    public string? InvoiceNumber { get; set; }

    [Description("The total amount due, as a decimal number (no currency symbol).")]
    public decimal? TotalAmount { get; set; }

    [Description("The currency code, e.g. EUR, USD.")]
    public string? Currency { get; set; }

    [Description("The date the invoice was issued, in yyyy-MM-dd format.")]
    public string? IssueDate { get; set; }

    [Description("The date payment is due, in yyyy-MM-dd format.")]
    public string? DueDate { get; set; }

    [Description("The customer account number or reference, if present.")]
    public string? AccountNumber { get; set; }

    [Description("Structured communication reference (Belgian: gestructureerde mededeling), if present.")]
    public string? PaymentReference { get; set; }

    [Description("The IBAN account to pay to, if present.")]
    public string? BeneficiaryIban { get; set; }
}
