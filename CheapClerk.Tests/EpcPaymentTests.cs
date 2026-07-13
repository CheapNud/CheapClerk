using CheapClerk.Models.Extraction;
using CheapClerk.Services;
using System.Globalization;
using Xunit;

namespace CheapClerk.Tests;

public sealed class EpcPaymentTests
{
    [Theory]
    [InlineData("100.005")]
    [InlineData("0.015")]
    [InlineData("123.4549")]
    public void BuildPayload_SubCentPrecision_RejectsInsteadOfRounding(string rawAmount)
    {
        var subCentAmount = decimal.Parse(rawAmount, CultureInfo.InvariantCulture);

        var payload = EpcPayment.BuildPayload("Engie", "BE68539007547034", subCentAmount, null, null);

        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_CurrencyWithWhitespace_IsAccepted()
    {
        var payload = EpcPayment.BuildPayload("Engie", "BE68539007547034", 10m, " EUR ", null);

        Assert.NotNull(payload);
        Assert.Contains("EUR10.00", payload);
    }

    [Fact]
    public void BuildPayload_HappyPath_ReturnsCorrectPayload()
    {
        // Arrange
        var beneficiaryName = "Engie Electrabel NV";
        var iban = "BE68 5390 0754 7034";
        var amount = 123.45m;
        string? currency = null;
        var reference = "+++090/9337/55493+++";

        var expected = "BCD\n002\n1\nSCT\n\nEngie Electrabel NV\nBE68539007547034\nEUR123.45\n\n\n+++090/9337/55493+++";

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Equal(expected, payload);
    }

    [Fact]
    public void BuildPayload_IbanWithSpaces_NormalizesAndReturnsPayload()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68 5390 0754 7034";
        var amount = 100m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.NotNull(payload);
        Assert.Contains("BE68539007547034", payload);
    }

