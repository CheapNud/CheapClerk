using CheapClerk.Data;
using Microsoft.EntityFrameworkCore;

namespace CheapClerk.Services;

public sealed class ShareGenerationStore(IDbContextFactory<ClerkDbContext> dbFactory)
{
    /// <summary>Current generation for a document; 0 when never shared or revoked.</summary>
    public async Task<int> GetAsync(int documentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.ShareGenerations.FindAsync([documentId], cancellationToken);
        return row?.Generation ?? 0;
    }

    /// <summary>Invalidates every outstanding share link for the document.</summary>
    public async Task<int> BumpAsync(int documentId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.ShareGenerations.FindAsync([documentId], cancellationToken);
        if (row is null)
        {
            row = new ShareGeneration { DocumentId = documentId, Generation = 1 };
            db.ShareGenerations.Add(row);
        }
        else
        {
            row.Generation++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return row.Generation;
    }
}
