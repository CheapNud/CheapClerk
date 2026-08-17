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
    public async Task GetTaskStatus_PaginatedShape_Paperless3_ReturnsStatus()
    {
        // Paperless 3.x wraps /api/tasks/ in {count, next, previous, results}
        var stub = new StubHttpHandler(_ => Ok(
            "{\"count\":1,\"next\":null,\"previous\":null,\"results\":[{\"task_id\":\"uuid-9\",\"status\":\"SUCCESS\",\"result\":\"ok\",\"related_document\":\"7\"}]}"));
        var paperless = BuildClient(stub);

        var taskStatus = await paperless.GetTaskStatusAsync("uuid-9");

        Assert.NotNull(taskStatus);
        Assert.Equal("SUCCESS", taskStatus!.Status);
        Assert.Equal("7", taskStatus.RelatedDocument);
    }

    [Fact]
    public async Task GetTaskStatus_BareArrayShape_LegacyPaperless_ReturnsStatus()
    {
        var stub = new StubHttpHandler(_ => Ok(
            "[{\"task_id\":\"uuid-9\",\"status\":\"PENDING\",\"result\":null,\"related_document\":null}]"));
        var paperless = BuildClient(stub);

        var taskStatus = await paperless.GetTaskStatusAsync("uuid-9");

        Assert.NotNull(taskStatus);
        Assert.Equal("PENDING", taskStatus!.Status);
    }

    [Fact]
    public async Task GetTaskStatus_UnrecognizedShape_ReturnsNullInsteadOfThrowing()
    {
        var stub = new StubHttpHandler(_ => Ok("{\"unexpected\":true}"));
        var paperless = BuildClient(stub);

        Assert.Null(await paperless.GetTaskStatusAsync("uuid-9"));
    }

    [Fact]
    public async Task ListRecentTasks_PaginatedShape_ResolvesNestedFilename()
    {
        var stub = new StubHttpHandler(_ => Ok(
            "{\"count\":2,\"results\":[" +
            "{\"task_id\":\"a\",\"status\":\"started\",\"input_data\":{\"filename\":\"bill.pdf\"},\"date_created\":\"2026-08-17T10:00:00Z\"}," +
            "{\"task_id\":\"b\",\"status\":\"success\",\"input_data\":{\"filename\":\"scan.jpg\"},\"related_document\":\"7\"}]}"));
        var paperless = BuildClient(stub);

        var recentTasks = await paperless.ListRecentTasksAsync();

        Assert.NotNull(recentTasks);
        Assert.Equal(2, recentTasks!.Count);
        Assert.Equal("bill.pdf", recentTasks[0].Filename);
        Assert.Equal("started", recentTasks[0].Status);
    }

    [Fact]
    public async Task ListRecentTasks_FiltersOutPaperlessHousekeepingTasks()
    {
        var stub = new StubHttpHandler(_ => Ok(
            "{\"count\":3,\"results\":[" +
            "{\"task_id\":\"a\",\"status\":\"success\",\"task_type\":\"check_mail\"}," +
            "{\"task_id\":\"b\",\"status\":\"success\",\"task_type\":\"consume_file\",\"input_data\":{\"filename\":\"bill.pdf\"}}," +
            "{\"task_id\":\"c\",\"status\":\"success\",\"task_type\":\"train_classifier\"}]}"));
        var paperless = BuildClient(stub);

        var recentTasks = await paperless.ListRecentTasksAsync();

        Assert.Equal("bill.pdf", Assert.Single(recentTasks!).Filename);
    }

    [Fact]
    public async Task ListRecentTasks_LegacyArrayShape_ResolvesTopLevelFilename()
    {
        var stub = new StubHttpHandler(_ => Ok(
            "[{\"task_id\":\"a\",\"status\":\"SUCCESS\",\"task_file_name\":\"old.pdf\",\"related_document\":\"3\"}]"));
        var paperless = BuildClient(stub);

        var recentTasks = await paperless.ListRecentTasksAsync();

        Assert.NotNull(recentTasks);
        Assert.Equal("old.pdf", Assert.Single(recentTasks!).Filename);
    }

    [Fact]
    public async Task ListRecentTasks_ServerDown_ReturnsNullNotEmpty()
    {
        var stub = new StubHttpHandler(_ => throw new HttpRequestException(
            "Connection refused", new System.Net.Sockets.SocketException(111)));
        var paperless = BuildClient(stub);

        // Null must stay distinguishable from an empty queue
        Assert.Null(await paperless.ListRecentTasksAsync());
    }

    [Fact]
    public async Task UploadDocumentAsync_SuccessfulUpload_ReturnsUnquotedUuid()
    {
        var stub = new StubHttpHandler(_ => Ok("\"abc-123\""));
        var paperless = BuildClient(stub);
        var fileBytes = "PDF content"u8.ToArray();

        var attempt = await paperless.UploadDocumentAsync(fileBytes, "factuur.pdf");

        Assert.Equal("abc-123", attempt.TaskUuid);
        Assert.Null(attempt.FailureDetail);
        var sent = Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("http://paperless.test/api/documents/post_document/", sent.RequestUri!.ToString());
        var body = stub.RequestBodies[0]!;
        Assert.Contains("name=document", body);
        Assert.Contains("filename=factuur.pdf", body);
    }

    [Fact]
    public async Task UploadDocumentAsync_HttpError_ReportsRejectionWithStatusCode()
    {
        var stub = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var paperless = BuildClient(stub);
        var fileBytes = "PDF content"u8.ToArray();

        var attempt = await paperless.UploadDocumentAsync(fileBytes, "factuur.pdf");

        Assert.Null(attempt.TaskUuid);
        Assert.Contains("500", attempt.FailureDetail);
    }

    [Fact]
    public async Task UploadDocumentAsync_ConnectionRefused_ReportsUnreachableNotRejected()
    {
        var stub = new StubHttpHandler(_ => throw new HttpRequestException(
            "Connection refused", new System.Net.Sockets.SocketException(111)));
        var paperless = BuildClient(stub);

        var attempt = await paperless.UploadDocumentAsync("PDF"u8.ToArray(), "factuur.pdf");

        Assert.Null(attempt.TaskUuid);
        Assert.Contains("unreachable", attempt.FailureDetail);
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

    [Fact]
    public async Task GetTaskStatusAsync_ParsesNumericRelatedDocument()
    {
        var stub = new StubHttpHandler(_ => Ok(
            "[{\"task_id\":\"abc\",\"status\":\"SUCCESS\",\"result\":\"ok\",\"related_document\":5}]"));
        var paperless = BuildClient(stub);

        var status = await paperless.GetTaskStatusAsync("abc");

        Assert.NotNull(status);
        Assert.Equal("5", status!.RelatedDocument);
    }

    [Fact]
    public async Task GetTaskStatusAsync_ParsesStringRelatedDocument()
    {
        var stub = new StubHttpHandler(_ => Ok(
            "[{\"task_id\":\"abc\",\"status\":\"SUCCESS\",\"result\":\"ok\",\"related_document\":\"123\"}]"));
        var paperless = BuildClient(stub);

        var status = await paperless.GetTaskStatusAsync("abc");

        Assert.NotNull(status);
        Assert.Equal("123", status!.RelatedDocument);
    }
}
