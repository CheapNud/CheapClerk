namespace CheapClerk.Services;

public static class DocumentLinkFormatter
{
    /// <summary>
    /// Two links per document, Kreuzakt-style: a viewer page safe to hand a
    /// person, and the raw file for direct opening. Null when no public base
    /// URL is configured.
    /// </summary>
    public static string? Links(string? publicBaseUrl, int documentId)
    {
        if (string.IsNullOrWhiteSpace(publicBaseUrl)) return null;
        var trimmedBase = publicBaseUrl.TrimEnd('/');
        return $"View: {trimmedBase}/documents/{documentId} | File: {trimmedBase}/documents/{documentId}/file";
    }
}
