using CheapClerk.Configuration;
using CheapClerk.Models;
using CheapClerk.Models.Classification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed class InboxProcessorService(
    PaperlessClient paperlessClient,
    DocumentClassifierService classifier,
    OcrQualityChecker ocrQualityChecker,
    VisionOcrService visionOcrService,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<InboxProcessorService> logger)
{
    private readonly ClassificationOptions _options = classificationOptions.Value;

    public async Task<InboxProcessingReport> ProcessInboxAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !classifier.IsEnabled)
        {
            logger.LogInformation("Inbox processing skipped: classification disabled or classifier not configured");
            return new InboxProcessingReport();
        }

        var (inboxTagId, reviewTagId) = await EnsureWorkflowTagsAsync(cancellationToken);
        if (inboxTagId <= 0 || reviewTagId <= 0)
        {
            logger.LogError("Inbox processing aborted: unable to ensure workflow tags exist");
            return new InboxProcessingReport();
        }

        var inboxDocuments = await paperlessClient.ListDocumentsByTagIdAsync(
            inboxTagId, _options.MaxDocumentsPerRun, cancellationToken);

        var report = new InboxProcessingReport { InboxCount = inboxDocuments.Count };
        if (inboxDocuments.Count == 0)
            return report;

        var tagLookup = new Dictionary<int, string>(await paperlessClient.GetTagLookupAsync(cancellationToken));
        var correspondentLookup = new Dictionary<int, string>(await paperlessClient.GetCorrespondentLookupAsync(cancellationToken));
        var documentTypeLookup = new Dictionary<int, string>(await paperlessClient.GetDocumentTypeLookupAsync(cancellationToken));

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
                var classification = await classifier.ClassifyAsync(
                    text ?? string.Empty,
                    tagLookup.Values.ToList(),
                    correspondentLookup.Values.ToList(),
                    documentTypeLookup.Values.ToList(),
                    cancellationToken);

                if (classification is null || classification.Confidence < _options.MinConfidence)
                {
                    outcome.Confidence = classification?.Confidence ?? 0;

                    var lowRoadTags = doc.Tags
                        .Where(tagId => tagId != inboxTagId)
                        .Append(reviewTagId)
                        .Distinct()
                        .ToList();

                    var lowRoadApplied = await paperlessClient.UpdateDocumentAsync(
                        doc.Id,
                        new DocumentUpdate { TagIds = lowRoadTags },
                        cancellationToken);

                    outcome.Applied = false;
                    outcome.SentToReview = true;
                    outcome.AppliedTags = lowRoadTags
                        .Where(tagLookup.ContainsKey)
                        .Select(tagId => tagLookup[tagId])
                        .ToList();

                    if (!lowRoadApplied)
                        outcome.Error = "Failed to apply review tag";

                    sentToReview++;
                    report.Outcomes.Add(outcome);
                    continue;
                }

                outcome.Confidence = classification.Confidence;

                var (matchedTagIds, missingTagNames) = TagResolver.Resolve(
                    classification.Tags, tagLookup, _options.MaxTagsPerDocument);

                var createdTagIds = new List<int>();
                if (_options.AutoCreateTags)
                {
                    foreach (var missingName in missingTagNames)
                    {
                        var createdTag = await paperlessClient.CreateTagAsync(missingName, cancellationToken: cancellationToken);
                        if (createdTag is not null)
                        {
                            createdTagIds.Add(createdTag.Id);
                            tagLookup[createdTag.Id] = createdTag.Name;
                        }
                    }
                }

                int? correspondentId = null;
                if (!string.IsNullOrWhiteSpace(classification.Correspondent))
                {
                    var existingCorrespondentId = correspondentLookup
                        .FirstOrDefault(c => c.Value.Equals(classification.Correspondent, StringComparison.OrdinalIgnoreCase)).Key;
                    if (existingCorrespondentId > 0)
                    {
                        correspondentId = existingCorrespondentId;
                    }
                    else
                    {
                        var createdCorrespondent = await paperlessClient.CreateCorrespondentAsync(
                            classification.Correspondent, cancellationToken);
                        if (createdCorrespondent is not null)
                        {
                            correspondentId = createdCorrespondent.Id;
                            correspondentLookup[createdCorrespondent.Id] = createdCorrespondent.Name;
                        }
                    }
                }

                int? documentTypeId = null;
                if (!string.IsNullOrWhiteSpace(classification.DocumentType))
                {
                    var existingDocumentTypeId = documentTypeLookup
                        .FirstOrDefault(dt => dt.Value.Equals(classification.DocumentType, StringComparison.OrdinalIgnoreCase)).Key;
                    if (existingDocumentTypeId > 0)
                    {
                        documentTypeId = existingDocumentTypeId;
                    }
                    else
                    {
                        var createdDocumentType = await paperlessClient.CreateDocumentTypeAsync(
                            classification.DocumentType, cancellationToken);
                        if (createdDocumentType is not null)
                        {
                            documentTypeId = createdDocumentType.Id;
                            documentTypeLookup[createdDocumentType.Id] = createdDocumentType.Name;
                        }
                    }
                }

                var finalTagIds = matchedTagIds
                    .Concat(createdTagIds)
                    .Concat(doc.Tags.Where(tagId => tagId != inboxTagId))
                    .Distinct()
                    .ToList();

                string? createdDate = null;
                if (DateOnly.TryParseExact(classification.DocumentDate, "yyyy-MM-dd", out var parsedDate))
                    createdDate = parsedDate.ToString("yyyy-MM-dd");

                var update = new DocumentUpdate
                {
                    Title = string.IsNullOrWhiteSpace(classification.SuggestedTitle) ? null : classification.SuggestedTitle,
                    CorrespondentId = correspondentId,
                    DocumentTypeId = documentTypeId,
                    TagIds = finalTagIds,
                    CreatedDate = createdDate
                };

                var patchSucceeded = await paperlessClient.UpdateDocumentAsync(doc.Id, update, cancellationToken);

                outcome.Applied = patchSucceeded;
                outcome.NewTitle = update.Title;
                outcome.AppliedTags = finalTagIds
                    .Where(tagLookup.ContainsKey)
                    .Select(tagId => tagLookup[tagId])
                    .ToList();

                if (!patchSucceeded)
                    outcome.Error = "Failed to apply document update";

                if (patchSucceeded)
                    applied++;
                else
                    failed++;

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

    private async Task<(int InboxTagId, int ReviewTagId)> EnsureWorkflowTagsAsync(CancellationToken cancellationToken)
    {
        var tags = await paperlessClient.GetTagsAsync(cancellationToken);

        var inboxTag = tags.FirstOrDefault(t => t.Name.Equals(_options.InboxTagName, StringComparison.OrdinalIgnoreCase));
        if (inboxTag is null)
        {
            inboxTag = await paperlessClient.CreateTagAsync(_options.InboxTagName, isInboxTag: true, cancellationToken: cancellationToken);
            if (inboxTag is null)
            {
                logger.LogError("Failed to create inbox tag '{TagName}'", _options.InboxTagName);
                return (0, 0);
            }
        }

        var reviewTag = tags.FirstOrDefault(t => t.Name.Equals(_options.ReviewTagName, StringComparison.OrdinalIgnoreCase));
        if (reviewTag is null)
        {
            reviewTag = await paperlessClient.CreateTagAsync(_options.ReviewTagName, cancellationToken: cancellationToken);
            if (reviewTag is null)
            {
                logger.LogError("Failed to create review tag '{TagName}'", _options.ReviewTagName);
                return (0, 0);
            }
        }

        return (inboxTag.Id, reviewTag.Id);
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
