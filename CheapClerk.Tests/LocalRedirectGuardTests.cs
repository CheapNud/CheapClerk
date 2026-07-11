using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class LocalRedirectGuardTests
{
    [Theory]
    [InlineData("/documents", "/documents")]
    [InlineData("/documents/4?tab=text", "/documents/4?tab=text")]
    [InlineData("/", "/")]
    public void Sanitize_PassesLocalPaths(string requested, string expected)
    {
        Assert.Equal(expected, LocalRedirectGuard.Sanitize(requested));
    }

    [Theory]
    [InlineData("//evil.example")]
    [InlineData(@"/\evil.example")]
    [InlineData("https://evil.example/")]
    [InlineData("http://192.168.1.12:5030/")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("documents")]
    public void Sanitize_RejectsNonLocalTargets(string? requested)
    {
        Assert.Equal("/", LocalRedirectGuard.Sanitize(requested));
    }
}
