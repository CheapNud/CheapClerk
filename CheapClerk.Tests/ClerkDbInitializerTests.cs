using CheapClerk.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CheapClerk.Tests;

public sealed class ClerkDbInitializerTests
{
    private static DbContextOptions<ClerkDbContext> TempDbOptions(string dbPath) =>
        new DbContextOptionsBuilder<ClerkDbContext>().UseSqlite($"Data Source={dbPath}").Options;

    [Fact]
    public async Task EnsureSchema_AddsSuggestionsTable_ToPreexistingDatabase()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"clerk-init-{Guid.NewGuid():N}.db");
        try
        {
            // Simulate a pre-suggestions production db: created WITHOUT the new table
            await using (var oldDb = new ClerkDbContext(TempDbOptions(dbPath)))
            {
                await oldDb.Database.EnsureCreatedAsync();
                await oldDb.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"CachedSuggestions\";");
            }

            await using var db = new ClerkDbContext(TempDbOptions(dbPath));
            await ClerkDbInitializer.EnsureSchemaAsync(db);

            db.Suggestions.Add(new CachedSuggestion
            {
                DocumentId = 7,
                ClassificationJson = "{}",
                Confidence = 0.4,
                SuggestedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.Suggestions.CountAsync());
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
