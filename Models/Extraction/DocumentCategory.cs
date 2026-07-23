namespace CheapClerk.Models.Extraction;

public enum DocumentCategory
{
    Unknown,
    Invoice,
    Insurance,
    Contract,
    Receipt,
    TaxDocument,
    Warranty,
    BankStatement,
    // Appended last — the cache stores this enum as an integer
    Vehicle
}
