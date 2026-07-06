using System.Globalization;
using CheapClerk.Configuration;
using CheapClerk.Models;
using CheapClerk.Models.Classification;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed class AppliedClassification
{
    public string? NewTitle { get; set; }
    public List<string> AppliedTags { get; set; } = [];
}

public sealed class ClassificationApplier(
    PaperlessClient paperlessClient,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<ClassificationApplier> logger)
{
    private readonly ClassificationOptions _options = classificationOptions.Value;

    public async Task<AppliedClassification?> ApplyAsync(
        PaperlessDocument doc,
        ClassificationResult classification,
        TagContext tagContext,
        CancellationToken cancellationToken = default)
    {
        var (matchedTagIds, missingTagNames) = TagResolver.Resolve(
            classification.Tags, tagContext.ClassifiableTagLookup, _options.MaxTagsPerDocument);

        var createdTagIds = new List<int>();
        if (_options.AutoCreateTags)
        {
            foreach (var missingName in missingTagNames)
            {
                if (missingName.Equals(_options.InboxTagName, StringComparison.OrdinalIgnoreCase) ||
                    missingName.Equals(_options.ReviewTagName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var createdTag = await paperlessClient.CreateTagAsync(missingName, cancellationToken: cancellationToken);
                if (createdTag is not null)
                {
                    createdTagIds.Add(createdTag.Id);
                    tagContext.TagLookup[createdTag.Id] = createdTag.Name;
                    tagContext.ClassifiableTagLookup[createdTag.Id] = createdTag.Name;
                }
            }
        }

        int? correspondentId = null;
        if (!string.IsNullOrWhiteSpace(classification.Correspondent))
        {
            var existingCorrespondentId = tagContext.CorrespondentLookup
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
                    tagContext.CorrespondentLookup[createdCorrespondent.Id] = createdCorrespondent.Name;
                }
            }
        }

        int? documentTypeId = null;
        if (!string.IsNullOrWhiteSpace(classification.DocumentType))
        {
            var existingDocumentTypeId = tagContext.DocumentTypeLookup
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
                    tagContext.DocumentTypeLookup[createdDocumentType.Id] = createdDocumentType.Name;
                }
            }
        }

        // Deliberate extension over today's processor: exclude BOTH workflow tag ids.
        // The processor only ever strips InboxTagId (a no-op difference for inbox
        // docs); stripping ReviewTagId too matters once review-queue docs flow
        // through this same applier.
        var finalTagIds = matchedTagIds
            .Concat(createdTagIds)
            .Concat(doc.Tags.Where(tagId => tagId != tagContext.InboxTagId && tagId != tagContext.ReviewTagId))
            .Distinct()
            .ToList();

        string? createdDate = null;
        if (DateOnly.TryParseExact(classification.DocumentDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            createdDate = parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var suggested = classification.SuggestedTitle;
        var title = string.IsNullOrWhiteSpace(suggested)
            ? null
            : suggested.Length > 128 ? suggested[..128] : suggested;

        var update = new DocumentUpdate
        {
            Title = title,
            CorrespondentId = correspondentId,
            DocumentTypeId = documentTypeId,
            TagIds = finalTagIds,
            CreatedDate = createdDate
        };

        var patchSucceeded = await paperlessClient.UpdateDocumentAsync(doc.Id, update, cancellationToken);
        if (!patchSucceeded)
            return null;

        return new AppliedClassification
        {
            NewTitle = update.Title,
            AppliedTags = finalTagIds
                .Where(tagContext.TagLookup.ContainsKey)
                .Select(tagId => tagContext.TagLookup[tagId])
                .ToList()
        };
    }
}
