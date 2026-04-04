namespace CheapClerk.Configuration;

public sealed class VisionFallbackOptions
{
    public const string SectionName = "VisionFallback";

    public bool Enabled { get; set; } = true;
    public int MinTextLength { get; set; } = 50;
    public double MaxGarbageRatio { get; set; } = 0.15;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-20250514";
}
