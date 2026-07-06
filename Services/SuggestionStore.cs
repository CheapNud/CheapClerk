using System.Text.Json;
using CheapClerk.Data;
using CheapClerk.Models.Classification;
using Microsoft.EntityFrameworkCore;

namespace CheapClerk.Services;

public sealed class SuggestionStore(IDbContextFactory<ClerkDbContext> dbFactory)
{
    private static readonly JsonSerializerOptions JsonSettings = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task UpsertAsync(int documentId, ClassificationResult classification, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var classificationJson = JsonSerializer.Serialize(classification, JsonSettings);
        var now = DateTime.UtcNow;

        var existingRow = await db.CachedSuggestions.FindAsync([documentId], cancellationToken: ct);
        if (existingRow is null)
        {
            db.CachedSuggestions.Add(new CachedSuggestion
            {
                DocumentId = documentId,
                ClassificationJson = classificationJson,
                Confidence = classification.Confidence,
                SuggestedAtUtc = now
            });
        }
        else
        {
            existingRow.ClassificationJson = classificationJson;
            existingRow.Confidence = classification.Confidence;
            existingRow.SuggestedAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<(ClassificationResult? Classification, double Confidence, DateTime SuggestedAtUtc)?> GetAsync(int documentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var row = await db.CachedSuggestions.FindAsync([documentId], cancellationToken: ct);
        if (row is null)
            return null;

        var classification = JsonSerializer.Deserialize<ClassificationResult>(row.ClassificationJson, JsonSettings);
        return (classification, row.Confidence, row.SuggestedAtUtc);
    }

    public async Task DeleteAsync(int documentId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var row = await db.CachedSuggestions.FindAsync([documentId], cancellationToken: ct);
        if (row is not null)
        {
            db.CachedSuggestions.Remove(row);
            await db.SaveChangesAsync(ct);
        }
    }
}
