using CheapClerk.Data;
using CheapClerk.Models.Classification;
using CheapClerk.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace CheapClerk.Tests;

public sealed class CachedSuggestionStoreTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clerk-store-{Guid.NewGuid():N}.db");
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
    public async Task Upsert_ThenGet_RoundTrips()
    {
        var store = new SuggestionStore(_dbFactory);
        var classification = new ClassificationResult
        {
            SuggestedTitle = "Polis 2026",
            Tags = ["Verzekering"],
            Confidence = 0.45
        };

        await store.UpsertAsync(31, classification);
        var stored = await store.GetAsync(31);

        Assert.NotNull(stored);
        Assert.Equal("Polis 2026", stored!.Value.Classification!.SuggestedTitle);
        Assert.Equal(0.45, stored.Value.Confidence);
    }

    [Fact]
    public async Task Upsert_SameDocument_Overwrites()
    {
        var store = new SuggestionStore(_dbFactory);
        await store.UpsertAsync(32, new ClassificationResult { SuggestedTitle = "first", Confidence = 0.3 });
        await store.UpsertAsync(32, new ClassificationResult { SuggestedTitle = "second", Confidence = 0.5 });

        var stored = await store.GetAsync(32);

        Assert.Equal("second", stored!.Value.Classification!.SuggestedTitle);
        Assert.Equal(0.5, stored.Value.Confidence);
    }

    [Fact]
    public async Task Delete_RemovesSuggestion()
    {
        var store = new SuggestionStore(_dbFactory);
        await store.UpsertAsync(33, new ClassificationResult { Confidence = 0.2 });

        await store.DeleteAsync(33);

        Assert.Null(await store.GetAsync(33));
    }
}
