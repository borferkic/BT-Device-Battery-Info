using System.Windows;
using System.Windows.Media;

namespace BTDeviceBatteryInfo.Services;

public static class ThemeManager
{
    public const string SystemTheme = "System";
    public const string ElegantBlackTheme = "Elegant Black";

    public static void Apply(ResourceDictionary resources, string? themeName)
    {
        var isElegantBlack = themeName == ElegantBlackTheme;
        var colors = isElegantBlack
            ? new Dictionary<string, string>
            {
                ["ShadcnBackgroundBrush"] = "#09090B", ["ShadcnSurfaceBrush"] = "#111113",
                ["ShadcnElevatedBrush"] = "#18181B", ["ShadcnAccentBrush"] = "#27272A",
                ["ShadcnBorderBrush"] = "#27272A", ["ShadcnForegroundBrush"] = "#FAFAFA",
                ["ShadcnMutedBrush"] = "#A1A1AA", ["ShadcnPrimaryBrush"] = "#FAFAFA",
                ["ShadcnPrimaryForegroundBrush"] = "#18181B", ["ShadcnRingBrush"] = "#D4D4D8"
            }
            : new Dictionary<string, string>
            {
                ["ShadcnBackgroundBrush"] = "#202020", ["ShadcnSurfaceBrush"] = "#292929",
                ["ShadcnElevatedBrush"] = "#303030", ["ShadcnAccentBrush"] = "#3A3A3A",
                ["ShadcnBorderBrush"] = "#454545", ["ShadcnForegroundBrush"] = "#FFFFFF",
                ["ShadcnMutedBrush"] = "#B8B8B8", ["ShadcnPrimaryBrush"] = "#46D9D3",
                ["ShadcnPrimaryForegroundBrush"] = "#101010", ["ShadcnRingBrush"] = "#46D9D3"
            };

        foreach (var (key, hex) in colors)
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            resources[key] = new SolidColorBrush(color);
        }

        resources["ShadcnButtonCornerRadius"] = new CornerRadius(isElegantBlack ? 18 : 8);
        resources["AppFontFamily"] = isElegantBlack
            ? new System.Windows.Media.FontFamily(new Uri("pack://application:,,,/"), "./Resources/#Geist")
            : System.Windows.SystemFonts.MessageFontFamily;
    }
}
