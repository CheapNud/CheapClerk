using MudBlazor;

namespace CheapClerk.Web.Theme;

public static class ClerkTheme
{
    public static readonly MudTheme Instance = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1565C0",
            Secondary = "#FF8F00",
            AppbarBackground = "#0D47A1"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#42A5F5",
            Secondary = "#FFB74D",
            Tertiary = "#81C784",
            AppbarBackground = "#0a0a0a",
            Background = "#0a0a0a",
            Surface = "#141414",
            DrawerBackground = "#0f0f0f",
            DrawerText = "rgba(255,255,255,0.7)",
            TextPrimary = "rgba(255,255,255,0.9)",
            TextSecondary = "rgba(255,255,255,0.5)",
            ActionDefault = "rgba(255,255,255,0.5)",
            Divider = "rgba(255,255,255,0.06)"
        }
    };
}
