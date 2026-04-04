namespace CheapClerk.Configuration;

public sealed class PaperlessOptions
{
    public const string SectionName = "Paperless";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string ApiToken { get; set; } = string.Empty;
}
