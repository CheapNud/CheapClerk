using System.Text.Json;
using CheapClerk.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CheapClerk.Configuration;

namespace CheapClerk.Services;

public sealed class PaperlessClient(
    HttpClient httpClient,
    IOptions<PaperlessOptions> paperlessOptions,
    ILogger<PaperlessClient> logger)
{
    private readonly PaperlessOptions _options = paperlessOptions.Value;

    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<DocumentMatch>> SearchDocumentsAsync(
        string query,
        string? tagName = null,
        string? correspondentName = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var tagLookup = await GetTagLookupAsync(cancellationToken);
        var correspondentLookup = await GetCorrespondentLookupAsync(cancellationToken);

        var url = $"api/documents/?query={Uri.EscapeDataString(query)}&page_size={maxResults}";

        if (tagName is not null)
        {
            var tagId = tagLookup.FirstOrDefault(t => t.Value.Equals(tagName, StringComparison.OrdinalIgnoreCase)).Key;
            if (tagId > 0)
                url += $"&tags__id={tagId}";
        }

        if (correspondentName is not null)
        {
            var correspondentId = correspondentLookup
                .FirstOrDefault(c => c.Value.Equals(correspondentName, StringComparison.OrdinalIgnoreCase)).Key;
            if (correspondentId > 0)
                url += $"&correspondent__id={correspondentId}";
        }

        var page = await GetAsync<PaperlessPage<PaperlessDocument>>(url, cancellationToken);
        if (page is null) return [];

        return page.Entries.Select(doc => new DocumentMatch
        {
            DocumentId = doc.Id,
            Title = doc.Title,
            Excerpt = doc.Content?.Length > 200 ? doc.Content[..200] + "..." : doc.Content,
            Correspondent = doc.CorrespondentId.HasValue && correspondentLookup.TryGetValue(doc.CorrespondentId.Value, out var corrName)
                ? corrName : null,
            Tags = doc.Tags
                .Where(tagLookup.ContainsKey)
                .Select(tagId => tagLookup[tagId])
                .ToList(),
            Created = doc.Created
        }).ToList();
    }

    public async Task<PaperlessDocument?> GetDocumentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<PaperlessDocument>($"api/documents/{documentId}/", cancellationToken);
    }

    public async Task<string?> GetDocumentContentAsync(int documentId, CancellationToken cancellationToken = default)
    {
        var doc = await GetDocumentAsync(documentId, cancellationToken);
        return doc?.Content;
    }

    public async Task<byte[]?> DownloadOriginalAsync(int documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var downloadReply = await httpClient.GetAsync($"api/documents/{documentId}/download/", cancellationToken);
            downloadReply.EnsureSuccessStatusCode();
            return await downloadReply.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to download original for document {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<List<PaperlessDocument>> ListDocumentsAsync(
        string? correspondentName = null,
        string? tagName = null,
        DateTime? addedAfter = null,
        DateTime? addedBefore = null,
        int maxResults = 25,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/documents/?page_size={maxResults}&ordering=-added";

        if (correspondentName is not null)
        {
            var correspondentLookup = await GetCorrespondentLookupAsync(cancellationToken);
            var correspondentId = correspondentLookup
                .FirstOrDefault(c => c.Value.Equals(correspondentName, StringComparison.OrdinalIgnoreCase)).Key;
            if (correspondentId > 0)
                url += $"&correspondent__id={correspondentId}";
        }

        if (tagName is not null)
        {
            var tagLookup = await GetTagLookupAsync(cancellationToken);
            var tagId = tagLookup.FirstOrDefault(t => t.Value.Equals(tagName, StringComparison.OrdinalIgnoreCase)).Key;
            if (tagId > 0)
                url += $"&tags__id={tagId}";
        }

        if (addedAfter.HasValue)
            url += $"&added__date__gt={addedAfter.Value:yyyy-MM-dd}";

        if (addedBefore.HasValue)
            url += $"&added__date__lt={addedBefore.Value:yyyy-MM-dd}";

        var page = await GetAsync<PaperlessPage<PaperlessDocument>>(url, cancellationToken);
        return page?.Entries ?? [];
    }

    public async Task<List<PaperlessTag>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        var page = await GetAsync<PaperlessPage<PaperlessTag>>("api/tags/?page_size=100", cancellationToken);
        return page?.Entries ?? [];
    }

    public async Task<List<PaperlessCorrespondent>> GetCorrespondentsAsync(CancellationToken cancellationToken = default)
    {
        var page = await GetAsync<PaperlessPage<PaperlessCorrespondent>>(
            "api/correspondents/?page_size=100", cancellationToken);
        return page?.Entries ?? [];
    }

    public async Task<Dictionary<int, string>> GetTagLookupAsync(CancellationToken cancellationToken = default)
    {
        var tags = await GetTagsAsync(cancellationToken);
        return tags.ToDictionary(t => t.Id, t => t.Name);
    }

    public async Task<Dictionary<int, string>> GetCorrespondentLookupAsync(CancellationToken cancellationToken = default)
    {
        var correspondents = await GetCorrespondentsAsync(cancellationToken);
        return correspondents.ToDictionary(c => c.Id, c => c.Name);
    }

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken) where T : class
    {
        try
        {
            var httpReply = await httpClient.GetAsync(relativeUrl, cancellationToken);
            httpReply.EnsureSuccessStatusCode();

            var json = await httpReply.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(json, JsonSettings);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Paperless API call failed: {Url}", relativeUrl);
            return null;
        }
    }
}
