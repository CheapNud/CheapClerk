using System.ComponentModel;
using System.Text;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class FindExpiringDocumentsTool
{
    [McpServerTool(Name = "find_expiring_documents"), Description("Find documents (insurance, contracts, invoices) expiring within the next N days. Reads from the local extraction cache — call refresh_extraction_cache first to populate.")]
    public static async Task<string> FindExpiringDocuments(
        ExtractionCacheService extractionCache,
        [Description("Days ahead to check (default 30).")] int daysAhead = 30,
        CancellationToken cancellationToken = default)
    {
        var expiring = await extractionCache.FindExpiringAsync(daysAhead, cancellationToken);

        if (expiring.Count == 0)
            return $"No documents expiring within the next {daysAhead} days.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {expiring.Count} document(s) expiring within {daysAhead} days:");
        sb.AppendLine();

        foreach (var expiringDoc in expiring)
        {
            sb.AppendLine($"**[{expiringDoc.DocumentId}] {expiringDoc.Category}** — expires {expiringDoc.ExpiryDate:yyyy-MM-dd} ({expiringDoc.DaysRemaining} days)");
            if (expiringDoc.Summary is not null)
                sb.AppendLine($"  {expiringDoc.Summary}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
