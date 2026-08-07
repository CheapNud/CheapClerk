using System.ComponentModel;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class SearchDocumentsTool
{
    [McpServerTool(Name = "search_documents"), Description("Full-text search across all ingested documents in Paperless-ngx. Results include viewer/file links when the web UI URL is configured.")]
    public static async Task<string> SearchDocuments(
        PaperlessClient paperlessClient,
        IOptions<WebOptions> webOptions,
        [Description("The search query to find documents.")] string query,
        [Description("Optional tag name to filter results.")] string? tag = null,
        [Description("Optional correspondent name to filter results.")] string? correspondent = null,
        [Description("Maximum number of results to return.")] int maxResults = 10,
        [Description("Optional document type name to filter results.")] string? documentType = null,
        CancellationToken cancellationToken = default)
    {
        var matches = await paperlessClient.SearchDocumentsAsync(query, tag, correspondent, maxResults, documentType, cancellationToken);

        if (matches.Count == 0)
            return "No documents found matching the query.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {matches.Count} document(s):");
        sb.AppendLine();

        foreach (var match in matches)
        {
            sb.AppendLine($"**[{match.DocumentId}] {match.Title}**");

            if (match.Correspondent is not null)
                sb.AppendLine($"  Correspondent: {match.Correspondent}");

            if (match.Tags.Count > 0)
                sb.AppendLine($"  Tags: {string.Join(", ", match.Tags)}");

            if (match.Created.HasValue)
                sb.AppendLine($"  Date: {match.Created.Value:yyyy-MM-dd}");

            if (match.Excerpt is not null)
                sb.AppendLine($"  Excerpt: {match.Excerpt}");

            if (DocumentLinkFormatter.Links(webOptions.Value.PublicBaseUrl, match.DocumentId) is { } docLinks)
                sb.AppendLine($"  {docLinks}");

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
