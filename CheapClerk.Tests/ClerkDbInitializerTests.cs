using CheapClerk.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CheapClerk.Tests;

public sealed class ClerkDbInitializerTests
{
    private static DbContextOptions<ClerkDbContext> TempDbOptions(string dbPath) =>
        new DbContextOptionsBuilder<ClerkDbContext>().UseSqlite($"Data Source={dbPath}").Options;

    // Captured verbatim from `SELECT sql FROM sqlite_master WHERE name='CachedExtractions'`
    // after running EnsureCreatedAsync against the current model. This is the ONLY table
    // a legacy (pre-suggestions) production database is expected to have.
    private const string LegacyCachedExtractionsDdl =
        """
        CREATE TABLE "CachedExtractions" (
            "DocumentId" INTEGER NOT NULL CONSTRAINT "PK_CachedExtractions" PRIMARY KEY AUTOINCREMENT,
            "Category" INTEGER NOT NULL,
            "Confidence" REAL NOT NULL,
            "Summary" TEXT NULL,
            "ExpiryDate" TEXT NULL,
            "PayloadJson" TEXT NOT NULL,
            "ExtractedAt" TEXT NOT NULL
        )
        """;

    [Fact]
    public async Task EnsureSchema_AddsSuggestionsTable_ToPreexistingDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"clerk-init-{Guid.NewGuid():N}.db");
        try
        {
            // Simulate a genuine pre-suggestions production db: build the file by hand with
            // only the legacy table's DDL, bypassing EF entirely so no suggestions table can
            // sneak in via EnsureCreatedAsync against the current model.
            await using (var legacyConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                await legacyConnection.OpenAsync();
                await using var createTable = legacyConnection.CreateCommand();
                createTable.CommandText = LegacyCachedExtractionsDdl;
                await createTable.ExecuteNonQueryAsync();
            }

            await using var db = new ClerkDbContext(TempDbOptions(dbPath));
            await ClerkDbInitializer.EnsureSchemaAsync(db);

            db.CachedSuggestions.Add(new CachedSuggestion
            {
                DocumentId = 7,
                ClassificationJson = "{}",
                Confidence = 0.4,
                SuggestedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.CachedSuggestions.CountAsync());
        }
        finally
        {
            SqliteConnectionDropper.Drop(dbPath);
        }
    }
}

// small helper in the same file
internal static class SqliteConnectionDropper
{
    public static void Drop(string dbPath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(dbPath)) File.Delete(dbPath);
    }
}
