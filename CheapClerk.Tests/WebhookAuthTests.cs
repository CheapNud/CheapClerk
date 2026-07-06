using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class WebhookAuthTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_NoTokenConfigured_ReturnsNotConfigured(string? configured)
    {
        Assert.Equal(WebhookAuthOutcome.NotConfigured, WebhookAuth.Evaluate(configured, "anything"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-token")]
    [InlineData("secret-tokeN")]
    public void Evaluate_WrongOrMissingToken_ReturnsUnauthorized(string? supplied)
    {
        Assert.Equal(WebhookAuthOutcome.Unauthorized, WebhookAuth.Evaluate("secret-token", supplied));
    }

    [Fact]
    public void Evaluate_MatchingToken_ReturnsAccepted()
    {
        Assert.Equal(WebhookAuthOutcome.Accepted, WebhookAuth.Evaluate("secret-token", "secret-token"));
    }
}
