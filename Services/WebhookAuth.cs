using System.Security.Cryptography;
using System.Text;

namespace CheapClerk.Services;

public enum WebhookAuthOutcome
{
    NotConfigured,
    Unauthorized,
    Accepted
}

public static class WebhookAuth
{
    public static WebhookAuthOutcome Evaluate(string? configuredToken, string? suppliedToken)
    {
        if (string.IsNullOrWhiteSpace(configuredToken))
            return WebhookAuthOutcome.NotConfigured;

        if (string.IsNullOrEmpty(suppliedToken))
            return WebhookAuthOutcome.Unauthorized;

        var configuredBytes = Encoding.UTF8.GetBytes(configuredToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        return CryptographicOperations.FixedTimeEquals(configuredBytes, suppliedBytes)
            ? WebhookAuthOutcome.Accepted
            : WebhookAuthOutcome.Unauthorized;
    }
}
