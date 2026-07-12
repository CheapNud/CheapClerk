using System.Net;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Models;
using CheapClerk.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheapClerk.Tests;

public sealed class PaperlessClientUploadTests
{
    internal static PaperlessClient BuildClient(StubHttpHandler stub) =>
        new(
            new HttpClient(stub) { BaseAddress = new Uri("http://paperless.test/") },
            Options.Create(new PaperlessOptions()),
            NullLogger<PaperlessClient>.Instance);

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task UploadDocumentAsync_SuccessfulUpload_ReturnsUnquotedUuid()
    {
        var stub = new StubHttpHandler(_ => Ok("\"abc-123\""));
        var paperless = BuildClient(stub);
        var fileBytes = "PDF content"u8.ToArray();

        var taskId = await paperless.UploadDocumentAsync(fileBytes, "factuur.pdf");

        Assert.Equal("abc-123", taskId);
        var sent = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("http://paperless.test/api/documents/post_document/", sent.RequestUri!.ToString());
        var body = stub.RequestBodies[0]!;
        Assert.Contains("name=document", body);
        Assert.Contains("filename=factuur.pdf", body);
    }

    [Fact]
    public async Task UploadDocumentAsync_HttpError_ReturnsNull()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var paperless = BuildClient(stub);
        var fileBytes = "PDF content"u8.ToArray();

        var taskId = await paperless.UploadDocumentAsync(fileBytes, "factuur.pdf");

        Assert.Null(taskId);
    }

    [Fact]
    public async Task GetTaskStatusAsync_WithResults_ReturnsParsedStatus()
    {
        var stub = new StubHttpHandler(_ => Ok(
            "[{\"task_id\":\"abc\",\"status\":\"FAILURE\",\"result\":\"Not consuming duplicate.pdf: it is a duplicate\",\"related_document\":null}]"));
        var paperless = BuildClient(stub);

        var status = await paperless.GetTaskStatusAsync("abc");

        Assert.NotNull(status);
        Assert.Equal("abc", status!.TaskId);
        Assert.Equal("FAILURE", status.Status);
        Assert.Equal("Not consuming duplicate.pdf: it is a duplicate", status.Result);
        Assert.Null(status.RelatedDocument);
    }

    [Fact]
    public async Task GetTaskStatusAsync_EmptyArray_ReturnsNull()
    {
        var stub = new StubHttpHandler(_ => Ok("[]"));
        var paperless = BuildClient(stub);

        var status = await paperless.GetTaskStatusAsync("nonexistent");

        Assert.Null(status);
    }
}
