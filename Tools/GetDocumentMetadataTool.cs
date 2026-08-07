using System.ComponentModel;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class GetDocumentMetadataTool
{
    [McpServerTool(Name = "get_document_metadata"), Description("Retrieve metadata for one or more documents without the full text — faster for bulk operations. Prefer passing several IDs in one call over multiple calls.")]
    public static async Task<string> GetDocumentMetadata(
        PaperlessClient paperlessClient,
        IOptions<WebOptions> webOptions,
        [Description("One or more Paperless document IDs.")] int[] documentIds,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Length == 0)
            return "No document IDs provided.";

        var tagLookup = await paperlessClient.GetTagLookupAsync(cancellationToken);
        var correspondentLookup = await paperlessClient.GetCorrespondentLookupAsync(cancellationToken);

        var sb = new StringBuilder();
        foreach (var documentId in documentIds.Distinct())
        {
            var doc = await paperlessClient.GetDocumentAsync(documentId, cancellationToken);
            if (doc is null)
            {
                sb.AppendLine($"Document {documentId} not found.");
                sb.AppendLine();
                continue;
            }

            sb.AppendLine($"**{doc.Title}**");
            sb.AppendLine();
            sb.AppendLine($"Document ID: {doc.Id}");

            if (doc.CorrespondentId.HasValue && correspondentLookup.TryGetValue(doc.CorrespondentId.Value, out var corrName))
                sb.AppendLine($"Correspondent: {corrName}");

            var docTags = doc.Tags
                .Where(tagLookup.ContainsKey)
                .Select(tagId => tagLookup[tagId])
                .ToList();

            if (docTags.Count > 0)
                sb.AppendLine($"Tags: {string.Join(", ", docTags)}");

            if (doc.Created.HasValue)
                sb.AppendLine($"Created: {doc.Created.Value:yyyy-MM-dd}");

            if (doc.Added.HasValue)
                sb.AppendLine($"Added: {doc.Added.Value:yyyy-MM-dd}");

            if (doc.Modified.HasValue)
                sb.AppendLine($"Modified: {doc.Modified.Value:yyyy-MM-dd}");

            if (doc.ArchiveSerialNumber.HasValue)
                sb.AppendLine($"Archive Serial Number: {doc.ArchiveSerialNumber.Value}");

            if (doc.OriginalFileName is not null)
                sb.AppendLine($"Original Filename: {doc.OriginalFileName}");

            if (DocumentLinkFormatter.Links(webOptions.Value.PublicBaseUrl, doc.Id) is { } docLinks)
                sb.AppendLine(docLinks);

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
