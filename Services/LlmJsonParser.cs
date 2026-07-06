using System.Text.Json;

namespace CheapClerk.Services;

public static class LlmJsonParser
{
    private static readonly JsonSerializerOptions LenientSettings = new(JsonSerializerDefaults.Web);

    public static bool TryParse<T>(string? rawText, out T? parsed) where T : class
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(rawText)) return false;

        var start = rawText.IndexOf('{');
        var end = rawText.LastIndexOf('}');
        if (start < 0 || end <= start) return false;

        try
        {
            parsed = JsonSerializer.Deserialize<T>(rawText.AsSpan(start, end - start + 1), LenientSettings);
            return parsed is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
