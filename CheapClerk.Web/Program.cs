using CheapClerk.Configuration;
using CheapClerk.Data;
using CheapClerk.Services;
using CheapClerk.Web.Components;
using CheapHelpers.Blazor.Extensions;
using CheapHelpers.Services.Auth.Plex;
using CheapHelpers.Services.Auth.Plex.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
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

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/login";
        opt.ExpireTimeSpan = TimeSpan.FromDays(30);
        opt.SlidingExpiration = true;
    });

// Server members only — everything is protected unless explicitly marked AllowAnonymous.
builder.Services.AddAuthorization(opt =>
    opt.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddPlexAuth(opts =>
{
    opts.ProductName = "CheapClerk";
    opts.ClientIdentifier = builder.Configuration["Plex:ClientId"] ?? "CheapClerk";
    opts.AdminToken = builder.Configuration["Plex:AdminToken"];
    // Empty string would defeat the package's auto-detect fallback and break the PIN flow
    var callbackBaseUrl = builder.Configuration["Plex:CallbackBaseUrl"];
    opts.CallbackBaseUrl = string.IsNullOrWhiteSpace(callbackBaseUrl) ? null : callbackBaseUrl;
    opts.PinPollAttempts = 5;
    opts.PinPollDelay = TimeSpan.FromSeconds(1);
    opts.PostLogoutRedirect = "/login";
    opts.AuthorizeUser = async (plexUser, sp, ct) =>
    {
        var plexAuth = sp.GetRequiredService<IPlexAuthService>();
        return await plexAuth.HasServerAccessAsync(plexUser.Id, ct);
    };
});

builder.Services.AddLocalization(locOptions => locOptions.ResourcesPath = "Resources");

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
builder.Services.AddSingleton<TranslationStore>();
builder.Services.AddSingleton<TaxonomyTranslationService>();
builder.Services.AddSingleton<UploadTracker>();
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

// Behind Hidden-Valley NPM the reverse proxy isn't on loopback, so the default
// Trust forwarded scheme/client IP ONLY from configured proxies (NPM on
// Hidden-Valley in production); unconfigured = framework default (loopback),
// so a direct LAN client cannot spoof X-Forwarded-* headers.
var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
foreach (var proxyAddress in app.Configuration.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
{
    if (System.Net.IPAddress.TryParse(proxyAddress, out var parsedProxy))
        forwardedHeaderOptions.KnownProxies.Add(parsedProxy);
}
app.UseForwardedHeaders(forwardedHeaderOptions);

var supportedCultures = ClassificationOptions.SupportedCultures;
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPlexAuthEndpoints();

app.MapGet("/culture/set", (string culture, string redirectUri, HttpContext http) =>
{
    if (ClassificationOptions.SupportedCultures.Contains(culture))
    {
        http.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
    }
    return Results.LocalRedirect(LocalRedirectGuard.Sanitize(redirectUri));
}).AllowAnonymous();

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
}).AllowAnonymous();

// Streams the archived preview (or ?original=true) from Paperless so the
// browser never needs the API token. Content types outside the viewer's
// allowlist are degraded to octet-stream so nothing scriptable (SVG/HTML
// smuggled through the consume folder) can render on this origin.
app.MapGet("/documents/{documentId:int}/file", async (
    int documentId,
    PaperlessClient paperless,
    CancellationToken cancellationToken,
    bool original = false) =>
{
    var storedFile = await paperless.GetFileAsync(documentId, original, cancellationToken);
    if (storedFile is null)
        return Results.NotFound();

    string[] inlineSafeTypes = ["application/pdf", "image/jpeg", "image/png", "image/webp", "image/gif"];
    var safeContentType = inlineSafeTypes.Contains(storedFile.Value.ContentType, StringComparer.OrdinalIgnoreCase)
        ? storedFile.Value.ContentType
        : "application/octet-stream";
    return Results.File(storedFile.Value.Payload, safeContentType);
});

app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
