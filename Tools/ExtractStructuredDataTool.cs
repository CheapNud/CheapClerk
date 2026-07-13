using System.ComponentModel;
using System.Text;
using System.Text.Json;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ExtractStructuredDataTool
{
    private static readonly JsonSerializerOptions SerializerSettings = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    [McpServerTool(Name = "extract_structured_data"), Description("Classify a document and extract structured fields (invoice, insurance, contract). Uses Claude for analysis. The result is cached, so find_expiring_documents and get_payment_details see it immediately.")]
    public static async Task<string> ExtractStructuredData(
        StructuredExtractionService structuredExtractionService,
        ExtractionCacheService extractionCache,
        [Description("The Paperless document ID to analyze.")] int documentId,
        CancellationToken cancellationToken = default)
    {
        if (!structuredExtractionService.IsEnabled)
            return "Structured extraction is disabled. Configure the Llm provider in appsettings.json.";

        // Runs through the cache (content fetch + vision fallback + persistence)
        // so downstream tools see the result — extraction that evaporates was a bug
        var extracted = await extractionCache.GetOrExtractAsync(documentId, forceRefresh: true, cancellationToken);
        if (extracted is null)
            return $"Failed to extract structured data from document {documentId} (no readable text or extraction failed).";

        var sb = new StringBuilder();
        sb.AppendLine($"**Document {documentId} — {extracted.Category}**");
        sb.AppendLine($"Confidence: {extracted.Confidence:P0}");

        if (extracted.Summary is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"Summary: {extracted.Summary}");
        }

        sb.AppendLine();
        sb.AppendLine("```json");
        sb.AppendLine(JsonSerializer.Serialize(extracted, SerializerSettings));
        sb.AppendLine("```");

        return sb.ToString();
    }
}
