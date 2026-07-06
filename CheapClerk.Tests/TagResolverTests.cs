using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class TagResolverTests
{
    private static readonly Dictionary<int, string> Existing = new()
    {
        [1] = "Insurance",
        [2] = "Utilities",
        [3] = "KBC"
    };

    [Fact]
    public void Resolve_MatchesCaseInsensitive_AndReportsMissing()
    {
        var (matchedIds, missingNames) = TagResolver.Resolve(
            ["insurance", "Home", "kbc"], Existing, maxTags: 4);

        Assert.Equal([1, 3], matchedIds);
        Assert.Equal(["Home"], missingNames);
    }

    [Fact]
    public void Resolve_CapsAtMaxTags_MatchedFirst()
    {
        var (matchedIds, missingNames) = TagResolver.Resolve(
            ["Insurance", "Utilities", "KBC", "Home", "Car"], Existing, maxTags: 4);

        Assert.Equal(3, matchedIds.Count);
        Assert.Single(missingNames);          // only one slot left after 3 matches
        Assert.Equal("Home", missingNames[0]);
    }

    [Fact]
    public void Resolve_IgnoresBlankAndDuplicateSuggestions()
    {
        var (matchedIds, missingNames) = TagResolver.Resolve(
            ["Insurance", " ", "INSURANCE", ""], Existing, maxTags: 4);

        Assert.Equal([1], matchedIds);
        Assert.Empty(missingNames);
    }

    [Fact]
    public void Resolve_PrefersMatchesOverMisses_WhenMissesComeFirst()
    {
        var (matchedIds, missingNames) = TagResolver.Resolve(
            ["Home", "Car", "Insurance", "Utilities", "KBC"], Existing, maxTags: 4);

        Assert.Equal([1, 2, 3], matchedIds);
        Assert.Equal(["Home"], missingNames);
    }
}
