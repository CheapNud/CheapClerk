using System.Buffers;
using Microsoft.Extensions.Options;
using CheapClerk.Configuration;

namespace CheapClerk.Services;

public sealed class OcrQualityChecker(IOptions<VisionFallbackOptions> visionOptions)
{
    private readonly VisionFallbackOptions _options = visionOptions.Value;

    private static readonly SearchValues<char> GarbageCharacters =
        SearchValues.Create(['□', '�', '■', '▪', '●', '○', '\uFFFD']);

    public bool IsOcrQualitySuspect(string? ocrText)
    {
        if (string.IsNullOrWhiteSpace(ocrText))
            return true;

        if (ocrText.Length < _options.MinTextLength)
            return true;

        var span = ocrText.AsSpan();
        var garbageCount = 0;
        var remaining = span;
        while (true)
        {
            var idx = remaining.IndexOfAny(GarbageCharacters);
            if (idx < 0) break;
            garbageCount++;
            remaining = remaining[(idx + 1)..];
        }

        var garbageRatio = (double)garbageCount / ocrText.Length;
        return garbageRatio > _options.MaxGarbageRatio;
    }
}
