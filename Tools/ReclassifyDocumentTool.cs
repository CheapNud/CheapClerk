using System.ComponentModel;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ReclassifyDocumentTool
{
    [McpServerTool(Name = "reclassify_document"), Description("Re-run AI classification for a document in the review queue, refreshing its stored suggestion.")]
    public static async Task<string> ReclassifyDocument(
        ReviewQueueService reviewQueue,
        [Description("The Paperless document id to reclassify.")] int documentId,
        [Description("Force vision-based OCR even if the extracted text looks fine.")] bool forceVisionOcr = false,
        CancellationToken cancellationToken = default)
    {
        var (entry, error) = await reviewQueue.ReclassifyAsync(documentId, forceVisionOcr, cancellationToken);

        if (error is not null)
            return error;

        return ListReviewQueueTool.FormatEntry(entry!);
    }
}
