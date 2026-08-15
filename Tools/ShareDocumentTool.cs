using System.ComponentModel;
using CheapClerk.Configuration;
using CheapClerk.Services;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class ShareDocumentTool
{
    [McpServerTool(Name = "share_document"), Description("Create a time-limited public share link for a document (SAS-style, no account needed to open it). Requires Share:SigningKey and Web:PublicBaseUrl. Note: revocation state lives in the cache database, so point this host at the same cache DB as the web app for revokes to be consistent.")]
    public static async Task<string> ShareDocument(
        PaperlessClient paperlessClient,
        ShareGenerationStore shareGenerations,
        IOptions<ShareOptions> shareOptions,
        IOptions<WebOptions> webOptions,
        [Description("The Paperless document ID to share.")] int documentId,
        [Description("Hours until the link expires (1-168, default 24).")] int hours = 24,
        CancellationToken cancellationToken = default)
    {
        var signingKey = shareOptions.Value.SigningKey;
        if (string.IsNullOrWhiteSpace(signingKey))
            return "Sharing is disabled: Share:SigningKey is not configured.";

        var publicBaseUrl = webOptions.Value.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
            return "Sharing needs Web:PublicBaseUrl so the link is reachable by the recipient.";

        var doc = await paperlessClient.GetDocumentAsync(documentId, cancellationToken);
        if (doc is null)
            return $"Document {documentId} not found.";

        var boundedHours = Math.Clamp(hours, 1, 168);
        var expiresUtc = DateTimeOffset.UtcNow.AddHours(boundedHours);
        var generation = await shareGenerations.GetAsync(documentId, cancellationToken);
        var shareToken = ShareLinkBuilder.CreateToken(signingKey, documentId, generation, expiresUtc);

        return $"""
            Share link for '{doc.Title}' (#{documentId}), valid until {expiresUtc:yyyy-MM-dd HH:mm} UTC ({boundedHours}h):
            {publicBaseUrl.TrimEnd('/')}/share/{shareToken}

            Anyone with this link can download the document until it expires. Use revoke_document_shares to kill all outstanding links for this document early.
            """;
    }

    [McpServerTool(Name = "revoke_document_shares"), Description("Instantly invalidate every outstanding share link for a document (bumps its share generation).")]
    public static async Task<string> RevokeDocumentShares(
        ShareGenerationStore shareGenerations,
        [Description("The Paperless document ID whose share links should all stop working.")] int documentId,
        CancellationToken cancellationToken = default)
    {
        await shareGenerations.BumpAsync(documentId, cancellationToken);
        return $"All outstanding share links for document {documentId} are now invalid. New links can be created as usual.";
    }
}
