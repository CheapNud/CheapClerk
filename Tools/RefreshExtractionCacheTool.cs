using System.ComponentModel;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class RefreshExtractionCacheTool
{
    [McpServerTool(Name = "refresh_extraction_cache"), Description("Re-run structured extraction on all documents and update the local cache. Required before find_expiring_documents returns fresh results.")]
    public static async Task<string> RefreshExtractionCache(
        ExtractionCacheService extractionCache,
        [Description("Maximum number of documents to process (default 100).")] int maxDocuments = 100,
        CancellationToken cancellationToken = default)
    {
        var summary = await extractionCache.RefreshAllAsync(maxDocuments, cancellationToken);
        return $"Refreshed extraction cache: {summary.Processed} processed, {summary.Classified} classified, {summary.Skipped} skipped.";
    }
}
