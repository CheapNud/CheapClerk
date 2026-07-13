using System.ComponentModel;
using CheapClerk.Configuration;
using CheapClerk.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class PaperlessStatusTool
{
    [McpServerTool(Name = "paperless_status"), Description("Check whether Paperless-ngx is reachable and report basic taxonomy counts (tags, document types, correspondents).")]
    public static async Task<string> PaperlessStatus(
        PaperlessClient paperlessClient,
        IOptions<PaperlessOptions> paperlessOptions,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = paperlessOptions.Value.BaseUrl;

        try
        {
            var tags = await paperlessClient.GetTagsAsync(cancellationToken);
            var documentTypes = await paperlessClient.GetDocumentTypesAsync(cancellationToken);
            var correspondents = await paperlessClient.GetCorrespondentsAsync(cancellationToken);

            // The client swallows connection failures into empty lists, so zero
            // across the board is indistinguishable from an unreachable server —
            // say so instead of claiming reachability we cannot prove
            if (tags.Count == 0 && documentTypes.Count == 0 && correspondents.Count == 0)
                return $"Paperless at {baseUrl} returned no taxonomy at all — either the archive is empty or the server is unreachable. Check the URL and container logs.";

            return $"Paperless reachable at {baseUrl}: {tags.Count} tags, {documentTypes.Count} document types, {correspondents.Count} correspondents.";
        }
        catch (Exception ex)
        {
            return $"Paperless unreachable at {baseUrl}: {ex.Message}";
        }
    }
}
