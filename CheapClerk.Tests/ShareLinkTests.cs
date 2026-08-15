using CheapClerk.Data;
using CheapClerk.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Xunit;

namespace CheapClerk.Tests;

public sealed class ShareLinkBuilderTests
{
    private const string Key = "test-signing-key";
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateToken_ThenValidate_RoundTrips()
    {
        var token = ShareLinkBuilder.CreateToken(Key, 27, 3, Now.AddHours(24));

        var validated = ShareLinkBuilder.TryValidate(Key, token, Now);

        Assert.NotNull(validated);
        Assert.Equal(27, validated!.Value.DocumentId);
        Assert.Equal(3, validated.Value.Generation);
        Assert.Equal(Now.AddHours(24).ToUnixTimeSeconds(), validated.Value.ExpiresUtc.ToUnixTimeSeconds());
    }

    [Fact]
    public void TryValidate_ExpiredToken_ReturnsNull()
    {
        var token = ShareLinkBuilder.CreateToken(Key, 27, 0, Now.AddHours(-1));

        Assert.Null(ShareLinkBuilder.TryValidate(Key, token, Now));
    }

    [Fact]
    public void TryValidate_TamperedPayload_ReturnsNull()
    {
        var token = ShareLinkBuilder.CreateToken(Key, 27, 0, Now.AddHours(24));
        // Re-sign a different document id with a DIFFERENT key, splice its payload onto the real signature
        var forgedPayload = ShareLinkBuilder.CreateToken("other-key", 99, 0, Now.AddHours(24)).Split('.')[0];
        var forged = $"{forgedPayload}.{token.Split('.')[1]}";

        Assert.Null(ShareLinkBuilder.TryValidate(Key, forged, Now));
    }

    [Fact]
    public void TryValidate_WrongKey_ReturnsNull()
    {
        var token = ShareLinkBuilder.CreateToken(Key, 27, 0, Now.AddHours(24));

        Assert.Null(ShareLinkBuilder.TryValidate("different-key", token, Now));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    [InlineData("")]
    public void TryValidate_MalformedTokens_ReturnNullWithoutThrowing(string malformed) =>
        Assert.Null(ShareLinkBuilder.TryValidate(Key, malformed, Now));
}

public sealed class ShareGenerationStoreTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clerk-share-{Guid.NewGuid():N}.db");
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
    public async Task Get_UnsharedDocument_IsGenerationZero()
    {
        var store = new ShareGenerationStore(_dbFactory);

        Assert.Equal(0, await store.GetAsync(42));
    }

    [Fact]
    public async Task Bump_InvalidatesTokensMintedUnderThePreviousGeneration()
    {
        var store = new ShareGenerationStore(_dbFactory);
        var beforeRevoke = await store.GetAsync(42);
        var token = ShareLinkBuilder.CreateToken("k", 42, beforeRevoke, DateTimeOffset.UtcNow.AddDays(1));

        Assert.Equal(1, await store.BumpAsync(42));
        Assert.Equal(2, await store.BumpAsync(42));

        var validated = ShareLinkBuilder.TryValidate("k", token, DateTimeOffset.UtcNow);
        Assert.NotNull(validated);                       // signature/expiry still fine…
        Assert.NotEqual(await store.GetAsync(42), validated!.Value.Generation); // …but the generation check kills it
    }
}
