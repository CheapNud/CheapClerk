namespace CheapClerk.Configuration;

public sealed class WebOptions
{
    public const string SectionName = "Web";

    /// <summary>
    /// Public base URL of the CheapClerk web UI (e.g. https://clerk.example.com).
    /// When set, MCP tools append clickable viewer/file links to every document
    /// they return, so an AI assistant can hand the user the document, not just
    /// talk about it. Unset: links are omitted.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
