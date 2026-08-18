using System.Text.Json;
using CheapClerk.Configuration;
using CheapClerk.Models.Classification;
using CheapClerk.Models.Extraction;
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
            You are a filing clerk. You file EVERY document you are given — your job
            is to describe what a document IS and make it findable, never to judge
            whether it belongs in the archive.

            Most documents will be Belgian personal administration (household bills,
            insurance, contracts, taxes, vehicles/keuring, building co-ownership
            VME/syndicus, medical, employment, education), usually Dutch, sometimes
            French, German or English. Treat that as a prior, not a requirement:
            manuals, technical documents, game guides, letters — file them just as
            honestly as any invoice.

            Given the OCR text of ONE document plus the existing organizational taxonomy,
            decide how to file it: title, correspondent, document type, tags, document date.

            Rules:
            - NEVER refuse or return an empty suggestion because a document seems out
              of place. Every document gets a truthful title and 1-3 topical tags.
            - Prefer existing tags/correspondents/document types when they genuinely
              fit — reuse exact existing spelling. But a wrong-but-existing label is
              WORSE than a new accurate one: when nothing fits, coin a new short,
              reusable {languageName} name (a document type like 'Handleiding', a tag
              like {tagExample}) instead of forcing the nearest mismatch.
            - When no existing tag fits, create ONE short, reusable {languageName} tag (like {tagExample})
              rather than leaving the document untagged.
            - The correspondent is who SENT the document, not the recipient; leave it
              empty when there is no meaningful sender.
            - Title: short and specific, in the document's language. Never include dates
              the DocumentDate field already captures.
            - DocumentDate is when the document was issued or written. On identity
              documents (ID cards, passports) that is the ISSUE date — never a birth date.
            - Confidence expresses how certain you are that THIS FILING (title, tags,
              type) is right — not whether the document fits an expected domain. Go
              below 0.5 only when the text is garbled or genuinely ambiguous.
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
        List<string> existingDocumentTypes,
        string? extractionContext = null)
    {
        var bounded = documentText.Length > MaxDocumentChars
            ? documentText[..MaxDocumentChars] + "\n[truncated]"
            : documentText;

        var contextBlock = string.IsNullOrWhiteSpace(extractionContext)
            ? string.Empty
            : $"""

              Structured analysis of this document already found:
              {extractionContext}
              Keep the filing decision CONSISTENT with these findings.

              """;

        return $"""
            Existing tags: {(existingTags.Count > 0 ? string.Join(", ", existingTags) : "(none yet)")}
            Existing correspondents: {(existingCorrespondents.Count > 0 ? string.Join(", ", existingCorrespondents) : "(none yet)")}
            Existing document types: {(existingDocumentTypes.Count > 0 ? string.Join(", ", existingDocumentTypes) : "(none yet)")}
            {contextBlock}
            Document text:
            ---
            {bounded}
            ---
            """;
    }

    private static readonly JsonSerializerOptions ContextJsonSettings = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Compact text block the classifier prompt can quote so filing decisions
    // stay consistent with what the deep extraction pass already discovered
    public static string? BuildExtractionContext(ExtractionResult extracted)
    {
        var detail = (object?)extracted.Invoice ?? (object?)extracted.Insurance
            ?? (object?)extracted.Contract ?? extracted.Vehicle;

        var lines = new List<string> { $"Category: {extracted.Category} ({extracted.Confidence:P0} confidence)" };
        if (!string.IsNullOrWhiteSpace(extracted.Summary))
            lines.Add($"Summary: {extracted.Summary}");
        if (detail is not null)
            lines.Add($"Fields: {JsonSerializer.Serialize(detail, detail.GetType(), ContextJsonSettings)}");

        return string.Join("\n", lines);
    }

    public async Task<(ClassificationResult? Classification, bool LlmFailed)> ClassifyAsync(
        string documentText,
        List<string> existingTags,
        List<string> existingCorrespondents,
        List<string> existingDocumentTypes,
        string? extractionContext = null,
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
                    documentText, existingTags, existingCorrespondents, existingDocumentTypes, extractionContext))
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
