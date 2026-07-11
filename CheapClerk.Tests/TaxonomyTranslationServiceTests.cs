using System.Net;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Data;
using CheapClerk.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheapClerk.Tests;

public sealed class TaxonomyTranslationServiceTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clerk-taxtrans-{Guid.NewGuid():N}.db");
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

    /// <summary>Fixed instant, advanceable on demand — lets throttle tests control elapsed time deterministically.</summary>
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    /// <summary>Throws if invoked — unconfigured-LLM tests only exercise the gather/short-circuit path,
    /// which never reaches the chat client.</summary>
    private sealed class ThrowingChatClient : IChatClient
    {
        public ChatClientMetadata Metadata { get; } = new("throwing");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? chatOptions = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should not be called — LLM provider is unconfigured in these tests.");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? chatOptions = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Should not be called — LLM provider is unconfigured in these tests.");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage RespondToTaxonomy(HttpRequestMessage incoming)
    {
        var url = incoming.RequestUri!.ToString();

        if (url.Contains("api/tags/"))
        {
            return Ok("""
                {"count":4,"results":[
                    {"id":1,"name":"Inbox","is_inbox_tag":true},
                    {"id":2,"name":"Needs Review","is_inbox_tag":false},
                    {"id":3,"name":"Utilities","is_inbox_tag":false},
                    {"id":4,"name":"Insurance","is_inbox_tag":false}
                ]}
                """);
        }

        if (url.Contains("api/document_types/"))
        {
            return Ok("""
                {"count":1,"results":[
                    {"id":10,"name":"Invoice"}
                ]}
                """);
        }

        return Ok("{\"count\":0,\"results\":[]}");
    }

    private TaxonomyTranslationService BuildService(
        StubHttpHandler stub,
        TranslationStore translationStore,
        IChatClient chatClient,
        TimeProvider? clock = null)
    {
        var paperlessClient = new PaperlessClient(
            new HttpClient(stub) { BaseAddress = new Uri("http://paperless.test/") },
            Options.Create(new PaperlessOptions()),
            NullLogger<PaperlessClient>.Instance);

        var llmOptions = Options.Create(new LlmOptions
        {
            Provider = LlmProvider.Anthropic,
            Anthropic = new AnthropicProviderOptions { ApiKey = string.Empty }
        });
        var classificationOptions = Options.Create(new ClassificationOptions());

        return new TaxonomyTranslationService(
            paperlessClient,
            translationStore,
            chatClient,
            llmOptions,
            classificationOptions,
            NullLogger<TaxonomyTranslationService>.Instance,
            clock);
    }

    [Fact]
    public async Task EnsureTranslations_UnconfiguredLlm_ReturnsFailedForMissingLabels_StoreUntouched()
    {
        var stub = new StubHttpHandler(RespondToTaxonomy);
        var translationStore = new TranslationStore(_dbFactory);
        var taxonomyTranslator = BuildService(stub, translationStore, new ThrowingChatClient());

        var sweep = await taxonomyTranslator.EnsureTranslationsAsync("en");

        // Utilities + Insurance (tags, workflow tags excluded) + Invoice (document type) = 3 missing
        Assert.Equal(0, sweep.AlreadyTranslated);
        Assert.Equal(0, sweep.NewlyTranslated);
        Assert.Equal(3, sweep.Failed);

        Assert.Empty(await translationStore.GetMapAsync("tag", "en"));
        Assert.Empty(await translationStore.GetMapAsync("document_type", "en"));
    }

    [Fact]
    public async Task GetDisplayMap_UnconfiguredLlm_FallsBackToEmptyStoreMap()
    {
        var stub = new StubHttpHandler(RespondToTaxonomy);
        var translationStore = new TranslationStore(_dbFactory);
        var taxonomyTranslator = BuildService(stub, translationStore, new ThrowingChatClient());

        var displayMap = await taxonomyTranslator.GetDisplayMapAsync("tag", "en");

        Assert.Empty(displayMap);
    }

    [Fact]
    public async Task GetDisplayMap_WithinThrottleWindow_HitsPaperlessOnce()
    {
        var stub = new StubHttpHandler(RespondToTaxonomy);
        var translationStore = new TranslationStore(_dbFactory);
        var fakeClock = new FakeClock();
        var taxonomyTranslator = BuildService(stub, translationStore, new ThrowingChatClient(), fakeClock);

        await taxonomyTranslator.GetDisplayMapAsync("tag", "en");
        await taxonomyTranslator.GetDisplayMapAsync("tag", "en");

        var tagRequests = stub.Requests.Count(r => r.RequestUri!.ToString().Contains("api/tags/"));
        Assert.Equal(1, tagRequests);
    }

    [Fact]
    public async Task GetDisplayMap_AfterThrottleWindowExpires_RefetchesFromPaperless()
    {
        var stub = new StubHttpHandler(RespondToTaxonomy);
        var translationStore = new TranslationStore(_dbFactory);
        var fakeClock = new FakeClock();
        var taxonomyTranslator = BuildService(stub, translationStore, new ThrowingChatClient(), fakeClock);

        await taxonomyTranslator.GetDisplayMapAsync("tag", "en");
        fakeClock.UtcNow = fakeClock.UtcNow.AddMinutes(6);
        await taxonomyTranslator.GetDisplayMapAsync("tag", "en");

        var tagRequests = stub.Requests.Count(r => r.RequestUri!.ToString().Contains("api/tags/"));
        Assert.Equal(2, tagRequests);
    }
}
