using CheapClerk.Data;
using Microsoft.EntityFrameworkCore;

namespace CheapClerk.Services;

public sealed class TranslationStore(IDbContextFactory<ClerkDbContext> dbFactory)
{
    public async Task<Dictionary<string, string>> GetMapAsync(string kind, string culture, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db.NameTranslations
            .Where(t => t.Kind == kind && t.Culture == culture)
            .ToListAsync(ct);

        // Case-variant canonical names are distinct PKs in SQLite; collapse
        // them tolerantly instead of letting the case-insensitive map throw
        return rows
            .GroupBy(t => t.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(rowGroup => rowGroup.Key, rowGroup => rowGroup.Last().Label, StringComparer.OrdinalIgnoreCase);
    }

    public async Task UpsertRangeAsync(string kind, string culture, IReadOnlyDictionary<string, string> labels, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        foreach (var (canonicalName, label) in labels)
        {
            var existingRow = await db.NameTranslations.FindAsync([kind, canonicalName, culture], cancellationToken: ct);
            if (existingRow is null)
            {
                db.NameTranslations.Add(new NameTranslation
                {
                    Kind = kind,
                    CanonicalName = canonicalName,
                    Culture = culture,
                    Label = label,
                    TranslatedAtUtc = now
                });
            }
            else
            {
                existingRow.Label = label;
                existingRow.TranslatedAtUtc = now;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