    [Fact]
    public void BuildPayload_LowercaseIban_UppercasesAndReturnsPayload()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "be68539007547034";
        var amount = 100m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.NotNull(payload);
        Assert.Contains("BE68539007547034", payload);
    }

    [Fact]
    public void BuildPayload_InvalidIban_ReturnsNull()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "INVALID";
        var amount = 100m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_NullBeneficiaryName_ReturnsNull()
    {
        // Arrange
        string? beneficiaryName = null;
        var iban = "BE68539007547034";
        var amount = 100m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_WhitespaceBeneficiaryName_ReturnsNull()
    {
        // Arrange
        var beneficiaryName = "   ";
        var iban = "BE68539007547034";
        var amount = 100m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_ZeroAmount_ReturnsNull()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68539007547034";
        var amount = 0m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_NullAmount_ReturnsNull()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68539007547034";
        decimal? amount = null;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_AmountLessThanMinimum_ReturnsNull()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68539007547034";
        var amount = 0.001m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_AmountGreaterThanMaximum_ReturnsNull()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68539007547034";
        var amount = 1_000_000_000m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_CurrencyUsd_ReturnsNull()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68539007547034";
        var amount = 100m;
        var currency = "USD";
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.Null(payload);
    }

    [Fact]
    public void BuildPayload_CurrencyEurLowercase_ReturnsPayload()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68539007547034";
        var amount = 100m;
        var currency = "eur";
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.NotNull(payload);
    }

    [Fact]
    public void BuildPayload_NameWith80Chars_TruncatesTo70()
    {
        // Arrange
        var beneficiaryName = new string('A', 80);
        var iban = "BE68539007547034";
        var amount = 100m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.NotNull(payload);
        var lines = payload.Split('\n');
        var nameInPayload = lines[5];
        Assert.Equal(new string('A', 70), nameInPayload);
    }

    [Fact]
    public void BuildPayload_ReferenceWith200Chars_TruncatesTo140()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68539007547034";
        var amount = 100m;
        string? currency = null;
        var reference = new string('X', 200);

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.NotNull(payload);
        var lines = payload.Split('\n');
        var referenceInPayload = lines[10];
        Assert.Equal(new string('X', 140), referenceInPayload);
    }

    [Fact]
    public void BuildPayload_Amount1000_FormatsWithInvariantCulture()
    {
        // Arrange
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("nl-BE");

            var beneficiaryName = "Test Name";
            var iban = "BE68539007547034";
            var amount = 1000m;
            string? currency = null;
            string? reference = null;

            // Act
            var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

            // Assert
            Assert.NotNull(payload);
            Assert.Contains("EUR1000.00", payload);
            Assert.DoesNotContain("EUR1000,00", payload);
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    [Fact]
    public void BuildPayload_NullReference_OmitsContentButKeepsLine()
    {
        // Arrange
        var beneficiaryName = "Test Name";
        var iban = "BE68539007547034";
        var amount = 100m;
        string? currency = null;
        string? reference = null;

        // Act
        var payload = EpcPayment.BuildPayload(beneficiaryName, iban, amount, currency, reference);

        // Assert
        Assert.NotNull(payload);
        var lines = payload.Split('\n');
        Assert.Equal(11, lines.Length);
        Assert.Empty(lines[10]); // Last line is empty
    }

    [Fact]
    public void BuildForInvoice_HappyPath_UsesVendorAndBuildsPayload()
    {
        // Arrange
        var invoice = new ExtractedInvoice
        {
            Vendor = "Engie Electrabel NV",
            BeneficiaryIban = "BE68539007547034",
            TotalAmount = 123.45m,
            Currency = null,
            PaymentReference = "+++090/9337/55493+++"
        };
        var fallbackBeneficiary = "Fallback Inc";

        // Act
        var payload = EpcPayment.BuildForInvoice(invoice, fallbackBeneficiary);

        // Assert
        Assert.NotNull(payload);
        Assert.Contains("Engie Electrabel NV", payload);
    }

    [Fact]
    public void BuildForInvoice_VendorNull_UsesFallbackBeneficiary()
    {
        // Arrange
        var invoice = new ExtractedInvoice
        {
            Vendor = null,
            BeneficiaryIban = "BE68539007547034",
            TotalAmount = 123.45m,
            Currency = null,
            PaymentReference = "+++090/9337/55493+++"
        };
        var fallbackBeneficiary = "Fallback Inc";

        // Act
        var payload = EpcPayment.BuildForInvoice(invoice, fallbackBeneficiary);

        // Assert
        Assert.NotNull(payload);
        Assert.Contains("Fallback Inc", payload);
    }

    [Fact]
    public void BuildForInvoice_UsesPaymentReferenceWhenPresent()
    {
        // Arrange
        var invoice = new ExtractedInvoice
        {
            Vendor = "Test Vendor",
            BeneficiaryIban = "BE68539007547034",
            TotalAmount = 100m,
            Currency = "EUR",
            PaymentReference = "REF123"
        };
        var fallbackBeneficiary = "Fallback";

        // Act
        var payload = EpcPayment.BuildForInvoice(invoice, fallbackBeneficiary);

        // Assert
        Assert.NotNull(payload);
        Assert.Contains("REF123", payload);
    }

    [Fact]
    public void BuildForInvoice_UsesInvoiceNumberWhenReferenceIsNull()
    {
        // Arrange
        var invoice = new ExtractedInvoice
        {
            Vendor = "Test Vendor",
            BeneficiaryIban = "BE68539007547034",
            TotalAmount = 100m,
            Currency = "EUR",
            PaymentReference = null,
            InvoiceNumber = "INV123"
        };
        var fallbackBeneficiary = "Fallback";

        // Act
        var payload = EpcPayment.BuildForInvoice(invoice, fallbackBeneficiary);

        // Assert
        Assert.NotNull(payload);
        Assert.Contains("INV123", payload);
    }
}
