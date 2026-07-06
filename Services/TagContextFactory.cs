using CheapClerk.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheapClerk.Services;

public sealed record TagContext(
    int InboxTagId,
    int ReviewTagId,
    Dictionary<int, string> TagLookup,
    Dictionary<int, string> ClassifiableTagLookup,
    Dictionary<int, string> CorrespondentLookup,
    Dictionary<int, string> DocumentTypeLookup);

public sealed class TagContextFactory(
    PaperlessClient paperlessClient,
    IOptions<ClassificationOptions> classificationOptions,
    ILogger<TagContextFactory> logger)
{
    private readonly ClassificationOptions _options = classificationOptions.Value;

    public async Task<TagContext?> BuildAsync(CancellationToken cancellationToken = default)
    {
        var (inboxTagId, reviewTagId) = await EnsureWorkflowTagsAsync(cancellationToken);
        if (inboxTagId <= 0 || reviewTagId <= 0)
            return null;

        var tagLookup = new Dictionary<int, string>(await paperlessClient.GetTagLookupAsync(cancellationToken));
        var correspondentLookup = new Dictionary<int, string>(await paperlessClient.GetCorrespondentLookupAsync(cancellationToken));
        var documentTypeLookup = new Dictionary<int, string>(await paperlessClient.GetDocumentTypeLookupAsync(cancellationToken));

        var classifiableTagLookup = new Dictionary<int, string>(
            tagLookup.Where(kvp => kvp.Key != inboxTagId && kvp.Key != reviewTagId));

        return new TagContext(
            inboxTagId,
            reviewTagId,
            tagLookup,
            classifiableTagLookup,
            correspondentLookup,
            documentTypeLookup);
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
}
