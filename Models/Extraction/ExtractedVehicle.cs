using System.ComponentModel;

namespace CheapClerk.Models.Extraction;

public sealed class ExtractedVehicle
{
    [Description("License plate number.")]
    public string? LicensePlate { get; set; }

    [Description("Vehicle make (e.g. Mercedes, Toyota).")]
    public string? Make { get; set; }

    [Description("Vehicle model.")]
    public string? Model { get; set; }

    [Description("Chassis number / VIN.")]
    public string? ChassisNumber { get; set; }

    [Description("Date of first registration, yyyy-MM-dd.")]
    public string? FirstRegistrationDate { get; set; }

    [Description("Next technical inspection (keuring) due date, yyyy-MM-dd.")]
    public string? InspectionDueDate { get; set; }
}
