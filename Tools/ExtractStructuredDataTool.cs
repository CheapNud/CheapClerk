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

    [McpServerTool(Name = "extract_structured_data"), Description("Classify a document and extract structured fields (invoice, insurance, contract). Uses Claude for analysis.")]
    public static async Task<string> ExtractStructuredData(
        PaperlessClient paperlessClient,
        OcrQualityChecker ocrQualityChecker,
        VisionOcrService visionOcrService,
        StructuredExtractionService structuredExtractionService,
        [Description("The Paperless document ID to analyze.")] int documentId,
        CancellationToken cancellationToken = default)
    {
        if (!structuredExtractionService.IsEnabled)
            return "Structured extraction is disabled. Configure VisionFallback.ApiKey in appsettings.json.";

        var ocrText = await paperlessClient.GetDocumentContentAsync(documentId, cancellationToken);

        if (ocrQualityChecker.IsOcrQualitySuspect(ocrText) && visionOcrService.IsEnabled)
        {
            var originalBytes = await paperlessClient.DownloadOriginalAsync(documentId, cancellationToken);
            if (originalBytes is not null)
            {
                var visionText = await visionOcrService.ExtractTextFromImageAsync(
                    originalBytes,
                    cancellationToken: cancellationToken);
                if (visionText is not null)
                    ocrText = visionText;
            }
        }

        if (string.IsNullOrWhiteSpace(ocrText))
            return $"Document {documentId} has no text content to analyze.";

        var extracted = await structuredExtractionService.ExtractAsync(ocrText, cancellationToken);
        if (extracted is null)
            return $"Failed to extract structured data from document {documentId}.";

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
