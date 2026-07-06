namespace CheapClerk.Configuration;

public sealed class ClassificationOptions
{
    public const string SectionName = "Classification";

    public bool Enabled { get; set; } = true;
    public string InboxTagName { get; set; } = "Inbox";
    public string ReviewTagName { get; set; } = "Needs Review";
    public double MinConfidence { get; set; } = 0.6;
    public int PollIntervalMinutes { get; set; } = 15;
    public int MaxTagsPerDocument { get; set; } = 4;
    public bool AutoCreateTags { get; set; } = true;
    public int MaxDocumentsPerRun { get; set; } = 20;
}
