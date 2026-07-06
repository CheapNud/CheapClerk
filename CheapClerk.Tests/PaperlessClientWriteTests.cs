using System.Net;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Models;
using CheapClerk.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheapClerk.Tests;

public sealed class PaperlessClientWriteTests
{
    internal static PaperlessClient BuildClient(StubHttpHandler stub) =>
        new(
            new HttpClient(stub) { BaseAddress = new Uri("http://paperless.test/") },
            Options.Create(new PaperlessOptions()),
            NullLogger<PaperlessClient>.Instance);

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task UpdateDocument_SendsPatchWithOnlyProvidedFields()
    {
        var stub = new StubHttpHandler(_ => Ok("{}"));
        var paperless = BuildClient(stub);

        var applied = await paperless.UpdateDocumentAsync(42, new DocumentUpdate
        {
            Title = "KBC Woonverzekering 2026",
            TagIds = [3, 7]
        });

        Assert.True(applied);
        var sent = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Patch, sent.Method);
        Assert.Equal("http://paperless.test/api/documents/42/", sent.RequestUri!.ToString());
        var body = stub.RequestBodies[0]!;
        Assert.Contains("\"title\":\"KBC Woonverzekering 2026\"", body);
        Assert.Contains("\"tags\":[3,7]", body);
        Assert.DoesNotContain("correspondent", body);
        Assert.DoesNotContain("document_type", body);
        Assert.DoesNotContain("created", body);
    }

    [Fact]
    public async Task UpdateDocument_ReturnsFalseOnHttpError()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var paperless = BuildClient(stub);

        var applied = await paperless.UpdateDocumentAsync(42, new DocumentUpdate { Title = "x" });

        Assert.False(applied);
    }
}
