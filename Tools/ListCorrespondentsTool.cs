using System.ComponentModel;
using System.Text;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ListCorrespondentsTool
{
    [McpServerTool(Name = "list_correspondents"), Description("List all available correspondents in Paperless-ngx with document counts.")]
    public static async Task<string> ListCorrespondents(
        PaperlessClient paperlessClient,
        CancellationToken cancellationToken = default)
    {
        var correspondents = await paperlessClient.GetCorrespondentsAsync(cancellationToken);

        if (correspondents.Count == 0)
            return "No correspondents found in Paperless.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {correspondents.Count} correspondent(s):");
        sb.AppendLine();

        foreach (var correspondent in correspondents.OrderByDescending(c => c.DocumentCount))
        {
            sb.AppendLine($"  - {correspondent.Name} ({correspondent.DocumentCount} documents)");
        }

        return sb.ToString();
    }
}
