using System.ComponentModel;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class UploadDocumentTool
{
    [McpServerTool(Name = "upload_document"), Description("Upload a local file (pdf or image) into the Paperless archive. Consumption is tracked briefly; after consumption the clerk auto-classifies and files it.")]
    public static async Task<string> UploadDocument(
        UploadTracker uploadTracker,
        [Description("Absolute path of the file to upload.")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        var fileName = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);
        var rejection = UploadRules.Validate(fileName, fileInfo.Length);
        if (rejection is not null)
            return $"Cannot upload {fileName}: {rejection}";

        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var outcome = await uploadTracker.UploadAndTrackAsync(fileBytes, fileName, TimeSpan.FromSeconds(30), cancellationToken);

        return outcome.Kind switch
        {
            UploadOutcomeKind.Consumed => $"Uploaded and consumed{(outcome.DocumentId is not null ? $" as document {outcome.DocumentId}" : "")} — the clerk will classify and file it within seconds.",
            UploadOutcomeKind.Failed => $"Paperless rejected {fileName}: {outcome.Detail}",
            UploadOutcomeKind.StillProcessing => $"Uploaded {fileName}; {outcome.Detail}",
            _ => $"Upload of {fileName} failed: {outcome.Detail}"
        };
    }
}
