namespace CheapClerk.Services;

public static class TagResolver
{
    public static (List<int> MatchedIds, List<string> MissingNames) Resolve(
        List<string> suggestedNames,
        Dictionary<int, string> existingLookup,
        int maxTags)
    {
        List<int> matchedIds = [];
        List<string> missingNames = [];
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (var suggestedName in suggestedNames
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .Select(n => n.Trim()))
        {
            if (!seenNames.Add(suggestedName)) continue;
            if (matchedIds.Count + missingNames.Count >= maxTags) break;

            var existingId = existingLookup
                .FirstOrDefault(t => t.Value.Equals(suggestedName, StringComparison.OrdinalIgnoreCase)).Key;

            if (existingId > 0)
                matchedIds.Add(existingId);
            else
                missingNames.Add(suggestedName);
        }

        return (matchedIds, missingNames);
    }
}
