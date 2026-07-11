using CheapClerk.Data;
using CheapClerk.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace CheapClerk.Tests;

public sealed class TranslationStoreTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clerk-trans-{Guid.NewGuid():N}.db");
    private PooledDbContextFactory<ClerkDbContext> _dbFactory = null!;

    public async ValueTask InitializeAsync()
    {
        var dbOptions = new DbContextOptionsBuilder<ClerkDbContext>()
            .UseSqlite($"Data Source={_dbPath}").Options;
        _dbFactory = new PooledDbContextFactory<ClerkDbContext>(dbOptions);
        await using var db = await _dbFactory.CreateDbContextAsync();
        await ClerkDbInitializer.EnsureSchemaAsync(db);
    }

    public ValueTask DisposeAsync()
    {
        SqliteConnectionDropper.Drop(_dbPath);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task GetMap_ToleratesCaseVariantCanonicalNames()
    {
        var store = new TranslationStore(_dbFactory);
        await store.UpsertRangeAsync("tag", "en", new Dictionary<string, string> { { "Belastingen", "Taxes" } });
        await store.UpsertRangeAsync("tag", "en", new Dictionary<string, string> { { "BELASTINGEN", "TAXES" } });

        var retrieved = await store.GetMapAsync("tag", "en");

        Assert.Single(retrieved);
        Assert.True(retrieved.ContainsKey("belastingen"));
    }

    [Fact]
    public async Task Upsert_ThenGet_RoundTrips()
    {
        var store = new TranslationStore(_dbFactory);
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Invoice", "Factuur" },
            { "Receipt", "Kwitantie" }
        };

        await store.UpsertRangeAsync("document_type", "nl", labels);
        var retrieved = await store.GetMapAsync("document_type", "nl");

        Assert.Equal(2, retrieved.Count);
        Assert.True(retrieved.TryGetValue("Invoice", out var label));
        Assert.Equal("Factuur", label);
    }

    [Fact]
    public async Task Upsert_SameKey_Overwrites()
    {
        var store = new TranslationStore(_dbFactory);
        var labels1 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "TagName", "OldLabel" }
        };
        var labels2 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "TagName", "NewLabel" }
        };

        await store.UpsertRangeAsync("tag", "en", labels1);
        await store.UpsertRangeAsync("tag", "en", labels2);
        var retrieved = await store.GetMapAsync("tag", "en");

        Assert.Single(retrieved);
        Assert.Equal("NewLabel", retrieved["TagName"]);
    }

    [Fact]
    public async Task Upsert_KindAndCulture_Isolated()
    {
        var store = new TranslationStore(_dbFactory);
        var tagsNl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Green", "Groen" }
        };
        var tagsEn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Green", "Green" }
        };
        var docTypesNl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Green", "Groen-Soort" }
        };

        await store.UpsertRangeAsync("tag", "nl", tagsNl);
        await store.UpsertRangeAsync("tag", "en", tagsEn);
        await store.UpsertRangeAsync("document_type", "nl", docTypesNl);

        var tagNl = await store.GetMapAsync("tag", "nl");
        var tagEn = await store.GetMapAsync("tag", "en");
        var docTypeNl = await store.GetMapAsync("document_type", "nl");

        Assert.Equal("Groen", tagNl["Green"]);
        Assert.Equal("Green", tagEn["Green"]);
        Assert.Equal("Groen-Soort", docTypeNl["Green"]);
    }

    [Fact]
    public async Task EnsureSchema_BootstrapsLegacyDb()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"clerk-trans-legacy-{Guid.NewGuid():N}.db");
        try
        {
            // Create a legacy database with only CachedExtractions
            await using (var legacyConnection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                await legacyConnection.OpenAsync();
                await using var createTable = legacyConnection.CreateCommand();
                createTable.CommandText =
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
                await createTable.ExecuteNonQueryAsync();
            }

            var dbOptions = new DbContextOptionsBuilder<ClerkDbContext>()
                .UseSqlite($"Data Source={dbPath}").Options;
            await using var db = new ClerkDbContext(dbOptions);
            await ClerkDbInitializer.EnsureSchemaAsync(db);

            // Verify that we can now insert via DbSet
            db.NameTranslations.Add(new NameTranslation
            {
                Kind = "tag",
                CanonicalName = "TestTag",
                Culture = "nl",
                Label = "TestLabel",
                TranslatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            Assert.Equal(1, await db.NameTranslations.CountAsync());
        }
        finally
        {
            SqliteConnectionDropper.Drop(dbPath);
        }
    }
}
