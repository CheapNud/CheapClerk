using System.Net;
using System.Text.Json;
using CheapClerk.Configuration;
using CheapClerk.Data;
using CheapClerk.Models.Extraction;
using CheapClerk.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheapClerk.Tests;

public sealed class ExtractionCacheReadTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clerk-extract-{Guid.NewGuid():N}.db");
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

    private ExtractionCacheService BuildCache()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var paperless = new PaperlessClient(
            new HttpClient(stub) { BaseAddress = new Uri("http://paperless.test/") },
            Options.Create(new PaperlessOptions()),
            NullLogger<PaperlessClient>.Instance);
        var llmConfig = Options.Create(new LlmOptions());
        var visionConfig = Options.Create(new VisionFallbackOptions { Enabled = false });
        return new ExtractionCacheService(
            _dbFactory,
            paperless,
            new OcrQualityChecker(visionConfig),
            new VisionOcrService(visionConfig, llmConfig, NullLogger<VisionOcrService>.Instance),
            new StructuredExtractionService(new SilentChatClient(), llmConfig, NullLogger<StructuredExtractionService>.Instance),
            NullLogger<ExtractionCacheService>.Instance);
    }

    [Fact]
    public async Task GetCached_ReturnsStoredExtraction_WithoutAnyLlmOrHttpCall()
    {
        var stored = new ExtractionResult { Category = DocumentCategory.Invoice, Confidence = 0.87, Summary = "Telenet" };
        await using (var db = await _dbFactory.CreateDbContextAsync())
        {
            db.CachedExtractions.Add(new CachedExtraction
            {
                DocumentId = 12,
                Category = stored.Category,
                Confidence = stored.Confidence,
                Summary = stored.Summary,
                PayloadJson = JsonSerializer.Serialize(stored),
                ExtractedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var extractionCache = BuildCache();
        var cached = await extractionCache.GetCachedAsync(12);

        Assert.NotNull(cached);
        Assert.Equal(DocumentCategory.Invoice, cached!.Category);
        Assert.Equal(0.87, cached.Confidence);
    }

    [Fact]
    public async Task GetCached_ReturnsNull_WhenNothingStored()
    {
        var extractionCache = BuildCache();

        Assert.Null(await extractionCache.GetCachedAsync(999));
    }

    private async Task SeedInvoiceAsync(int documentId, string? invoiceNumber, string? vendor)
    {
        var seeded = new ExtractionResult
        {
            Category = DocumentCategory.Invoice,
            Confidence = 0.9,
            Invoice = new ExtractedInvoice { InvoiceNumber = invoiceNumber, Vendor = vendor }
        };
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.CachedExtractions.Add(new CachedExtraction
        {
            DocumentId = documentId,
            Category = seeded.Category,
            Confidence = seeded.Confidence,
            PayloadJson = JsonSerializer.Serialize(seeded),
            ExtractedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static ExtractionResult BuildInvoice(string? invoiceNumber, string? vendor) => new()
    {
        Category = DocumentCategory.Invoice,
        Confidence = 0.9,
        Invoice = new ExtractedInvoice { InvoiceNumber = invoiceNumber, Vendor = vendor }
    };

    [Fact]
    public async Task FindInvoiceDuplicate_MatchesSameNumberAndVendor_CaseInsensitive()
    {
        await SeedInvoiceAsync(31, "LM-2026-778899", "Luminus NV");
        var extractionCache = BuildCache();

        var duplicateOf = await extractionCache.FindInvoiceDuplicateAsync(
            40, BuildInvoice("lm-2026-778899", "LUMINUS NV"));

        Assert.Equal(31, duplicateOf);
    }

    [Fact]
    public async Task FindInvoiceDuplicate_MatchesWhenEitherVendorUnknown()
    {
        await SeedInvoiceAsync(32, "F-1001", null);
        var extractionCache = BuildCache();

        Assert.Equal(32, await extractionCache.FindInvoiceDuplicateAsync(40, BuildInvoice("F-1001", "Telenet BV")));
    }

    [Fact]
    public async Task FindInvoiceDuplicate_IgnoresSameNumberFromDifferentVendor()
    {
        await SeedInvoiceAsync(33, "1", "Telenet BV");
        var extractionCache = BuildCache();

        Assert.Null(await extractionCache.FindInvoiceDuplicateAsync(40, BuildInvoice("1", "Proximus NV")));
    }

    [Fact]
    public async Task FindInvoiceDuplicate_IgnoresOwnDocument_AndBlankNumbers()
    {
        await SeedInvoiceAsync(34, "F-2002", "Engie");
        var extractionCache = BuildCache();

        // Same document id must never match itself
        Assert.Null(await extractionCache.FindInvoiceDuplicateAsync(34, BuildInvoice("F-2002", "Engie")));
        // Missing invoice number never matches anything
        Assert.Null(await extractionCache.FindInvoiceDuplicateAsync(40, BuildInvoice(null, "Engie")));
    }

    private sealed class SilentChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("LLM must not be called in cached-read tests");
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("LLM must not be called in cached-read tests");
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
