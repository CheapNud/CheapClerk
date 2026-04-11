namespace CheapClerk.Configuration;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public string DatabasePath { get; set; } = "cheapclerk.db";
}
