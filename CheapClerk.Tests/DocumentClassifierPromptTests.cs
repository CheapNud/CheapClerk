using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class DocumentClassifierPromptTests
{
    [Fact]
    public void BuildTaxonomyMessage_WithExtractionContext_QuotesFindingsAndAsksForConsistency()
    {
        var extracted = new CheapClerk.Models.Extraction.ExtractionResult
        {
            Category = CheapClerk.Models.Extraction.DocumentCategory.Insurance,
            Confidence = 0.95,
            Summary = "Accident report filed with Baloise",
            Insurance = new CheapClerk.Models.Extraction.ExtractedInsurance { Insurer = "BALOISE", PolicyType = "Motor vehicle liability" }
        };
        var extractionContext = DocumentClassifierService.BuildExtractionContext(extracted);

        var message = DocumentClassifierService.BuildTaxonomyMessage(
            "aanrijdingsformulier", [], [], [], extractionContext);

        Assert.Contains("Motor vehicle liability", message);
        Assert.Contains("BALOISE", message);
        Assert.Contains("CONSISTENT", message);
    }

    [Fact]
    public void BuildTaxonomyMessage_WithoutExtractionContext_OmitsConsistencyBlock()
    {
        var message = DocumentClassifierService.BuildTaxonomyMessage("text", [], [], []);

        Assert.DoesNotContain("CONSISTENT", message);
    }

    [Fact]
    public void ResolveExpiryDate_VehicleCategory_UsesInspectionDueDate()
    {
        var extracted = new CheapClerk.Models.Extraction.ExtractionResult
        {
            Category = CheapClerk.Models.Extraction.DocumentCategory.Vehicle,
            Confidence = 0.9,
            Vehicle = new CheapClerk.Models.Extraction.ExtractedVehicle { InspectionDueDate = "2027-03-15" }
        };

        var resolved = ExtractionCacheService.ResolveExpiryDate(extracted);

        Assert.Equal(new DateTime(2027, 3, 15, 0, 0, 0, DateTimeKind.Utc), resolved);
    }

    [Fact]
    public void BuildTaxonomyMessage_ListsExistingEntities_AndDocumentText()
    {
        var message = DocumentClassifierService.BuildTaxonomyMessage(
            "Factuur Engie oktober 2026",
            existingTags: ["Utilities", "Insurance"],
            existingCorrespondents: ["Engie", "KBC"],
            existingDocumentTypes: ["Invoice"]);

        Assert.Contains("Utilities, Insurance", message);
        Assert.Contains("Engie, KBC", message);
        Assert.Contains("Invoice", message);
        Assert.Contains("Factuur Engie oktober 2026", message);
    }

    [Fact]
    public void BuildTaxonomyMessage_TruncatesVeryLongDocuments()
    {
        var longText = new string('x', 20_000);

        var message = DocumentClassifierService.BuildTaxonomyMessage(longText, [], [], []);

        Assert.True(message.Length < 12_000);
        Assert.Contains("[truncated]", message);
    }

    [Fact]
    public void BuildSystemPrompt_Dutch_MentionsDutchTagCoinage()
    {
        var prompt = DocumentClassifierService.BuildSystemPrompt("nl");

        Assert.Contains("reusable Dutch tag (like 'Belastingen' or 'Pensioen')", prompt);
        Assert.DoesNotContain("English tag", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_English_MentionsEnglishTagCoinage()
    {
        var prompt = DocumentClassifierService.BuildSystemPrompt("en");

        Assert.Contains("reusable English tag (like 'Taxes' or 'Pension')", prompt);
        Assert.DoesNotContain("Dutch tag", prompt);
    }
}
