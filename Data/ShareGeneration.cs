namespace CheapClerk.Data;

/// <summary>
/// Per-document share generation: the revocation half of the stateless share
/// token. Tokens embed the generation they were minted under; bumping it makes
/// every outstanding link for that document fail validation instantly.
/// </summary>
public sealed class ShareGeneration
{
    public int DocumentId { get; set; }
    public int Generation { get; set; }
}
