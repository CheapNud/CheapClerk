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

    [Fact]
    public async Task CreateTag_PostsNameAndInboxFlag_AndInvalidatesLookup()
    {
        var callCount = 0;
        var stub = new StubHttpHandler(incoming =>
        {
            if (incoming.Method == HttpMethod.Post)
                return Ok("{\"id\": 99, \"name\": \"Inbox\", \"is_inbox_tag\": true}");
            callCount++;
            return Ok("{\"count\":0,\"results\":[]}");
        });
        var paperless = BuildClient(stub);

        await paperless.GetTagLookupAsync();          // primes cache (fetch #1)
        var created = await paperless.CreateTagAsync("Inbox", isInboxTag: true);
        await paperless.GetTagLookupAsync();          // must re-fetch (fetch #2)

        Assert.NotNull(created);
        Assert.Equal(99, created!.Id);
        Assert.True(created.IsInboxTag);
        Assert.Equal(2, callCount);
        var postBody = stub.RequestBodies.First(b => b is not null && b.Contains("Inbox"))!;
        Assert.Contains("\"is_inbox_tag\":true", postBody);
    }

    [Fact]
    public async Task GetDocumentTypes_ParsesPage()
    {
        var stub = new StubHttpHandler(_ =>
            Ok("{\"count\":1,\"results\":[{\"id\":5,\"name\":\"Invoice\",\"document_count\":3}]}"));
        var paperless = BuildClient(stub);

        var documentTypes = await paperless.GetDocumentTypesAsync();

        var only = Assert.Single(documentTypes);
        Assert.Equal("Invoice", only.Name);
    }
}
