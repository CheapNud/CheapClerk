using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CheapClerk.Data;

public sealed class CachedSuggestion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int DocumentId { get; set; }

    public string ClassificationJson { get; set; } = string.Empty;

    public double Confidence { get; set; }

    public DateTime SuggestedAtUtc { get; set; }
}
