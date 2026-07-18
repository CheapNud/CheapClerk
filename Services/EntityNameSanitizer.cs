namespace CheapClerk.Services;

/// <summary>
/// Cleans LLM-suggested entity names before they are matched or created in
/// Paperless — anything created here feeds back into every future
/// classification prompt, so newlines and runaway lengths must not survive.
/// </summary>
public static class EntityNameSanitizer
{
    public const int MaxNameLength = 100;

    public static string Clean(string rawName)
    {
        // Split on any whitespace (incl. newlines/tabs) and rejoin with single
        // spaces — strips control characters and collapses runs in one pass
        var collapsed = string.Join(' ', rawName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length > MaxNameLength ? collapsed[..MaxNameLength].TrimEnd() : collapsed;
    }
}
