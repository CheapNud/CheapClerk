using Anthropic;
using Anthropic.Core;
using CheapClerk.Configuration;
using CheapClerk.Models.Extraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed class StructuredExtractionService(
    IOptions<VisionFallbackOptions> visionOptions,
    ILogger<StructuredExtractionService> logger)
{
    private readonly VisionFallbackOptions _options = visionOptions.Value;

    private const string SystemPrompt = """
        You are a document analysis assistant specialized in Belgian household paperwork
        (invoices, insurance policies, contracts, tax documents, receipts).

        Analyze the provided OCR text and extract structured data. Classify the document
        into one of the known categories. Populate ONLY the sub-object matching the category
        (Invoice / Insurance / Contract) — leave the others null.

        For currency amounts, return decimal numbers without currency symbols.
        For dates, use yyyy-MM-dd format.
        If a field is not present in the document, leave it null — do not guess.
        """;

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<ExtractionResult?> ExtractAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("Structured extraction skipped: Anthropic API key not configured");
            return null;
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            logger.LogWarning("Structured extraction skipped: document text is empty");
            return null;
        }

        try
        {
            var anthropic = new AnthropicClient(new ClientOptions { ApiKey = _options.ApiKey });
            IChatClient extractionClient = anthropic.AsIChatClient(_options.Model);

            var extractionPrompt = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, $"Analyze this document:\n\n---\n{documentText}\n---")
            };

            var chatOptions = new ChatOptions
            {
                MaxOutputTokens = 2048,
                Temperature = 0.0f
            };

            var extractionCompletion = await extractionClient.GetResponseAsync<ExtractionResult>(
                extractionPrompt,
                chatOptions,
                cancellationToken: cancellationToken);

            if (extractionCompletion.TryGetResult(out var extracted))
            {
                logger.LogInformation(
                    "Extracted document as {Category} with confidence {Confidence:P0}",
                    extracted.Category,
                    extracted.Confidence);
                return extracted;
            }

            logger.LogWarning("Structured extraction returned no parseable result");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Structured extraction failed");
            return null;
        }
    }
}
