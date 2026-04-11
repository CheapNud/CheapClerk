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

builder.Services.Configure<PaperlessOptions>(paperlessSection);
builder.Services.Configure<VisionFallbackOptions>(visionSection);
builder.Services.Configure<LlmOptions>(llmSection);
builder.Services.Configure<CacheOptions>(cacheSection);
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

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ClerkDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
