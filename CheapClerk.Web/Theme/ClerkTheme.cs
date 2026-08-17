using MudBlazor;

namespace CheapClerk.Web.Theme;

/// <summary>
/// House design language (global UI/UX rules): flat surfaces separated by
/// hairline borders, cards lifting off the field in both modes, a dark drawer
/// as structural chrome everywhere, and dark-mode text as an opacity ladder.
/// </summary>
public static class ClerkTheme
{
    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1565C0",
            Secondary = "#FF8F00",
            Tertiary = "#2E7D32",
            // White cards on an off-white field — cards lift, never blend
            Background = "#F5F5F7",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#1a1a1a",
            // The drawer stays dark in both modes: structural chrome, not content
            DrawerBackground = "#0f0f0f",
            DrawerText = "rgba(255,255,255,0.70)",
            DrawerIcon = "rgba(255,255,255,0.70)",
            // MudBlazor's default lines are too heavy — explicit hairlines
            LinesDefault = "#E3E3E7",
            TableLines = "#ECECEF",
            Divider = "#E3E3E7"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#42A5F5",
            Secondary = "#FFB74D",
            Tertiary = "#81C784",
            AppbarBackground = "#0a0a0a",
            Background = "#0a0a0a",
            // Cards lighter than the field
            Surface = "#141414",
            DrawerBackground = "#0f0f0f",
            DrawerText = "rgba(255,255,255,0.70)",
            DrawerIcon = "rgba(255,255,255,0.70)",
            // Text is an opacity ladder, not grey values
            TextPrimary = "rgba(255,255,255,0.92)",
            TextSecondary = "rgba(255,255,255,0.62)",
            ActionDefault = "rgba(255,255,255,0.62)",
            LinesDefault = "rgba(255,255,255,0.12)",
            TableLines = "rgba(255,255,255,0.10)",
            Divider = "rgba(255,255,255,0.12)"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            DrawerWidthLeft = "260px"
        },
        Typography = new Typography
        {
            // Two fonts, split by role: DIN-lineage display for headings
            // (Bahnschrift native on Windows, Barlow self-hosted elsewhere),
            // humanist sans for everything else (Segoe on Windows, Roboto on
            // Android — no hosted body font needed).
            Default = new DefaultTypography
            {
                FontFamily = ["Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"]
            },
            H4 = new H4Typography { FontFamily = ["Bahnschrift", "Barlow", "sans-serif"] },
            H5 = new H5Typography { FontFamily = ["Bahnschrift", "Barlow", "sans-serif"] },
            H6 = new H6Typography { FontFamily = ["Bahnschrift", "Barlow", "sans-serif"] }
        }
    };
}
