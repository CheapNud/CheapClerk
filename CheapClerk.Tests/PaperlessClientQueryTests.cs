using System.Net;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheapClerk.Tests;

public sealed class PaperlessClientQueryTests
{
    internal static PaperlessClient BuildClient(StubHttpHandler stub) =>
        new(
            new HttpClient(stub) { BaseAddress = new Uri("http://paperless.test/") },
            Options.Create(new PaperlessOptions()),
            NullLogger<PaperlessClient>.Instance);

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task DeleteDocument_SendsDeleteRequest_AndReturnsTrue()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var paperless = BuildClient(stub);

        var deleted = await paperless.DeleteDocumentAsync(42);

        Assert.True(deleted);
        var sent = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Delete, sent.Method);
        Assert.Equal("http://paperless.test/api/documents/42/", sent.RequestUri!.ToString());
    }

    [Fact]
    public async Task DeleteDocument_ReturnsFalseOnHttpError()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var paperless = BuildClient(stub);

        var deleted = await paperless.DeleteDocumentAsync(42);

        Assert.False(deleted);
    }

    [Fact]
    public async Task GetTags_FollowsNextLinks_AcrossPages()
    {
        var stub = new StubHttpHandler(incoming =>
            incoming.RequestUri!.Query.Contains("page=2")
                ? Ok("{\"count\":2,\"next\":null,\"results\":[{\"id\":2,\"name\":\"Second\",\"document_count\":0}]}")
                : Ok("{\"count\":2,\"next\":\"http://paperless.test/api/tags/?page=2&page_size=100\",\"results\":[{\"id\":1,\"name\":\"First\",\"document_count\":0}]}"));
        var paperless = BuildClient(stub);

        var tags = await paperless.GetTagsAsync();

        Assert.Equal(2, tags.Count);
        Assert.Equal(["First", "Second"], tags.Select(t => t.Name).ToArray());
    }

    [Fact]
    public async Task SearchDocuments_WithDocumentTypeName_AppendsDocumentTypeFilter()
    {
        var stub = new StubHttpHandler(incoming =>
        {
            var path = incoming.RequestUri!.AbsoluteUri;
            if (path.Contains("document_types"))
                return Ok("{\"count\":1,\"results\":[{\"id\":5,\"name\":\"Invoice\",\"document_count\":10}]}");
            return Ok("{\"count\":0,\"results\":[]}");
        });
        var paperless = BuildClient(stub);

        await paperless.SearchDocumentsAsync("test", documentTypeName: "Invoice");

        var searchRequest = stub.Requests.First(r => r.RequestUri!.AbsoluteUri.Contains("documents/?"));
        Assert.Contains("document_type__id=5", searchRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchDocuments_WithUnknownDocumentTypeName_DropsFilter()
    {
        var stub = new StubHttpHandler(incoming =>
        {
            // Populated type lookup proves the filter drop comes from the name
            // mismatch, not an accidentally empty lookup
            if (incoming.Method == HttpMethod.Get && incoming.RequestUri!.AbsolutePath.Contains("api/document_types"))
                return Ok("{\"count\":1,\"results\":[{\"id\":5,\"name\":\"Invoice\",\"document_count\":10}]}");
            return Ok("{\"count\":0,\"results\":[]}");
        });
        var paperless = BuildClient(stub);

        await paperless.SearchDocumentsAsync("test", documentTypeName: "UnknownType");

        var searchRequest = stub.Requests.FirstOrDefault(r => r.RequestUri!.Query.Contains("query="));
        Assert.NotNull(searchRequest);
        Assert.DoesNotContain("document_type__id", searchRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task ListDocuments_WithDocumentTypeName_AppendsDocumentTypeFilter()
    {
        var stub = new StubHttpHandler(incoming =>
        {
            var path = incoming.RequestUri!.AbsoluteUri;
            if (path.Contains("document_types"))
                return Ok("{\"count\":1,\"results\":[{\"id\":7,\"name\":\"Contract\",\"document_count\":15}]}");
            return Ok("{\"count\":0,\"results\":[]}");
        });
        var paperless = BuildClient(stub);

        await paperless.ListDocumentsAsync(documentTypeName: "Contract");

        var listRequest = stub.Requests.First(r => r.RequestUri!.AbsoluteUri.Contains("documents/?"));
        Assert.Contains("document_type__id=7", listRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task ListDocuments_WithUnknownDocumentTypeName_DropsFilter()
    {
        var stub = new StubHttpHandler(incoming =>
        {
            // Populated lookup, non-matching name — same rationale as the search variant
            if (incoming.Method == HttpMethod.Get && incoming.RequestUri!.AbsolutePath.Contains("api/document_types"))
                return Ok("{\"count\":1,\"results\":[{\"id\":7,\"name\":\"Invoice\",\"document_count\":3}]}");
            return Ok("{\"count\":0,\"results\":[]}");
        });
        var paperless = BuildClient(stub);

        await paperless.ListDocumentsAsync(documentTypeName: "UnknownType");

        var listRequest = stub.Requests.FirstOrDefault(r => r.RequestUri!.Query.Contains("ordering="));
        Assert.NotNull(listRequest);
        Assert.DoesNotContain("document_type__id", listRequest!.RequestUri!.Query);
    }
}
