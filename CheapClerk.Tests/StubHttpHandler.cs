namespace CheapClerk.Tests;

public sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string?> RequestBodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage outgoing, CancellationToken cancellationToken)
    {
        Requests.Add(outgoing);
        RequestBodies.Add(outgoing.Content is null
            ? null
            : await outgoing.Content.ReadAsStringAsync(cancellationToken));
        return responder(outgoing);
    }
}
