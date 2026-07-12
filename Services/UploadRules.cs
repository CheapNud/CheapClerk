namespace CheapClerk.Services;

public static class UploadRules
{
    public static readonly string[] UploadableExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".tiff"];
    public const long MaxUploadBytes = 50 * 1024 * 1024;

    /// <summary>Null when acceptable; otherwise a short human-readable rejection reason.</summary>
    public static string? Validate(string fileName, long fileSize)
    {
        var fileExtension = Path.GetExtension(fileName);
        if (!UploadableExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase))
            return $"unsupported file type '{fileExtension}'";
        if (fileSize <= 0)
            return "file is empty";
        if (fileSize > MaxUploadBytes)
            return $"file exceeds the {MaxUploadBytes / (1024 * 1024)} MB limit";
        return null;
    }
}
