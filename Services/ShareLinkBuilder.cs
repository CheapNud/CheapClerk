using System.Security.Cryptography;
using System.Text;

namespace CheapClerk.Services;

/// <summary>
/// Stateless SAS-style share tokens: HMAC-SHA256 over "docId|generation|expiry".
/// Pure functions — validation state (the generation) lives in the cache DB.
/// </summary>
public static class ShareLinkBuilder
{
    public static string CreateToken(string signingKey, int documentId, int generation, DateTimeOffset expiresUtc)
    {
        var payload = $"{documentId}|{generation}|{expiresUtc.ToUnixTimeSeconds()}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), payloadBytes);
        return $"{Base64Url(payloadBytes)}.{Base64Url(signature)}";
    }

    public static (int DocumentId, int Generation, DateTimeOffset ExpiresUtc)? TryValidate(
        string signingKey, string token, DateTimeOffset now)
    {
        var parts = token.Split('.');
        if (parts.Length != 2) return null;

        byte[] payloadBytes;
        byte[] givenSignature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            givenSignature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expectedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, givenSignature)) return null;

        var payloadParts = Encoding.UTF8.GetString(payloadBytes).Split('|');
        if (payloadParts.Length != 3
            || !int.TryParse(payloadParts[0], out var documentId)
            || !int.TryParse(payloadParts[1], out var generation)
            || !long.TryParse(payloadParts[2], out var expiryUnix))
        {
            return null;
        }

        var expiresUtc = DateTimeOffset.FromUnixTimeSeconds(expiryUnix);
        if (expiresUtc <= now) return null;

        return (documentId, generation, expiresUtc);
    }

    private static string Base64Url(byte[] raw) =>
        Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string encoded)
    {
        var padded = encoded.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}
