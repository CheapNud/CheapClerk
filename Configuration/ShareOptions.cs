namespace CheapClerk.Configuration;

public sealed class ShareOptions
{
    public const string SectionName = "Share";

    /// <summary>
    /// HMAC key for time-limited public share links (SAS-style). Both hosts mint
    /// and validate with the same key from config, so links created by the MCP
    /// server work on the web app. Unset: sharing is dark — /share/* returns 404
    /// and the tools refuse.
    /// </summary>
    public string? SigningKey { get; set; }
}
