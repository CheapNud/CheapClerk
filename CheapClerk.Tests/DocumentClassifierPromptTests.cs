using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class DocumentClassifierPromptTests
{
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
