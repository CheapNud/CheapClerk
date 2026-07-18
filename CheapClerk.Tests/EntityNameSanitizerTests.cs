using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class EntityNameSanitizerTests
{
    [Theory]
    [InlineData("Engie Electrabel", "Engie Electrabel")]
    [InlineData("  Engie\nElectrabel\t NV ", "Engie Electrabel NV")]
    [InlineData("Multi   space", "Multi space")]
    public void Clean_StripsControlWhitespace_AndCollapsesRuns(string raw, string expected) =>
        Assert.Equal(expected, EntityNameSanitizer.Clean(raw));

    [Fact]
    public void Clean_CapsLengthAt100()
    {
        var cleaned = EntityNameSanitizer.Clean(new string('x', 300));

        Assert.Equal(100, cleaned.Length);
    }

    [Fact]
    public void Clean_TrimsTrailingSpaceAfterCap()
    {
        var raw = new string('x', 99) + " yyyyy";

        var cleaned = EntityNameSanitizer.Clean(raw);

        Assert.False(cleaned.EndsWith(' '));
        Assert.True(cleaned.Length <= 100);
    }
}
