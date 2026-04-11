using System.Globalization;
using System.Text.Json;
using CheapClerk.Data;
using CheapClerk.Models.Extraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CheapClerk.Services;

public sealed class ExtractionCacheService(
    IDbContextFactory<ClerkDbContext> dbFactory,
    PaperlessClient paperlessClient,
    OcrQualityChecker ocrQualityChecker,
    VisionOcrService visionOcrService,
    StructuredExtractionService extractionService,
    ILogger<ExtractionCacheService> logger)
{
    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ExtractionResult?> GetOrExtractAsync(
        int documentId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        if (!forceRefresh)
        {
            var cachedRow = await db.CachedExtractions.FindAsync([documentId], cancellationToken);
            if (cachedRow is not null)
                return JsonSerializer.Deserialize<ExtractionResult>(cachedRow.PayloadJson, JsonSettings);
        }

        var ocrText = await paperlessClient.GetDocumentContentAsync(documentId, cancellationToken);
        if (ocrQualityChecker.IsOcrQualitySuspect(ocrText) && visionOcrService.IsEnabled)
        {
            var originalBytes = await paperlessClient.DownloadOriginalAsync(documentId, cancellationToken);
            if (originalBytes is not null)
            {
                var visionText = await visionOcrService.ExtractTextFromImageAsync(
                    originalBytes,
                    cancellationToken: cancellationToken);
                if (visionText is not null)
                    ocrText = visionText;
            }
        }

        if (string.IsNullOrWhiteSpace(ocrText))
            return null;

        var extracted = await extractionService.ExtractAsync(ocrText, cancellationToken);
        if (extracted is null)
            return null;

        await UpsertAsync(db, documentId, extracted, cancellationToken);
        return extracted;
    }

    public async Task<List<ExpiringDocument>> FindExpiringAsync(
        int daysAhead,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(daysAhead);
        var today = DateTime.UtcNow.Date;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var expiringRows = await db.CachedExtractions
            .Where(e => e.ExpiryDate != null && e.ExpiryDate >= today && e.ExpiryDate <= cutoff)
            .OrderBy(e => e.ExpiryDate)
            .ToListAsync(cancellationToken);

        return expiringRows.Select(row => new ExpiringDocument(
            row.DocumentId,
            row.Category,
            row.ExpiryDate!.Value,
            (row.ExpiryDate.Value - today).Days,
            row.Summary)).ToList();
    }

    public async Task<RefreshSummary> RefreshAllAsync(
        int maxDocuments = 100,
        CancellationToken cancellationToken = default)
    {
        var documents = await paperlessClient.ListDocumentsAsync(maxResults: maxDocuments, cancellationToken: cancellationToken);

        var processed = 0;
        var classified = 0;
        var skipped = 0;

        foreach (var doc in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var extractionOutcome = await GetOrExtractAsync(doc.Id, forceRefresh: true, cancellationToken);
                processed++;
                if (extractionOutcome is not null && extractionOutcome.Category != DocumentCategory.Unknown)
                    classified++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract document {DocumentId}", doc.Id);
                skipped++;
            }
        }

        return new RefreshSummary(processed, classified, skipped);
    }

    public async Task<int> GetCachedCountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CachedExtractions.CountAsync(cancellationToken);
    }

    private async Task UpsertAsync(
        ClerkDbContext db,
        int documentId,
        ExtractionResult extracted,
        CancellationToken cancellationToken)
    {
        var expiryDate = ResolveExpiryDate(extracted);
        var payload = JsonSerializer.Serialize(extracted, JsonSettings);

        var existingRow = await db.CachedExtractions.FindAsync([documentId], cancellationToken);
        if (existingRow is null)
        {
            db.CachedExtractions.Add(new CachedExtraction
            {
                DocumentId = documentId,
                Category = extracted.Category,
                Confidence = extracted.Confidence,
                Summary = extracted.Summary,
                ExpiryDate = expiryDate,
                PayloadJson = payload,
                ExtractedAt = DateTime.UtcNow
            });
        }
        else
        {
            existingRow.Category = extracted.Category;
            existingRow.Confidence = extracted.Confidence;
            existingRow.Summary = extracted.Summary;
            existingRow.ExpiryDate = expiryDate;
            existingRow.PayloadJson = payload;
            existingRow.ExtractedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static DateTime? ResolveExpiryDate(ExtractionResult extracted)
    {
        var candidate = extracted.Category switch
        {
            DocumentCategory.Insurance => extracted.Insurance?.CoverageEnd,
            DocumentCategory.Contract => extracted.Contract?.EndDate,
            DocumentCategory.Invoice => extracted.Invoice?.DueDate,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(candidate)) return null;

        return DateTime.TryParseExact(candidate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}

public sealed record ExpiringDocument(
    int DocumentId,
    DocumentCategory Category,
    DateTime ExpiryDate,
    int DaysRemaining,
    string? Summary);

public sealed record RefreshSummary(int Processed, int Classified, int Skipped);
