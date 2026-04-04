using System.ComponentModel;
using System.Text;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class GetDocumentMetadataTool
{
    [McpServerTool(Name = "get_document_metadata"), Description("Retrieve document metadata without the full text — faster for bulk operations.")]
    public static async Task<string> GetDocumentMetadata(
        PaperlessClient paperlessClient,
        [Description("The Paperless document ID.")] int documentId,
        CancellationToken cancellationToken = default)
    {
        var doc = await paperlessClient.GetDocumentAsync(documentId, cancellationToken);
        if (doc is null)
            return $"Document {documentId} not found.";

        var tagLookup = await paperlessClient.GetTagLookupAsync(cancellationToken);
        var correspondentLookup = await paperlessClient.GetCorrespondentLookupAsync(cancellationToken);

        var sb = new StringBuilder();
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

        return sb.ToString();
    }
}
