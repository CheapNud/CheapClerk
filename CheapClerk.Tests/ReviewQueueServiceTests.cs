using System.Net;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Data;
using CheapClerk.Models.Classification;
using CheapClerk.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheapClerk.Tests;

public sealed class ReviewQueueServiceTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"clerk-review-{Guid.NewGuid():N}.db");
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

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <summary>Throws if invoked — reclassify tests only exercise the unconfigured-LLM short-circuit,
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

    private static HttpResponseMessage RespondToDefaultQueue(HttpRequestMessage incoming)
    {
        var url = incoming.RequestUri!.ToString();

        if (url.Contains("api/tags/"))
        {
            return Ok("""
                {"count":3,"results":[
                    {"id":1,"name":"Inbox","is_inbox_tag":true},
                    {"id":2,"name":"Needs Review","is_inbox_tag":false},
                    {"id":3,"name":"Utilities","is_inbox_tag":false}
                ]}
                """);
        }

        if (url.Contains("api/correspondents/"))
            return Ok("{\"count\":0,\"results\":[]}");

        if (url.Contains("api/document_types/"))
            return Ok("{\"count\":0,\"results\":[]}");

        if (url.Contains("api/documents/999/"))
            return new HttpResponseMessage(HttpStatusCode.NotFound);

        if (url.Contains("api/documents/31/"))
        {
            return Ok("""
                {"id":31,"title":"scan0031","content":"some ocr text","tags":[2],"added":"2026-01-05T00:00:00Z"}
                """);
        }

        if (url.Contains("api/documents/?tags__id=2"))
        {
            return Ok("""
                {"count":1,"results":[
                    {"id":31,"title":"scan0031","content":"some ocr text","tags":[2],"added":"2026-01-05T00:00:00Z"}
                ]}
                """);
        }

        if (incoming.Method == HttpMethod.Patch)
            return Ok("{}");

        return Ok("{\"count\":0,\"results\":[]}");
    }

    private ReviewQueueService BuildService(StubHttpHandler stub) =>
        BuildService(stub, out _);

    private ReviewQueueService BuildService(StubHttpHandler stub, out SuggestionStore suggestionStore)
    {
        var paperlessClient = new PaperlessClient(
            new HttpClient(stub) { BaseAddress = new Uri("http://paperless.test/") },
            Options.Create(new PaperlessOptions()),
            NullLogger<PaperlessClient>.Instance);

        var classificationOptions = Options.Create(new ClassificationOptions());

        var tagContextFactory = new TagContextFactory(
            paperlessClient, classificationOptions, NullLogger<TagContextFactory>.Instance);
        var applier = new ClassificationApplier(
            paperlessClient, classificationOptions, NullLogger<ClassificationApplier>.Instance);
        suggestionStore = new SuggestionStore(_dbFactory);

        var llmOptions = Options.Create(new LlmOptions
        {
            Provider = LlmProvider.Anthropic,
            Anthropic = new AnthropicProviderOptions { ApiKey = string.Empty }
        });
        var classifier = new DocumentClassifierService(
            new ThrowingChatClient(), llmOptions, classificationOptions, NullLogger<DocumentClassifierService>.Instance);

        var ocrQualityChecker = new OcrQualityChecker(Options.Create(new VisionFallbackOptions()));
        var visionOcrService = new VisionOcrService(
            Options.Create(new VisionFallbackOptions { Enabled = false }),
            llmOptions,
            NullLogger<VisionOcrService>.Instance);

        return new ReviewQueueService(
            paperlessClient,
            tagContextFactory,
            applier,
            suggestionStore,
            classifier,
            ocrQualityChecker,
            visionOcrService,
            classificationOptions,
            NullLogger<ReviewQueueService>.Instance);
    }

    [Fact]
    public async Task GetQueue_JoinsDocsWithStoredSuggestions()
    {
        var stub = new StubHttpHandler(RespondToDefaultQueue);
        var reviewQueue = BuildService(stub, out var suggestionStore);
        await suggestionStore.UpsertAsync(31, new ClassificationResult
        {
            SuggestedTitle = "Engie factuur",
            Confidence = 0.42
        });

        var queue = await reviewQueue.GetQueueAsync();

        var entry = Assert.Single(queue);
        Assert.Equal(31, entry.DocumentId);
        Assert.NotNull(entry.Suggestion);
        Assert.Equal("Engie factuur", entry.Suggestion!.SuggestedTitle);
        Assert.Equal(0.42, entry.Confidence);
    }

    [Fact]
    public async Task Apply_FilesDocument_AndDeletesSuggestion()
    {
        var stub = new StubHttpHandler(RespondToDefaultQueue);
        var reviewQueue = BuildService(stub, out var suggestionStore);
        await suggestionStore.UpsertAsync(31, new ClassificationResult
        {
            SuggestedTitle = "Engie factuur",
            Confidence = 0.42
        });

        var outcome = await reviewQueue.ApplyAsync(31, new ClassificationResult
        {
            SuggestedTitle = "Engie factuur 2026",
            Tags = ["Utilities"],
            Confidence = 0.9
        });

        Assert.True(outcome.Applied);
        Assert.Null(await suggestionStore.GetAsync(31));
        var patchBody = stub.RequestBodies.Last(body => body is not null && body.Contains("tags"))!;
        using var patchJson = System.Text.Json.JsonDocument.Parse(patchBody);
        var patchedTagIds = patchJson.RootElement.GetProperty("tags")
            .EnumerateArray().Select(tagElement => tagElement.GetInt32()).ToList();
        Assert.DoesNotContain(2, patchedTagIds);   // review tag stripped, wherever it would sit
        Assert.DoesNotContain(1, patchedTagIds);   // inbox tag stripped too
    }

    [Fact]
    public async Task Apply_UnknownDocument_ReturnsError()
    {
        var stub = new StubHttpHandler(RespondToDefaultQueue);
        var reviewQueue = BuildService(stub);

        var outcome = await reviewQueue.ApplyAsync(999, new ClassificationResult { Confidence = 0.9 });

        Assert.False(outcome.Applied);
        Assert.Contains("not found", outcome.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reclassify_WithoutLlmProvider_ReturnsError()
    {
        var stub = new StubHttpHandler(RespondToDefaultQueue);
        var reviewQueue = BuildService(stub);

        var (entry, error) = await reviewQueue.ReclassifyAsync(31, forceVisionOcr: false);

        Assert.Null(entry);
        Assert.NotNull(error);
        Assert.Contains("LLM unavailable", error);
    }
}
