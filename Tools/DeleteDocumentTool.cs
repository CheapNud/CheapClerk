using System.ComponentModel;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class DeleteDocumentTool
{
    [McpServerTool(Name = "delete_document"), Description("Permanently delete a document from Paperless-ngx. Requires confirm: true to actually delete.")]
    public static async Task<string> DeleteDocument(
        PaperlessClient paperlessClient,
        [Description("The Paperless document id to delete.")] int documentId,
        [Description("Must be true to actually delete; false just previews what would be deleted.")] bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var document = await paperlessClient.GetDocumentAsync(documentId, cancellationToken);
        if (document is null)
            return $"Document {documentId} not found.";

        if (!confirm)
            return $"Refusing to delete '{document.Title}' (#{documentId}) — call again with confirm: true.";

        var deleted = await paperlessClient.DeleteDocumentAsync(documentId, cancellationToken);
        if (!deleted)
            return $"Failed to delete '{document.Title}' (#{documentId}).";

        return $"'{document.Title}' (#{documentId}) deleted.";
    }
}
