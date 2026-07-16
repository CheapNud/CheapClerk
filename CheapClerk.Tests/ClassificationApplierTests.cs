using System.Net;
using System.Text;
using CheapClerk.Configuration;
using CheapClerk.Models;
using CheapClerk.Models.Classification;
using CheapClerk.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CheapClerk.Tests;

public sealed class ClassificationApplierTests
{
    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static ClassificationApplier BuildApplier(StubHttpHandler stub, ClassificationOptions? classificationConfig = null) =>
        new(
            new PaperlessClient(
                new HttpClient(stub) { BaseAddress = new Uri("http://paperless.test/") },
                Options.Create(new PaperlessOptions()),
                NullLogger<PaperlessClient>.Instance),
            Options.Create(classificationConfig ?? new ClassificationOptions()),
            NullLogger<ClassificationApplier>.Instance);

    private static TagContext BuildTagContext() => new(
        InboxTagId: 1,
        ReviewTagId: 2,
        TagLookup: new Dictionary<int, string> { [1] = "Inbox", [2] = "Needs Review", [3] = "Utilities" },
        ClassifiableTagLookup: new Dictionary<int, string> { [3] = "Utilities" },
        CorrespondentLookup: new Dictionary<int, string> { [5] = "Engie" },
        DocumentTypeLookup: new Dictionary<int, string> { [9] = "Invoice" });

    [Fact]
    public async Task Apply_RemovesInboxAndReviewTags_AndKeepsOtherExistingTags()
    {
        var stub = new StubHttpHandler(_ => Ok("{}"));
        var applier = BuildApplier(stub);
        var doc = new PaperlessDocument { Id = 42, Title = "scan", Tags = [1, 2, 3] };
        var classification = new ClassificationResult
        {
            SuggestedTitle = "Engie factuur",
            Correspondent = "Engie",
            DocumentType = "Invoice",
            Tags = ["Utilities"],
            Confidence = 0.9
        };

        var applied = await applier.ApplyAsync(doc, classification, BuildTagContext());

        Assert.NotNull(applied);
        var patchBody = stub.RequestBodies.Last()!;
        Assert.Contains("\"tags\":[3]", patchBody);          // inbox(1) and review(2) gone, Utilities(3) kept once
        Assert.Contains("\"correspondent\":5", patchBody);
        Assert.Contains("\"document_type\":9", patchBody);
        Assert.Contains("Engie factuur", patchBody);
    }

    [Fact]
    public async Task Apply_RecoversTagFromCrossHostRace_ByRematchingName()
    {
        // Create fails (duplicate-name 400 — another host won the race), but the
        // fresh tag fetch finds it; the tag must land on the document anyway
        var stub = new StubHttpHandler(incoming =>
        {
            if (incoming.Method == HttpMethod.Post && incoming.RequestUri!.AbsolutePath.Contains("api/tags"))
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            if (incoming.Method == HttpMethod.Get && incoming.RequestUri!.AbsolutePath.Contains("api/tags"))
                return Ok("{\"count\":1,\"next\":null,\"results\":[{\"id\":33,\"name\":\"Water\",\"document_count\":0}]}");
            return Ok("{}");
        });
        var applier = BuildApplier(stub);
        var doc = new PaperlessDocument { Id = 42, Title = "scan", Tags = [1] };
        var classification = new ClassificationResult
        {
            SuggestedTitle = "Waterfactuur",
            Tags = ["Water"],
            Confidence = 0.9
        };

        var applied = await applier.ApplyAsync(doc, classification, BuildTagContext());

        Assert.NotNull(applied);
        Assert.Contains("\"tags\":[33]", stub.RequestBodies.Last()!);
        Assert.Contains("Water", applied!.AppliedTags);
    }

    [Fact]
    public async Task Apply_ClampsTitleTo128Chars()
    {
        var stub = new StubHttpHandler(_ => Ok("{}"));
        var applier = BuildApplier(stub);
        var doc = new PaperlessDocument { Id = 7, Title = "scan", Tags = [] };
        var classification = new ClassificationResult
        {
            SuggestedTitle = new string('x', 300),
            Tags = [],
            Confidence = 0.9
        };

        var applied = await applier.ApplyAsync(doc, classification, BuildTagContext());

        Assert.NotNull(applied);
        Assert.Equal(128, applied!.NewTitle!.Length);
    }

    [Fact]
    public async Task Apply_NeverCreatesWorkflowTagNames_FromSuggestions()
    {
        var stub = new StubHttpHandler(incoming =>
            incoming.Method == HttpMethod.Post
                ? Ok("{\"id\": 77, \"name\": \"Verzekering\"}")
                : Ok("{}"));
        var applier = BuildApplier(stub);
        var doc = new PaperlessDocument { Id = 7, Title = "scan", Tags = [] };
        var classification = new ClassificationResult
        {
            Tags = ["Inbox", "Needs Review", "Verzekering"],
            Confidence = 0.9
        };

        var applied = await applier.ApplyAsync(doc, classification, BuildTagContext());

        Assert.NotNull(applied);
        var createCalls = stub.Requests.Count(r => r.Method == HttpMethod.Post);
        Assert.Equal(1, createCalls);                        // only Verzekering created
        Assert.Contains("\"tags\":[77]", stub.RequestBodies.Last()!);
    }

    [Fact]
    public async Task Apply_ReturnsNull_WhenPatchFails()
    {
        var stub = new StubHttpHandler(incoming =>
            incoming.Method == HttpMethod.Patch
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                : Ok("{}"));
        var applier = BuildApplier(stub);
        var doc = new PaperlessDocument { Id = 7, Title = "scan", Tags = [] };

        var applied = await applier.ApplyAsync(doc, new ClassificationResult { Confidence = 0.9 }, BuildTagContext());

        Assert.Null(applied);
    }
}
