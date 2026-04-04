namespace CheapClerk.Models;

public sealed class DocumentMatch
{
    public int DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Excerpt { get; set; }
    public string? Correspondent { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTime? Created { get; set; }
}
