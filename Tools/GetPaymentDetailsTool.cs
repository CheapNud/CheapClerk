using System.ComponentModel;
using System.Text;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class GetPaymentDetailsTool
{
    [McpServerTool(Name = "get_payment_details"), Description("Extract payment details from a cached invoice and generate an EPC payment QR payload.")]
    public static async Task<string> GetPaymentDetails(
        ExtractionCacheService extractionCacheService,
        PaperlessClient paperlessClient,
        [Description("The Paperless document ID containing the invoice.")] int documentId,
        CancellationToken cancellationToken = default)
    {
        var extraction = await extractionCacheService.GetCachedAsync(documentId, cancellationToken);
        if (extraction is null)
            return $"No extraction cached for document {documentId} — run extract_structured_data first.";

        var invoice = extraction.Invoice;
        if (invoice is null || string.IsNullOrWhiteSpace(invoice.BeneficiaryIban) || !invoice.TotalAmount.HasValue)
            return $"Document {documentId} has no payable invoice data (need IBAN and amount).";

        // Resolve beneficiary: use Vendor, fallback to correspondent name
        var beneficiary = invoice.Vendor;
        if (string.IsNullOrWhiteSpace(beneficiary))
        {
            var doc = await paperlessClient.GetDocumentAsync(documentId, cancellationToken);
            if (doc?.CorrespondentId.HasValue ?? false)
            {
                var correspondentLookup = await paperlessClient.GetCorrespondentLookupAsync(cancellationToken);
                if (correspondentLookup.TryGetValue(doc.CorrespondentId.Value, out var correspondentName))
                    beneficiary = correspondentName;
            }
        }

        var reference = invoice.PaymentReference ?? invoice.InvoiceNumber;
        var payload = EpcPayment.BuildForInvoice(invoice, beneficiary);

        if (payload is null)
            return $"Document {documentId} failed EPC payload validation (check IBAN, amount, and currency).";

        // Parse payload lines to extract IBAN (line 7 = index 6)
        var lines = payload.Split('\n');
        var iban = lines.Length > 6 ? lines[6] : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"**Payment Details — Document {documentId}**");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(beneficiary))
            sb.AppendLine($"Beneficiary: {beneficiary}");

        if (!string.IsNullOrWhiteSpace(iban))
            sb.AppendLine($"IBAN: {iban}");

        if (invoice.TotalAmount.HasValue)
        {
            var currencyCode = string.IsNullOrWhiteSpace(invoice.Currency) ? "EUR" : invoice.Currency;
            sb.AppendLine($"Amount: {invoice.TotalAmount:0.00} {currencyCode}");
        }

        if (!string.IsNullOrWhiteSpace(reference))
            sb.AppendLine($"Reference: {reference}");

        if (!string.IsNullOrWhiteSpace(invoice.DueDate))
            sb.AppendLine($"Due Date: {invoice.DueDate}");

        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(payload);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("^Renders as QR code when scanned with SEPA-capable payment app.");

        return sb.ToString();
    }
}
