using System.ComponentModel;
using CheapClerk.Configuration;
using CheapClerk.Models;
using CheapClerk.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class UpdateDocumentTool
{
    [McpServerTool(Name = "update_document"), Description("Update a document's title, tags, correspondent, or document type in Paperless-ngx. Tags are matched by canonical name and replace the existing set, except the Inbox/Needs Review workflow tags which are always preserved.")]
    public static async Task<string> UpdateDocument(
        PaperlessClient paperlessClient,
        IOptions<ClassificationOptions> classificationOptions,
        [Description("The Paperless document id to update.")] int documentId,
        [Description("New title for the document.")] string? title = null,
        [Description("Comma-separated canonical tag names to set on the document (replaces existing tags; workflow tags are preserved automatically).")] string? tags = null,
        [Description("Canonical correspondent name to set on the document.")] string? correspondent = null,
        [Description("Canonical document type name to set on the document.")] string? documentType = null,
        CancellationToken cancellationToken = default)
    {
        if (title is null && tags is null && correspondent is null && documentType is null)
            return "Provide at least one of: title, tags, correspondent, documentType.";

        var document = await paperlessClient.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
            return $"Document {documentId} not found.";

        var update = new DocumentUpdate();
        var changed = new List<string>();

        if (title is not null)
        {
            update.Title = title;
            changed.Add("title");
        }

        if (tags is not null)
        {
            var tagLookup = await paperlessClient.GetTagLookupAsync(cancellationToken);
            var names = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var resolvedIds = new List<int>();

            foreach (var name in names)
            {
                var match = tagLookup.FirstOrDefault(t => t.Value.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (match.Key <= 0)
                    return $"Unknown tag '{name}'. Available tags: {string.Join(", ", tagLookup.Values.OrderBy(v => v))}";

                resolvedIds.Add(match.Key);
            }

            var workflowTagNames = new[] { classificationOptions.Value.InboxTagName, classificationOptions.Value.ReviewTagName };
            var preservedIds = document.Tags
                .Where(tagId => tagLookup.TryGetValue(tagId, out var tagName)
                    && workflowTagNames.Any(w => w.Equals(tagName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            update.TagIds = resolvedIds.Concat(preservedIds).Distinct().ToList();
            changed.Add($"tags ({string.Join(", ", names)})");
        }

        if (correspondent is not null)
        {
            var correspondentLookup = await paperlessClient.GetCorrespondentLookupAsync(cancellationToken);
            var match = correspondentLookup.FirstOrDefault(c => c.Value.Equals(correspondent, StringComparison.OrdinalIgnoreCase));
            if (match.Key <= 0)
                return $"Unknown correspondent '{correspondent}'. Available correspondents: {string.Join(", ", correspondentLookup.Values.OrderBy(v => v))}";

            update.CorrespondentId = match.Key;
            changed.Add($"correspondent ({correspondent})");
        }

        if (documentType is not null)
        {
            var documentTypeLookup = await paperlessClient.GetDocumentTypeLookupAsync(cancellationToken);
            var match = documentTypeLookup.FirstOrDefault(dt => dt.Value.Equals(documentType, StringComparison.OrdinalIgnoreCase));
            if (match.Key <= 0)
                return $"Unknown document type '{documentType}'. Available document types: {string.Join(", ", documentTypeLookup.Values.OrderBy(v => v))}";

            update.DocumentTypeId = match.Key;
            changed.Add($"documentType ({documentType})");
        }

        var updated = await paperlessClient.UpdateDocumentAsync(documentId, update, cancellationToken);
        if (!updated)
            return $"Failed to update document {documentId}.";

        return $"Updated document {documentId}: {string.Join(", ", changed)}.";
    }
}
