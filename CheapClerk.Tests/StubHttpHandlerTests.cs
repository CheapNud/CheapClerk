using System.Net;
using System.Text;
using Xunit;

namespace CheapClerk.Tests;

public class StubHttpHandlerTests
{
    [Fact]
    public async Task SendAsync_RecordsRequestAndBody_ReturnsCannedResponse()
    {
        StubHttpHandler stub = new(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"),
        });
        using HttpClient client = new(stub);
        const string requestBody = "{\"name\":\"test\"}";
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using HttpResponseMessage response = await client.PostAsync(
            "https://example.test/api/resource",
            new StringContent(requestBody, Encoding.UTF8, "application/json"),
            cancellationToken);

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"ok\":true}", responseBody);
        Assert.Single(stub.Requests);
        Assert.Equal(HttpMethod.Post, stub.Requests[0].Method);
        Assert.Equal("https://example.test/api/resource", stub.Requests[0].RequestUri?.ToString());
        Assert.Single(stub.RequestBodies);
        Assert.Equal(requestBody, stub.RequestBodies[0]);
    }
}
