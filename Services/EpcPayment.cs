using CheapClerk.Models.Extraction;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CheapClerk.Services;

/// <summary>EPC069-12 SCT (SEPA Credit Transfer) QR payload builder.</summary>
public static class EpcPayment
{
    /// <summary>EPC069-12 SCT payload, or null when the inputs don't make a payable bill.</summary>
    public static string? BuildPayload(string? beneficiaryName, string? iban, decimal? amount, string? currency, string? reference)
    {
        // Validate beneficiary name
        if (string.IsNullOrWhiteSpace(beneficiaryName))
            return null;

        var trimmedName = beneficiaryName.Trim();
        var truncatedName = trimmedName.Length > 70 ? trimmedName[..70] : trimmedName;

        // Validate and normalize IBAN
        var normalizedIban = NormalizeIban(iban);
        if (normalizedIban is null)
            return null;

        // Validate amount. Sub-cent precision means the extraction is unreliable —
        // never silently round a payment amount; reject and let a human read the bill
        if (amount is null || amount < 0.01m || amount > 999_999_999.99m)
            return null;
        if (amount != Math.Round(amount.Value, 2))
            return null;

        // Validate currency (null = EUR, otherwise must be EUR)
        if (currency is not null && !currency.Trim().Equals("EUR", StringComparison.OrdinalIgnoreCase))
            return null;

        // Format amount
        var formattedAmount = amount.Value.ToString("0.00", CultureInfo.InvariantCulture);

        // Process reference
        var trimmedReference = reference?.Trim();
        var truncatedReference = trimmedReference?.Length > 140 ? trimmedReference[..140] : (trimmedReference ?? string.Empty);

        // Build payload
        var lines = new[]
        {
            "BCD",
            "002",
            "1",
            "SCT",
            string.Empty,
            truncatedName,
            normalizedIban,
            $"EUR{formattedAmount}",
            string.Empty,
            string.Empty,
            truncatedReference
        };

        return string.Join("\n", lines);
    }

    /// <summary>Composes the payload from an extracted invoice; beneficiary falls back to fallbackBeneficiary when Vendor is missing; reference = PaymentReference ?? InvoiceNumber.</summary>
    public static string? BuildForInvoice(ExtractedInvoice invoice, string? fallbackBeneficiary)
    {
        var beneficiaryName = invoice.Vendor ?? fallbackBeneficiary;
        var reference = invoice.PaymentReference ?? invoice.InvoiceNumber;

        return BuildPayload(beneficiaryName, invoice.BeneficiaryIban, invoice.TotalAmount, invoice.Currency, reference);
    }

    private static string? NormalizeIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return null;

        // Strip whitespace and uppercase
        var normalized = Regex.Replace(iban, @"\s", string.Empty).ToUpperInvariant();

        // Validate format: starts with 2 letters, then 2 digits, then 11-30 alphanumeric chars
        // Total length 15-34
        if (!Regex.IsMatch(normalized, @"^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$"))
            return null;

        return normalized;
    }
}
