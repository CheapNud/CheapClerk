using System.ComponentModel;
using CheapClerk.Models.Classification;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ApplySuggestionTool
{
    [McpServerTool(Name = "apply_suggestion"), Description("Apply a document's stored classification suggestion (or explicit overrides) and file it in Paperless — title, correspondent, document type, tags and date.")]
    public static async Task<string> ApplySuggestion(
        ReviewQueueService reviewQueue,
        SuggestionStore suggestionStore,
        [Description("The Paperless document id to file.")] int documentId,
        [Description("Override the suggested title.")] string? title = null,
        [Description("Override the suggested correspondent.")] string? correspondent = null,
        [Description("Override the suggested document type.")] string? documentType = null,
        [Description("Override the suggested tags, comma-separated.")] string? tags = null,
        [Description("Override the suggested document date, format yyyy-MM-dd.")] string? documentDate = null,
        CancellationToken cancellationToken = default)
    {
        var stored = await suggestionStore.GetAsync(documentId, cancellationToken);

        var noOverrides = title is null && correspondent is null && documentType is null && tags is null && documentDate is null;
        if (stored is null && noOverrides)
            return $"No stored suggestion for document {documentId} — provide fields or run reclassify_document first.";

        var baseResult = stored?.Classification ?? new ClassificationResult();

        var finalDecision = new ClassificationResult
        {
            SuggestedTitle = title ?? baseResult.SuggestedTitle,
            Correspondent = correspondent ?? baseResult.Correspondent,
            DocumentType = documentType ?? baseResult.DocumentType,
            Tags = tags is not null
                ? tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList()
                : baseResult.Tags,
            DocumentDate = documentDate ?? baseResult.DocumentDate,
            Confidence = baseResult.Confidence
        };

        var outcome = await reviewQueue.ApplyAsync(documentId, finalDecision, cancellationToken);

        return outcome.Applied
            ? $"Filed as '{outcome.NewTitle}' with tags [{string.Join(", ", outcome.AppliedTags)}]"
            : outcome.Error ?? "Apply failed for an unknown reason.";
    }
}
