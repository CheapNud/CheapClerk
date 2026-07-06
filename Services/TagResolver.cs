namespace CheapClerk.Services;

public static class TagResolver
{
    public static (List<int> MatchedIds, List<string> MissingNames) Resolve(
        List<string> suggestedNames,
        Dictionary<int, string> existingLookup,
        int maxTags)
    {
        List<int> allMatchedIds = [];
        List<string> allMissingNames = [];
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);

        foreach (var suggestedName in suggestedNames
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .Select(n => n.Trim()))
        {
            if (!seenNames.Add(suggestedName)) continue;

            var existingId = existingLookup
                .FirstOrDefault(t => t.Value.Equals(suggestedName, StringComparison.OrdinalIgnoreCase)).Key;

            if (existingId > 0)
                allMatchedIds.Add(existingId);
            else
                allMissingNames.Add(suggestedName);
        }

        var matchedIds = allMatchedIds.Take(maxTags).ToList();
        var missingNames = allMissingNames.Take(maxTags - matchedIds.Count).ToList();

        return (matchedIds, missingNames);
    }
}
