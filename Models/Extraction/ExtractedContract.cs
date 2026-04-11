using System.ComponentModel;

namespace CheapClerk.Models.Extraction;

public sealed class ExtractedContract
{
    [Description("The name of the counterparty (supplier, landlord, service provider).")]
    public string? Counterparty { get; set; }

    [Description("A brief description of the contract subject (e.g. electricity supply, rental agreement).")]
    public string? Subject { get; set; }

    [Description("The contract start date, in yyyy-MM-dd format.")]
    public string? StartDate { get; set; }

    [Description("The contract end date, in yyyy-MM-dd format.")]
    public string? EndDate { get; set; }

    [Description("The notice period required to terminate, e.g. '3 months'.")]
    public string? NoticePeriod { get; set; }

    [Description("Whether the contract auto-renews.")]
    public bool? AutoRenewal { get; set; }

    [Description("The recurring amount billed, as a decimal number.")]
    public decimal? RecurringAmount { get; set; }

    [Description("How often the recurring amount is billed: monthly, quarterly, annual.")]
    public string? BillingFrequency { get; set; }
}
