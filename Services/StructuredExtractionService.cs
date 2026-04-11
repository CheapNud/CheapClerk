using CheapClerk.Configuration;
using CheapClerk.Models.Extraction;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed class StructuredExtractionService(
    IChatClient chatClient,
    IOptions<LlmOptions> llmOptions,
    ILogger<StructuredExtractionService> logger)
{
    private readonly LlmOptions _llm = llmOptions.Value;

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

    public bool IsEnabled => _llm.Provider switch
    {
        LlmProvider.Anthropic => !string.IsNullOrWhiteSpace(_llm.Anthropic.ApiKey),
        LlmProvider.Ollama => !string.IsNullOrWhiteSpace(_llm.Ollama.BaseUrl),
        _ => false
    };

    public async Task<ExtractionResult?> ExtractAsync(
        string documentText,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("Structured extraction skipped: LLM provider not configured");
            return null;
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            logger.LogWarning("Structured extraction skipped: document text is empty");
            return null;
        }

        try
        {
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

            var extractionCompletion = await chatClient.GetResponseAsync<ExtractionResult>(
                extractionPrompt,
                chatOptions,
                cancellationToken: cancellationToken);

            if (extractionCompletion.TryGetResult(out var extracted))
            {
                logger.LogInformation(
                    "Extracted document as {Category} with confidence {Confidence:P0} via {Provider}",
                    extracted.Category,
                    extracted.Confidence,
                    _llm.Provider);
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
