using CheapClerk.Configuration;
using CheapClerk.Models.Classification;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed class DocumentClassifierService(
    IChatClient chatClient,
    IOptions<LlmOptions> llmOptions,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<DocumentClassifierService> logger)
{
    private readonly LlmOptions _llm = llmOptions.Value;
    private readonly ClassificationOptions _classification = classificationOptions.Value;

    private const int MaxDocumentChars = 8_000;

    internal static string BuildSystemPrompt(string taxonomyLanguage)
    {
        var (languageName, tagExample) = taxonomyLanguage switch
        {
            "en" => ("English", "'Taxes' or 'Pension'"),
            _ => ("Dutch", "'Belastingen' or 'Pensioen'")
        };

        return $"""
            You are a filing clerk for Belgian household paperwork (invoices, insurance
            policies, contracts, tax documents, receipts, warranties, official letters).
            Documents are usually Dutch, sometimes French, German or English.

            Given the OCR text of ONE document plus the existing organizational taxonomy,
            decide how to file it: title, correspondent, document type, tags, document date.

            Rules:
            - STRONGLY prefer existing tags/correspondents/document types. Only invent a
              new one when nothing existing fits. Reuse exact existing spelling.
            - Give EVERY document 1-3 topical tags. When no existing tag fits, create ONE
              short, reusable {languageName} tag (like {tagExample}) rather than
              leaving the document untagged.
            - The correspondent is who SENT the document, not the recipient.
            - Title: short and specific, in the document's language. Never include dates
              the DocumentDate field already captures.
            - Report honest confidence; below 0.5 when the text is garbled or ambiguous.
            """;
    }

    public bool IsEnabled => _llm.Provider switch
    {
        LlmProvider.Anthropic => !string.IsNullOrWhiteSpace(_llm.Anthropic.ApiKey),
        LlmProvider.Ollama => !string.IsNullOrWhiteSpace(_llm.Ollama.BaseUrl),
        _ => false
    };

    public static string BuildTaxonomyMessage(
        string documentText,
        List<string> existingTags,
        List<string> existingCorrespondents,
        List<string> existingDocumentTypes)
    {
        var bounded = documentText.Length > MaxDocumentChars
            ? documentText[..MaxDocumentChars] + "\n[truncated]"
            : documentText;

        return $"""
            Existing tags: {(existingTags.Count > 0 ? string.Join(", ", existingTags) : "(none yet)")}
            Existing correspondents: {(existingCorrespondents.Count > 0 ? string.Join(", ", existingCorrespondents) : "(none yet)")}
            Existing document types: {(existingDocumentTypes.Count > 0 ? string.Join(", ", existingDocumentTypes) : "(none yet)")}

            Document text:
            ---
            {bounded}
            ---
            """;
    }

    public async Task<(ClassificationResult? Classification, bool LlmFailed)> ClassifyAsync(
        string documentText,
        List<string> existingTags,
        List<string> existingCorrespondents,
        List<string> existingDocumentTypes,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            logger.LogWarning("Classification skipped: LLM provider not configured");
            return (null, true);
        }

        if (string.IsNullOrWhiteSpace(documentText))
        {
            logger.LogWarning("Classification skipped: document text is empty");
            return (null, false);
        }

        try
        {
            var classificationPrompt = new List<ChatMessage>
            {
                new(ChatRole.System, BuildSystemPrompt(_classification.TaxonomyLanguage)),
                new(ChatRole.User, BuildTaxonomyMessage(
                    documentText, existingTags, existingCorrespondents, existingDocumentTypes))
            };

            var chatOptions = new ChatOptions
            {
                MaxOutputTokens = 1024,
                Temperature = 0.0f
            };

            var classificationCompletion = await chatClient.GetResponseAsync<ClassificationResult>(
                classificationPrompt, chatOptions, useJsonSchemaResponseFormat: false, cancellationToken: cancellationToken);

            if (classificationCompletion.TryGetResult(out var classification))
            {
                logger.LogInformation(
                    "Classified document as '{Title}' ({Confidence:P0}) via {Provider}",
                    classification.SuggestedTitle, classification.Confidence, _llm.Provider);
                return (classification, false);
            }

            if (LlmJsonParser.TryParse<ClassificationResult>(classificationCompletion.Text, out var recovered))
            {
                logger.LogInformation("Recovered structured output via lenient parse");
                return (recovered, false);
            }

            logger.LogWarning("Classification returned no parseable result");
            return (null, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Document classification failed");
            return (null, true);
        }
    }
}
