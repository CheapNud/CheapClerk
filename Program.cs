using CheapClerk.Configuration;
using CheapClerk.Data;
using CheapClerk.Services;
using CheapClerk.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var paperlessSection = builder.Configuration.GetSection(PaperlessOptions.SectionName);
var visionSection = builder.Configuration.GetSection(VisionFallbackOptions.SectionName);
var llmSection = builder.Configuration.GetSection(LlmOptions.SectionName);
var cacheSection = builder.Configuration.GetSection(CacheOptions.SectionName);
var classificationSection = builder.Configuration.GetSection(ClassificationOptions.SectionName);

builder.Services.Configure<PaperlessOptions>(paperlessSection);
builder.Services.Configure<VisionFallbackOptions>(visionSection);
builder.Services.Configure<LlmOptions>(llmSection);
builder.Services.Configure<CacheOptions>(cacheSection);
builder.Services.Configure<ClassificationOptions>(classificationSection);
builder.Services.AddConfiguredChatClient();

var cacheOptions = cacheSection.Get<CacheOptions>() ?? new CacheOptions();
builder.Services.AddClerkDb(cacheOptions);

builder.Services.AddHttpClient<PaperlessClient>((sp, httpClient) =>
{
    var paperlessOptions = paperlessSection.Get<PaperlessOptions>()!;
    httpClient.BaseAddress = new Uri(paperlessOptions.BaseUrl.TrimEnd('/') + "/");
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {paperlessOptions.ApiToken}");
});

builder.Services.AddSingleton<OcrQualityChecker>();
builder.Services.AddSingleton<VisionOcrService>();
builder.Services.AddSingleton<StructuredExtractionService>();
builder.Services.AddSingleton<ExtractionCacheService>();
builder.Services.AddSingleton<DocumentClassifierService>();
builder.Services.AddSingleton<TagContextFactory>();
builder.Services.AddSingleton<ClassificationApplier>();
builder.Services.AddSingleton<SuggestionStore>();
builder.Services.AddSingleton<InboxProcessorService>();
builder.Services.AddSingleton<ReviewQueueService>();
builder.Services.AddSingleton<TranslationStore>();
builder.Services.AddSingleton<TaxonomyTranslationService>();
builder.Services.AddSingleton<UploadTracker>();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SearchDocumentsTool>()
    .WithTools<GetDocumentContentTool>()
    .WithTools<ListDocumentsTool>()
    .WithTools<GetDocumentMetadataTool>()
    .WithTools<ListTagsTool>()
    .WithTools<ExtractStructuredDataTool>()
    .WithTools<FindExpiringDocumentsTool>()
    .WithTools<RefreshExtractionCacheTool>()
    .WithTools<ProcessInboxTool>()
    .WithTools<TranslateTaxonomyTool>()
    .WithTools<UploadDocumentTool>()
    .WithTools<ListDocumentTypesTool>()
    .WithTools<ListCorrespondentsTool>()
    .WithTools<UpdateDocumentTool>()
    .WithTools<DeleteDocumentTool>()
    .WithTools<PaperlessStatusTool>()
    .WithTools<ListReviewQueueTool>()
    .WithTools<ApplySuggestionTool>()
    .WithTools<ReclassifyDocumentTool>()
    .WithTools<GetPaymentDetailsTool>();

// MCP stdio uses stdin/stdout — log to stderr only
builder.Logging.AddConsole(consoleOptions =>
{
    consoleOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ClerkDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await ClerkDbInitializer.EnsureSchemaAsync(db);
}

await app.RunAsync();
