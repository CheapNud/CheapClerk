using System.ComponentModel;
using System.Text;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ListDocumentsTool
{
    [McpServerTool(Name = "list_documents"), Description("Browse documents with optional filters by correspondent, tag, or date range.")]
    public static async Task<string> ListDocuments(
        PaperlessClient paperlessClient,
        [Description("Filter by correspondent name.")] string? correspondent = null,
        [Description("Filter by tag name.")] string? tag = null,
        [Description("Only show documents added after this date (yyyy-MM-dd).")] DateTime? addedAfter = null,
        [Description("Only show documents added before this date (yyyy-MM-dd).")] DateTime? addedBefore = null,
        [Description("Maximum number of results to return.")] int maxResults = 25,
        CancellationToken cancellationToken = default)
    {
        var documents = await paperlessClient.ListDocumentsAsync(
            correspondent, tag, addedAfter, addedBefore, maxResults, cancellationToken);

        if (documents.Count == 0)
            return "No documents found matching the filters.";

        var tagLookup = await paperlessClient.GetTagLookupAsync(cancellationToken);
        var correspondentLookup = await paperlessClient.GetCorrespondentLookupAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine($"Found {documents.Count} document(s):");
        sb.AppendLine();

        foreach (var doc in documents)
        {
            sb.AppendLine($"**[{doc.Id}] {doc.Title}**");

            if (doc.CorrespondentId.HasValue && correspondentLookup.TryGetValue(doc.CorrespondentId.Value, out var corrName))
                sb.AppendLine($"  Correspondent: {corrName}");

            var docTags = doc.Tags
                .Where(tagLookup.ContainsKey)
                .Select(tagId => tagLookup[tagId])
                .ToList();

            if (docTags.Count > 0)
                sb.AppendLine($"  Tags: {string.Join(", ", docTags)}");

            if (doc.Created.HasValue)
                sb.AppendLine($"  Created: {doc.Created.Value:yyyy-MM-dd}");

            if (doc.Added.HasValue)
                sb.AppendLine($"  Added: {doc.Added.Value:yyyy-MM-dd}");

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
