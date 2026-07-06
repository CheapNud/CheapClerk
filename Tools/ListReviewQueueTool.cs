using System.ComponentModel;
using System.Text;
using CheapClerk.Models.Classification;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ListReviewQueueTool
{
    [McpServerTool(Name = "list_review_queue"), Description("List documents currently sitting in the review queue, along with any stored AI classification suggestion for each.")]
    public static async Task<string> ListReviewQueue(
        ReviewQueueService reviewQueue,
        CancellationToken cancellationToken = default)
    {
        var queue = await reviewQueue.GetQueueAsync(cancellationToken);

        if (queue.Count == 0)
            return "Review queue is empty.";

        var sb = new StringBuilder();
        foreach (var entry in queue)
            sb.Append(FormatEntry(entry));

        return sb.ToString();
    }

    internal static string FormatEntry(ReviewQueueEntry entry)
    {
        var suggestion = entry.Suggestion;
        var status = suggestion is not null
            ? $"confidence {entry.Confidence * 100:F0}%"
            : "no stored suggestion — run reclassify_document";

        var sb = new StringBuilder();
        sb.AppendLine($"- [{entry.DocumentId}] '{entry.Title}' ({status})");

        if (suggestion is not null)
            AppendSuggestionDetails(sb, suggestion);

        return sb.ToString();
    }

    private static void AppendSuggestionDetails(StringBuilder sb, ClassificationResult suggestion)
    {
        if (suggestion.SuggestedTitle is not null)
            sb.AppendLine($"  Suggested title: {suggestion.SuggestedTitle}");
        if (suggestion.Correspondent is not null)
            sb.AppendLine($"  Correspondent: {suggestion.Correspondent}");
        if (suggestion.DocumentType is not null)
            sb.AppendLine($"  Type: {suggestion.DocumentType}");
        if (suggestion.Tags.Count > 0)
            sb.AppendLine($"  Tags: {string.Join(", ", suggestion.Tags)}");
        if (suggestion.DocumentDate is not null)
            sb.AppendLine($"  Date: {suggestion.DocumentDate}");
    }
}
