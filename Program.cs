using CheapClerk.Configuration;
using CheapClerk.Services;
using CheapClerk.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var paperlessSection = builder.Configuration.GetSection(PaperlessOptions.SectionName);
var visionSection = builder.Configuration.GetSection(VisionFallbackOptions.SectionName);

builder.Services.Configure<PaperlessOptions>(paperlessSection);
builder.Services.Configure<VisionFallbackOptions>(visionSection);

builder.Services.AddHttpClient<PaperlessClient>((sp, httpClient) =>
{
    var paperlessOptions = paperlessSection.Get<PaperlessOptions>()!;
    httpClient.BaseAddress = new Uri(paperlessOptions.BaseUrl.TrimEnd('/') + "/");
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Token {paperlessOptions.ApiToken}");
});

builder.Services.AddSingleton<OcrQualityChecker>();
builder.Services.AddSingleton<VisionOcrService>();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SearchDocumentsTool>()
    .WithTools<GetDocumentContentTool>()
    .WithTools<ListDocumentsTool>()
    .WithTools<GetDocumentMetadataTool>()
    .WithTools<ListTagsTool>();

// MCP stdio uses stdin/stdout — log to stderr only
builder.Logging.AddConsole(consoleOptions =>
{
    consoleOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

await builder.Build().RunAsync();
