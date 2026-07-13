using System.ComponentModel;
using System.Text;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ListDocumentTypesTool
{
    [McpServerTool(Name = "list_document_types"), Description("List all available document types in Paperless-ngx with document counts.")]
    public static async Task<string> ListDocumentTypes(
        PaperlessClient paperlessClient,
        CancellationToken cancellationToken = default)
    {
        var documentTypes = await paperlessClient.GetDocumentTypesAsync(cancellationToken);

        if (documentTypes.Count == 0)
            return "No document types found in Paperless.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {documentTypes.Count} document type(s):");
        sb.AppendLine();

        foreach (var documentType in documentTypes.OrderByDescending(dt => dt.DocumentCount))
        {
            sb.AppendLine($"  - {documentType.Name} ({documentType.DocumentCount} documents)");
        }

        return sb.ToString();
    }
}
