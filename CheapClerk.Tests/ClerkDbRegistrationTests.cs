using CheapClerk.Configuration;
using CheapClerk.Data;
using CheapClerk.Models.Extraction;
using CheapClerk.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CheapClerk.Tests;

public sealed class ClerkDbRegistrationTests
{
    [Fact]
    public async Task AddClerkDb_DefaultOptions_RegistersSqliteProvider()
    {
        var services = new ServiceCollection();
        services.AddClerkDb(new CacheOptions());

        await using var sp = services.BuildServiceProvider();
        var dbFactory = sp.GetRequiredService<IDbContextFactory<ClerkDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        Assert.Contains("Sqlite", db.Database.ProviderName);
    }

    [Fact]
    public async Task AddClerkDb_LowercasePostgresProvider_RegistersNpgsqlProvider()
    {
        var services = new ServiceCollection();
        services.AddClerkDb(new CacheOptions
        {
            Provider = "postgres",
            ConnectionString = "Host=localhost;Database=x;Username=u;Password=p"
        });

        // Building the context resolves the provider without opening a connection.
        await using var sp = services.BuildServiceProvider();
        var dbFactory = sp.GetRequiredService<IDbContextFactory<ClerkDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        Assert.Contains("Npgsql", db.Database.ProviderName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void AddClerkDb_PostgresWithoutConnectionString_Throws(string? missingConnectionString)
    {
        var services = new ServiceCollection();

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            services.AddClerkDb(new CacheOptions { Provider = "Postgres", ConnectionString = missingConnectionString }));

        Assert.Contains("Cache:ConnectionString", thrown.Message);
    }

    [Fact]
    public void AddClerkDb_UnknownProvider_ThrowsNamingValidOptions()
    {
        var services = new ServiceCollection();

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            services.AddClerkDb(new CacheOptions { Provider = "MongoDb" }));

        Assert.Contains("Sqlite", thrown.Message);
        Assert.Contains("Postgres", thrown.Message);
    }

    [Fact]
    public void ResolveExpiryDate_ParsesValidDate_AsUtcKind()
    {
        var extracted = new ExtractionResult
        {
            Category = DocumentCategory.Invoice,
            Confidence = 0.9,
            Invoice = new ExtractedInvoice { DueDate = "2026-08-15" }
        };

        var resolved = ExtractionCacheService.ResolveExpiryDate(extracted);

        Assert.NotNull(resolved);
        Assert.Equal(DateTimeKind.Utc, resolved!.Value.Kind);
    }
}
