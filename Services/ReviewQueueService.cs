using CheapClerk.Configuration;
using CheapClerk.Models.Classification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed class ReviewQueueEntry
{
    public int DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime? Added { get; set; }
    public ClassificationResult? Suggestion { get; set; }
    public double? Confidence { get; set; }
    public DateTime? SuggestedAtUtc { get; set; }
}

public sealed class ReviewApplyOutcome
{
    public bool Applied { get; set; }
    public string? NewTitle { get; set; }
    public List<string> AppliedTags { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class ReviewQueueService(
    PaperlessClient paperlessClient,
    TagContextFactory tagContextFactory,
    ClassificationApplier applier,
    SuggestionStore suggestionStore,
    ExtractionCacheService extractionCache,
    DocumentClassifierService classifier,
    OcrQualityChecker ocrQualityChecker,
    VisionOcrService visionOcrService,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<ReviewQueueService> logger)
{
    private readonly ClassificationOptions _options = classificationOptions.Value;

    public async Task<List<ReviewQueueEntry>> GetQueueAsync(CancellationToken ct = default)
    {
        var tagContext = await tagContextFactory.BuildAsync(ct);
        if (tagContext is null)
        {
            logger.LogError("Review queue unavailable: unable to ensure workflow tags exist");
            return [];
        }

        // The queue is a full listing, not a processing batch — don't reuse the
        // per-run batch knob as a page size.
        // ponytail: hard 100 ceiling; add paging if a household queue ever exceeds it
        var reviewDocuments = await paperlessClient.ListDocumentsByTagIdAsync(
            tagContext.ReviewTagId, 100, ct);

        var queue = new List<ReviewQueueEntry>();
        foreach (var doc in reviewDocuments)
        {
            var stored = await suggestionStore.GetAsync(doc.Id, ct);

            queue.Add(new ReviewQueueEntry
            {
                DocumentId = doc.Id,
                Title = doc.Title,
                Added = doc.Added,
                Suggestion = stored?.Classification,
                Confidence = stored?.Confidence,
                SuggestedAtUtc = stored?.SuggestedAtUtc
            });
        }

        return queue;
    }

    public async Task<ReviewApplyOutcome> ApplyAsync(
        int documentId, ClassificationResult finalDecision, CancellationToken ct = default)
    {
        var doc = await paperlessClient.GetDocumentAsync(documentId, ct);
        if (doc is null)
            return new ReviewApplyOutcome { Applied = false, Error = "document not found" };

        var tagContext = await tagContextFactory.BuildAsync(ct);
        if (tagContext is null)
            return new ReviewApplyOutcome { Applied = false, Error = "Paperless unreachable" };

        // Human decision: the submitted tag set replaces, additions AND removals stick
        var applied = await applier.ApplyAsync(doc, finalDecision, tagContext, replaceExistingTags: true, ct);
        if (applied is null)
            return new ReviewApplyOutcome { Applied = false, Error = "update failed" };

        await suggestionStore.DeleteAsync(documentId, ct);

        // Same post-filing extraction as the automatic path — accepting a review
        // should leave the document as complete as an auto-filed one
        try
        {
            await extractionCache.GetOrExtractAsync(documentId, forceRefresh: false, ct);
        }
        catch (Exception extractionEx)
        {
            logger.LogWarning(extractionEx, "Post-accept extraction failed for document {DocumentId}", documentId);
        }

        return new ReviewApplyOutcome
        {
            Applied = true,
            NewTitle = applied.NewTitle,
            AppliedTags = applied.AppliedTags
        };
    }

    public async Task<(ReviewQueueEntry? Entry, string? Error)> ReclassifyAsync(
        int documentId, bool forceVisionOcr, CancellationToken ct = default)
    {
        var doc = await paperlessClient.GetDocumentAsync(documentId, ct);
        if (doc is null)
            return (null, "document not found");

        var tagContext = await tagContextFactory.BuildAsync(ct);
        if (tagContext is null)
            return (null, "Paperless unreachable");

        var text = await ResolveDocumentTextAsync(doc.Id, doc.Content, forceVisionOcr, ct);

        // Cached extraction only — a re-run should be cheap; the Extract button
        // refreshes the deep read when the content itself changed
        var cachedExtraction = await extractionCache.GetCachedAsync(documentId, ct);
        var extractionContext = cachedExtraction is null
            ? null
            : DocumentClassifierService.BuildExtractionContext(cachedExtraction);

        var (classification, llmFailed) = await classifier.ClassifyAsync(
            text ?? string.Empty,
            tagContext.ClassifiableTagLookup.Values.ToList(),
            tagContext.CorrespondentLookup.Values.ToList(),
            tagContext.DocumentTypeLookup.Values.ToList(),
            extractionContext,
            ct);

        if (llmFailed)
            return (null, "LLM unavailable — stored suggestion unchanged");

        if (classification is null)
            return (null, "no readable text");

        await suggestionStore.UpsertAsync(documentId, classification, ct);
        var stored = await suggestionStore.GetAsync(documentId, ct);

        return (new ReviewQueueEntry
        {
            DocumentId = doc.Id,
            Title = doc.Title,
            Added = doc.Added,
            Suggestion = stored?.Classification,
            Confidence = stored?.Confidence,
            SuggestedAtUtc = stored?.SuggestedAtUtc
        }, null);
    }

    private async Task<string?> ResolveDocumentTextAsync(
        int documentId, string? content, bool forceVisionOcr, CancellationToken ct)
    {
        var text = content;

        if ((forceVisionOcr || ocrQualityChecker.IsOcrQualitySuspect(text)) && visionOcrService.IsEnabled)
        {
            var originalBytes = await paperlessClient.DownloadOriginalAsync(documentId, ct);
            if (originalBytes is not null)
            {
                var visionText = await visionOcrService.ExtractTextFromImageAsync(originalBytes, cancellationToken: ct);
                if (visionText is not null)
                    text = visionText;
            }
        }

        return text;
    }
}
