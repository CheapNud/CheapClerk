using CheapClerk.Configuration;
using CheapClerk.Services;
using CheapClerk.Web.Components;
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

builder.Services.Configure<PaperlessOptions>(paperlessSection);
builder.Services.Configure<VisionFallbackOptions>(visionSection);

builder.Services.AddHttpClient<PaperlessClient>((sp, httpClient) =>
{
    var paperlessConfig = paperlessSection.Get<PaperlessOptions>()!;
    httpClient.BaseAddress = new Uri(paperlessConfig.BaseUrl.TrimEnd('/') + "/");
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {paperlessConfig.ApiToken}");
});

builder.Services.AddSingleton<OcrQualityChecker>();
builder.Services.AddSingleton<VisionOcrService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
