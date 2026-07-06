using CheapClerk.Configuration;
using CheapClerk.Data;
using CheapClerk.Services;
using CheapClerk.Web.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MudBlazor.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .Enrich.WithProperty("Application", "CheapClerk.Web"));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(opt => opt.DetailedErrors = builder.Environment.IsDevelopment());

builder.Services.AddMudServices();

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
builder.Services.AddDbContextFactory<ClerkDbContext>(dbOpt =>
    dbOpt.UseSqlite($"Data Source={cacheOptions.DatabasePath}"));

builder.Services.AddHttpClient<PaperlessClient>((sp, httpClient) =>
{
    var paperlessConfig = paperlessSection.Get<PaperlessOptions>()!;
    httpClient.BaseAddress = new Uri(paperlessConfig.BaseUrl.TrimEnd('/') + "/");
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {paperlessConfig.ApiToken}");
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
builder.Services.AddSingleton(sp => new InboxRunCoordinator(
    ct => sp.GetRequiredService<InboxProcessorService>().ProcessInboxAsync(ct),
    sp.GetRequiredService<ILogger<InboxRunCoordinator>>()));
builder.Services.AddHostedService<CheapClerk.Web.Services.InboxPollingService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ClerkDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await ClerkDbInitializer.EnsureSchemaAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapPost("/api/inbox/process", (HttpContext http,
    Microsoft.Extensions.Options.IOptions<ClassificationOptions> classificationConfig,
    InboxRunCoordinator coordinator) =>
{
    var suppliedToken = http.Request.Headers["X-Webhook-Token"].FirstOrDefault()
        ?? http.Request.Query["token"].FirstOrDefault();

    return WebhookAuth.Evaluate(classificationConfig.Value.WebhookToken, suppliedToken) switch
    {
        WebhookAuthOutcome.Accepted => Trigger(coordinator),
        WebhookAuthOutcome.Unauthorized => Results.Unauthorized(),
        // NotConfigured and anything unexpected fail closed
        _ => Results.NotFound()
    };

    static IResult Trigger(InboxRunCoordinator coordinator)
    {
        coordinator.RequestRun();
        return Results.Accepted();
    }
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
