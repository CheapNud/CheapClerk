using System.ComponentModel;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ProcessInboxTool
{
    [McpServerTool(Name = "process_inbox"), Description("Classify and file all documents currently carrying the Paperless inbox tag: sets title, correspondent, document type, tags and document date via the configured LLM. Low-confidence documents get a review tag instead.")]
    public static async Task<string> ProcessInbox(
        InboxProcessorService inboxProcessor,
        CancellationToken cancellationToken = default)
    {
        var report = await inboxProcessor.ProcessInboxAsync(cancellationToken);
        if (report.InboxCount == 0)
            return "Inbox is empty — nothing to process.";

        var applied = report.Outcomes.Count(o => o.Applied);
        var review = report.Outcomes.Count(o => o.SentToReview);
        var failed = report.Outcomes.Count(o => o.Error is not null);
        var lines = report.Outcomes.Select(o =>
            $"- [{o.DocumentId}] {(o.Applied ? $"filed as '{o.NewTitle}' [{string.Join(", ", o.AppliedTags)}]" : o.SentToReview ? "sent to review" : $"failed: {o.Error}")}");
        return $"Processed {report.InboxCount} inbox documents: {applied} filed, {review} to review, {failed} failed.\n{string.Join("\n", lines)}";
    }
}
