using System.ComponentModel;
using System.Text;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ListTagsTool
{
    [McpServerTool(Name = "list_tags"), Description("List all available tags in Paperless-ngx with document counts.")]
    public static async Task<string> ListTags(
        PaperlessClient paperlessClient,
        CancellationToken cancellationToken = default)
    {
        var tags = await paperlessClient.GetTagsAsync(cancellationToken);

        if (tags.Count == 0)
            return "No tags found in Paperless.";

        var sb = new StringBuilder();
        sb.AppendLine($"Found {tags.Count} tag(s):");
        sb.AppendLine();

        foreach (var paperlessTag in tags.OrderByDescending(t => t.DocumentCount))
        {
            sb.AppendLine($"  - {paperlessTag.Name} ({paperlessTag.DocumentCount} documents)");
        }

        return sb.ToString();
    }
}
