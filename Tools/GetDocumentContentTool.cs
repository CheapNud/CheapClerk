using System.ComponentModel;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class GetDocumentContentTool
{
    [McpServerTool(Name = "get_document_content"), Description("Retrieve the full OCR text of one or more documents. Falls back to Vision OCR if text quality is poor. Prefer passing several IDs in one call over multiple calls.")]
    public static async Task<string> GetDocumentContent(
        PaperlessClient paperlessClient,
        OcrQualityChecker ocrQualityChecker,
        VisionOcrService visionOcrService,
        IOptions<WebOptions> webOptions,
        [Description("One or more Paperless document IDs.")] int[] documentIds,
        [Description("Force Vision OCR even if Tesseract text looks acceptable.")] bool forceVisionOcr = false,
        CancellationToken cancellationToken = default)
    {
        if (documentIds.Length == 0)
            return "No document IDs provided.";

        var distinctIds = documentIds.Distinct().ToList();
        var sb = new StringBuilder();

        foreach (var documentId in distinctIds)
        {
            // Single-document calls keep the historical bare-text shape;
            // bulk calls get a header per document so the caller can tell them apart
            if (distinctIds.Count > 1)
            {
                sb.AppendLine($"=== Document {documentId} ===");
                if (DocumentLinkFormatter.Links(webOptions.Value.PublicBaseUrl, documentId) is { } docLinks)
                    sb.AppendLine(docLinks);
                sb.AppendLine();
            }

            sb.AppendLine(await ResolveTextAsync(
                paperlessClient, ocrQualityChecker, visionOcrService, documentId, forceVisionOcr, cancellationToken));
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    private static async Task<string> ResolveTextAsync(
        PaperlessClient paperlessClient,
        OcrQualityChecker ocrQualityChecker,
        VisionOcrService visionOcrService,
        int documentId,
        bool forceVisionOcr,
        CancellationToken cancellationToken)
    {
        var ocrText = await paperlessClient.GetDocumentContentAsync(documentId, cancellationToken);

        if (!forceVisionOcr && !ocrQualityChecker.IsOcrQualitySuspect(ocrText))
            return ocrText!;

        if (!visionOcrService.IsEnabled)
        {
            return string.IsNullOrWhiteSpace(ocrText)
                ? $"No text content available for document {documentId}. Vision OCR fallback is disabled."
                : $"[Low quality OCR — Vision fallback disabled]\n\n{ocrText}";
        }

        var originalBytes = await paperlessClient.DownloadOriginalAsync(documentId, cancellationToken);
        if (originalBytes is null)
        {
            return string.IsNullOrWhiteSpace(ocrText)
                ? $"Failed to retrieve document {documentId}."
                : $"[Low quality OCR — original download failed]\n\n{ocrText}";
        }

        var visionText = await visionOcrService.ExtractTextFromImageAsync(originalBytes, cancellationToken: cancellationToken);
        if (visionText is not null)
            return visionText;

        return string.IsNullOrWhiteSpace(ocrText)
            ? $"Failed to extract text from document {documentId}."
            : $"[Vision OCR failed — showing Tesseract output]\n\n{ocrText}";
    }
}
