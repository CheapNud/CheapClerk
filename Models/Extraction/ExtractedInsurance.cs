using System.ComponentModel;

namespace CheapClerk.Models.Extraction;

public sealed class ExtractedInsurance
{
    [Description("The name of the insurance company.")]
    public string? Insurer { get; set; }

    [Description("The policy number as printed on the document.")]
    public string? PolicyNumber { get; set; }

    [Description("The type of insurance (e.g. home, car, health, liability).")]
    public string? PolicyType { get; set; }

    [Description("The annual or monthly premium amount, as a decimal number.")]
    public decimal? PremiumAmount { get; set; }

    [Description("How the premium is billed: annual, monthly, quarterly.")]
    public string? PremiumFrequency { get; set; }

    [Description("The deductible amount per claim, as a decimal number.")]
    public decimal? Deductible { get; set; }

    [Description("The coverage start date, in yyyy-MM-dd format.")]
    public string? CoverageStart { get; set; }

    [Description("The coverage end date, in yyyy-MM-dd format.")]
    public string? CoverageEnd { get; set; }

    [Description("A brief description of what is insured (e.g. property address, vehicle make/model).")]
    public string? InsuredItem { get; set; }
}
