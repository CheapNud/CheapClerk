using QRCoder;

namespace CheapClerk.Web.Services;

/// <summary>Renders an EPC payment payload as a scannable QR code image.</summary>
public static class PaymentQrRenderer
{
    /// <summary>Converts an EPC069-12 payload into a PNG data URI, or null when rendering fails.</summary>
    public static string? ToDataUri(string epcPayload)
    {
        try
        {
            using var generator = new QRCodeGenerator();
            using var qrData = generator.CreateQrCode(epcPayload, QRCodeGenerator.ECCLevel.M);
            using var pngQr = new PngByteQRCode(qrData);
            var pngBytes = pngQr.GetGraphic(20);

            return $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
        }
        catch
        {
            // Rendering failure hides the payment section rather than crashing the page.
            return null;
        }
    }
}
