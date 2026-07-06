using CheapClerk.Configuration;
using CheapClerk.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed class InboxProcessorService(
    PaperlessClient paperlessClient,
    DocumentClassifierService classifier,
    OcrQualityChecker ocrQualityChecker,
    VisionOcrService visionOcrService,
    TagContextFactory tagContextFactory,
    ClassificationApplier applier,
    SuggestionStore suggestionStore,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<InboxProcessorService> logger)
{
    private readonly ClassificationOptions _options = classificationOptions.Value;
    private readonly SemaphoreSlim _runGate = new(1, 1);

    public async Task<InboxProcessingReport> ProcessInboxAsync(CancellationToken cancellationToken = default)
    {
        if (!await _runGate.WaitAsync(0, cancellationToken))
        {
            logger.LogWarning("Inbox processing skipped: a processing run is already in progress");
            return new InboxProcessingReport { SkippedReason = "a processing run is already in progress" };
        }

        try
        {
            if (!_options.Enabled || !classifier.IsEnabled)
            {
                logger.LogInformation("Inbox processing skipped: classification disabled or classifier not configured");
                return new InboxProcessingReport
                {
                    SkippedReason = "classification is disabled or the LLM provider is not configured"
                };
            }

            var tagContext = await tagContextFactory.BuildAsync(cancellationToken);
            if (tagContext is null)
            {
                logger.LogError("Inbox processing aborted: unable to ensure workflow tags exist");
                return new InboxProcessingReport
                {
                    SkippedReason = "could not verify workflow tags — is Paperless reachable?"
                };
            }

            var inboxDocuments = await paperlessClient.ListDocumentsByTagIdAsync(
                tagContext.InboxTagId, _options.MaxDocumentsPerRun, cancellationToken);

            var report = new InboxProcessingReport { InboxCount = inboxDocuments.Count };
            if (inboxDocuments.Count == 0)
                return report;

            var applied = 0;
            var sentToReview = 0;
            var failed = 0;

            foreach (var doc in inboxDocuments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var outcome = new InboxDocumentOutcome
                {
                    DocumentId = doc.Id,
                    OriginalTitle = doc.Title
                };

                try
                {
                    var text = await ResolveDocumentTextAsync(doc, cancellationToken);
                    var (classification, llmFailed) = await classifier.ClassifyAsync(
                        text ?? string.Empty,
                        tagContext.ClassifiableTagLookup.Values.ToList(),
                        tagContext.CorrespondentLookup.Values.ToList(),
                        tagContext.DocumentTypeLookup.Values.ToList(),
                        cancellationToken);

                    if (llmFailed)
                    {
                        outcome.Error = "LLM unavailable";
                        failed++;
                        report.Outcomes.Add(outcome);
                        continue;
                    }

                    if (classification is null || classification.Confidence < _options.MinConfidence)
                    {
                        outcome.Confidence = classification?.Confidence ?? 0;

                        if (classification is not null)
                        {
                            await suggestionStore.UpsertAsync(doc.Id, classification, cancellationToken);
                        }

                        var lowRoadTags = doc.Tags
                            .Where(tagId => tagId != tagContext.InboxTagId)
                            .Append(tagContext.ReviewTagId)
                            .Distinct()
                            .ToList();

                        var lowRoadApplied = await paperlessClient.UpdateDocumentAsync(
                            doc.Id,
                            new DocumentUpdate { TagIds = lowRoadTags },
                            cancellationToken);

                        outcome.Applied = false;
                        outcome.AppliedTags = lowRoadTags
                            .Where(tagContext.TagLookup.ContainsKey)
                            .Select(tagId => tagContext.TagLookup[tagId])
                            .ToList();

                        if (lowRoadApplied)
                        {
                            outcome.SentToReview = true;
                            sentToReview++;
                        }
                        else
                        {
                            outcome.Error = "Failed to apply review tag";
                            failed++;
                        }

                        report.Outcomes.Add(outcome);
                        continue;
                    }

                    outcome.Confidence = classification.Confidence;

                    var appliedClassification = await applier.ApplyAsync(doc, classification, tagContext, cancellationToken);

                    outcome.Applied = appliedClassification is not null;
                    outcome.NewTitle = appliedClassification?.NewTitle;
                    outcome.AppliedTags = appliedClassification?.AppliedTags ?? [];

                    if (appliedClassification is null)
                        outcome.Error = "Failed to update document";

                    if (appliedClassification is not null)
                    {
                        // A confidently filed document no longer needs its parked suggestion
                        await suggestionStore.DeleteAsync(doc.Id, cancellationToken);
                        applied++;
                    }
                    else
                    {
                        failed++;
                    }

                    report.Outcomes.Add(outcome);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process inbox document {DocumentId}", doc.Id);
                    outcome.Applied = false;
                    outcome.Error = ex.Message;
                    failed++;
                    report.Outcomes.Add(outcome);
                }
            }

            logger.LogInformation(
                "Inbox processing complete: {Found} found, {Applied} applied, {Review} sent to review, {Failed} failed",
                inboxDocuments.Count, applied, sentToReview, failed);

            return report;
        }
        finally
        {
            _runGate.Release();
        }
    }

    private async Task<string?> ResolveDocumentTextAsync(PaperlessDocument doc, CancellationToken cancellationToken)
    {
        var text = doc.Content;

        if (ocrQualityChecker.IsOcrQualitySuspect(text) && visionOcrService.IsEnabled)
        {
            var originalBytes = await paperlessClient.DownloadOriginalAsync(doc.Id, cancellationToken);
            if (originalBytes is not null)
            {
                var visionText = await visionOcrService.ExtractTextFromImageAsync(
                    originalBytes, cancellationToken: cancellationToken);
                if (visionText is not null)
                    text = visionText;
            }
        }

        return text;
    }
}

public sealed class InboxProcessingReport
{
    public int InboxCount { get; set; }
    public List<InboxDocumentOutcome> Outcomes { get; set; } = [];
    public string? SkippedReason { get; set; }
}

public sealed class InboxDocumentOutcome
{
    public int DocumentId { get; set; }
    public string OriginalTitle { get; set; } = string.Empty;
    public string? NewTitle { get; set; }
    public bool Applied { get; set; }
    public bool SentToReview { get; set; }
    public double Confidence { get; set; }
    public List<string> AppliedTags { get; set; } = [];
    public string? Error { get; set; }
}
