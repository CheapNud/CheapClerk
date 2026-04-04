using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CheapClerk.Configuration;

namespace CheapClerk.Services;

public sealed class VisionOcrService(
    IOptions<VisionFallbackOptions> visionOptions,
    ILogger<VisionOcrService> logger)
{
    private readonly VisionFallbackOptions _options = visionOptions.Value;

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<string?> ExtractTextFromImageAsync(
        byte[] imageBytes,
        string mediaType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("Vision OCR fallback is disabled or API key is not configured");
            return null;
        }

        try
        {
            var anthropic = new AnthropicClient(new ClientOptions { ApiKey = _options.ApiKey });
            IChatClient visionClient = anthropic.AsIChatClient(_options.Model);

            var visionMediaType = mediaType switch
            {
                "image/png" or "image/jpeg" or "image/gif" or "image/webp" => mediaType,
                _ => "application/pdf"
            };

            var visionPrompt = new ChatMessage(ChatRole.User,
            [
                new DataContent(imageBytes, visionMediaType),
                new TextContent("Extract all text from this document. Return only the extracted text, preserving the original layout and structure as much as possible. Do not add commentary or interpretation.")
            ]);

            var visionCompletion = await visionClient.GetResponseAsync(
                [visionPrompt],
                cancellationToken: cancellationToken);

            var transcription = visionCompletion.Text;
            logger.LogInformation("Vision OCR extracted {Length} characters", transcription?.Length ?? 0);
            return transcription;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vision OCR extraction failed");
            return null;
        }
    }
}
