using System.ComponentModel;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class GetDocumentContentTool
{
    [McpServerTool(Name = "get_document_content"), Description("Retrieve the full OCR text of a specific document. Falls back to Vision OCR if text quality is poor.")]
    public static async Task<string> GetDocumentContent(
        PaperlessClient paperlessClient,
        OcrQualityChecker ocrQualityChecker,
        VisionOcrService visionOcrService,
        [Description("The Paperless document ID.")] int documentId,
        [Description("Force Vision OCR even if Tesseract text looks acceptable.")] bool forceVisionOcr = false,
        CancellationToken cancellationToken = default)
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
