using CheapClerk.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed record TranslationSweep(int AlreadyTranslated, int NewlyTranslated, int Failed);

public sealed class TaxonomyTranslationService(
    PaperlessClient paperlessClient,
    TranslationStore translationStore,
    IChatClient chatClient,
    IOptions<LlmOptions> llmOptions,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<TaxonomyTranslationService> logger,
    TimeProvider? timeProvider = null)
{
    private readonly LlmOptions _llm = llmOptions.Value;
    private readonly ClassificationOptions _classification = classificationOptions.Value;
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private readonly Lock _sync = new();
    private readonly Dictionary<string, DateTime> _lastEnsureUtc = new(StringComparer.OrdinalIgnoreCase);

    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromMinutes(5);

    private const string TagKind = "tag";
    private const string DocumentTypeKind = "document_type";

    public bool IsEnabled => _llm.Provider switch
    {
        LlmProvider.Anthropic => !string.IsNullOrWhiteSpace(_llm.Anthropic.ApiKey),
        LlmProvider.Ollama => !string.IsNullOrWhiteSpace(_llm.Ollama.BaseUrl),
        _ => false
    };

    public async Task<Dictionary<string, string>> GetDisplayMapAsync(
        string kind, string culture, CancellationToken ct = default)
    {
        try
        {
            await EnsureThrottledAsync(culture, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Taxonomy translation sweep failed for culture {Culture}", culture);
        }

        return await translationStore.GetMapAsync(kind, culture, ct);
    }

    private async Task EnsureThrottledAsync(string culture, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        lock (_sync)
        {
            if (_lastEnsureUtc.TryGetValue(culture, out var lastEnsure) && now - lastEnsure < ThrottleWindow)
                return;

            _lastEnsureUtc[culture] = now;
        }

        await EnsureTranslationsAsync(culture, ct);
    }

    public async Task<TranslationSweep> EnsureTranslationsAsync(string culture, CancellationToken ct = default)
    {
        var tags = await paperlessClient.GetTagsAsync(ct);
        var canonicalTagNames = tags
            .Where(tag => !string.Equals(tag.Name, _classification.InboxTagName, StringComparison.OrdinalIgnoreCase))
            .Where(tag => !string.Equals(tag.Name, _classification.ReviewTagName, StringComparison.OrdinalIgnoreCase))
            .Select(tag => tag.Name)
            .ToList();

        var documentTypes = await paperlessClient.GetDocumentTypesAsync(ct);
        var canonicalDocumentTypeNames = documentTypes.Select(documentType => documentType.Name).ToList();

        var existingTagMap = await translationStore.GetMapAsync(TagKind, culture, ct);
        var existingDocumentTypeMap = await translationStore.GetMapAsync(DocumentTypeKind, culture, ct);

        var missingTagNames = canonicalTagNames.Where(name => !existingTagMap.ContainsKey(name)).ToList();
        var missingDocumentTypeNames = canonicalDocumentTypeNames
            .Where(name => !existingDocumentTypeMap.ContainsKey(name))
            .ToList();

        var alreadyTranslated =
            (canonicalTagNames.Count - missingTagNames.Count) +
            (canonicalDocumentTypeNames.Count - missingDocumentTypeNames.Count);

        var missingTagSet = new HashSet<string>(missingTagNames, StringComparer.OrdinalIgnoreCase);
        var missingDocumentTypeSet = new HashSet<string>(missingDocumentTypeNames, StringComparer.OrdinalIgnoreCase);
        var missingNames = missingTagNames
            .Concat(missingDocumentTypeNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingNames.Count == 0)
            return new TranslationSweep(alreadyTranslated, 0, 0);

        if (!IsEnabled)
        {
            logger.LogWarning("Taxonomy translation skipped: LLM provider not configured");
            return new TranslationSweep(alreadyTranslated, 0, missingNames.Count);
        }

        var translations = await TranslateAsync(missingNames, culture, ct);

        var tagUpserts = new Dictionary<string, string>();
        var documentTypeUpserts = new Dictionary<string, string>();
        var newlyTranslated = 0;

        foreach (var name in missingNames)
        {
            if (!translations.TryGetValue(name, out var label) || string.IsNullOrWhiteSpace(label))
                continue;

            if (missingTagSet.Contains(name))
                tagUpserts[name] = label;
            if (missingDocumentTypeSet.Contains(name))
                documentTypeUpserts[name] = label;

            newlyTranslated++;
        }

        if (tagUpserts.Count > 0)
            await translationStore.UpsertRangeAsync(TagKind, culture, tagUpserts, ct);
        if (documentTypeUpserts.Count > 0)
            await translationStore.UpsertRangeAsync(DocumentTypeKind, culture, documentTypeUpserts, ct);

        return new TranslationSweep(alreadyTranslated, newlyTranslated, missingNames.Count - newlyTranslated);
    }

    private async Task<Dictionary<string, string>> TranslateAsync(
        List<string> labels, string culture, CancellationToken ct)
    {
        var cultureName = culture switch
        {
            "en" => "English",
            "nl" => "Dutch",
            _ => culture
        };

        var systemPrompt = $"""
            You are a translator for short document-filing labels (tags and document
            types used to organize household paperwork). Translate each label to
            {cultureName}. Return ONLY a JSON object mapping every input label to its
            translation; if a label is already in the target language return it
            unchanged.
            """;

        var chatMessages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, string.Join("\n", labels))
        };

        var chatOptions = new ChatOptions
        {
            MaxOutputTokens = 1024,
            Temperature = 0.0f
        };

        try
        {
            var reply = await chatClient.GetResponseAsync(chatMessages, chatOptions, ct);
            if (LlmJsonParser.TryParse<Dictionary<string, string>>(reply.Text, out var parsed) && parsed is not null)
                return new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Taxonomy translation LLM call failed for culture {Culture}", culture);
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
