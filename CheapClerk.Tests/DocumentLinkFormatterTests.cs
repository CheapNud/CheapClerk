using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class DocumentLinkFormatterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Links_ReturnsNull_WithoutPublicBaseUrl(string? publicBaseUrl) =>
        Assert.Null(DocumentLinkFormatter.Links(publicBaseUrl, 26));

    [Theory]
    [InlineData("https://clerk.example.com")]
    [InlineData("https://clerk.example.com/")]
    public void Links_BuildsViewerAndFileUrls_TrimmingTrailingSlash(string publicBaseUrl) =>
        Assert.Equal(
            "View: https://clerk.example.com/documents/26 | File: https://clerk.example.com/documents/26/file",
            DocumentLinkFormatter.Links(publicBaseUrl, 26));
}
