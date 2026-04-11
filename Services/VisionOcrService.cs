using Anthropic;
using Anthropic.Core;
using CheapClerk.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed class VisionOcrService(
    IOptions<VisionFallbackOptions> visionOptions,
    IOptions<LlmOptions> llmOptions,
    ILogger<VisionOcrService> logger)
{
    private readonly VisionFallbackOptions _vision = visionOptions.Value;
    private readonly LlmOptions _llm = llmOptions.Value;

    public bool IsEnabled => _vision.Enabled && !string.IsNullOrWhiteSpace(_llm.Anthropic.ApiKey);

    public async Task<string?> ExtractTextFromImageAsync(
        byte[] imageBytes,
        string mediaType = "application/pdf",
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("Vision OCR fallback is disabled or Anthropic API key is not configured");
            return null;
        }

        try
        {
            var anthropic = new AnthropicClient(new ClientOptions { ApiKey = _llm.Anthropic.ApiKey });
            IChatClient visionClient = anthropic.AsIChatClient(_llm.Anthropic.Model);

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
