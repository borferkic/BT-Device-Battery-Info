using System.Windows;
using Microsoft.Win32;
using Windows.UI.ViewManagement;

namespace BTDeviceBatteryInfo.Services;

public static class ThemeManager
{
    public const string SystemTheme = "System";
    public const string ElegantBlackTheme = "Elegant Black";

    private const string ThemeFolder = "Resources/Themes/";
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static ResourceDictionary? _resources;
    private static string? _themeName;
    private static bool _listening;
    // Kept alive so ColorValuesChanged keeps firing when the user changes the Windows accent color.
    private static UISettings? _uiSettings;

    /// <summary>True when the applied palette is light (System theme with Windows in light mode).</summary>
    public static bool IsLight { get; private set; }

    /// <summary>Raised on the UI thread after a palette has been applied.</summary>
    public static event EventHandler? ThemeChanged;

    public static void Apply(ResourceDictionary resources, string? themeName)
    {
        _resources = resources;
        _themeName = themeName;
        var isElegantBlack = themeName == ElegantBlackTheme;
        IsLight = !isElegantBlack && WindowsUsesLightTheme();
        var file = isElegantBlack ? "ElegantBlack" : IsLight ? "SystemLight" : "System";
        var theme = new ResourceDictionary { Source = new Uri($"pack://application:,,,/{ThemeFolder}{file}.xaml") };
        if (!isElegantBlack) ApplyWindowsAccent(theme, IsLight);

        var merged = resources.MergedDictionaries;
        var index = merged.ToList().FindIndex(dictionary =>
            dictionary.Source?.OriginalString.Contains(ThemeFolder, StringComparison.OrdinalIgnoreCase) == true);
        if (index >= 0) merged[index] = theme;
        else merged.Insert(0, theme);

        resources["AppFontFamily"] = isElegantBlack
            ? new System.Windows.Media.FontFamily(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Geist")
            : System.Windows.SystemFonts.MessageFontFamily;

        if (!_listening)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            try
            {
                _uiSettings = new UISettings();
                _uiSettings.ColorValuesChanged += OnColorValuesChanged;
            }
            catch
            {
                // Accent color is optional; the theme keeps its default highlight color.
            }
            _listening = true;
        }
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void StopListening()
    {
        if (!_listening) return;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        if (_uiSettings is not null) _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
        _uiSettings = null;
        _listening = false;
    }

    /// <summary>
    /// System theme highlight follows the Windows accent color, using the variants Windows 11 uses:
    /// AccentLight2 on dark, AccentDark1 on light. Status colors (success, warning, destructive) are unchanged.
    /// </summary>
    private static void ApplyWindowsAccent(ResourceDictionary theme, bool isLight)
    {
        try
        {
            var settings = _uiSettings ?? new UISettings();
            var primary = settings.GetColorValue(isLight ? UIColorType.AccentDark1 : UIColorType.AccentLight2);
            var hover = settings.GetColorValue(isLight ? UIColorType.AccentDark2 : UIColorType.AccentLight1);
            theme["ShadcnPrimaryBrush"] = Frozen(primary);
            theme["ShadcnPrimaryHoverBrush"] = Frozen(hover);
            theme["ShadcnRingBrush"] = Frozen(primary);
            theme["ShadcnPrimaryForegroundBrush"] = Frozen(ContrastingText(primary));
        }
        catch
        {
            // Keep the theme defaults if Windows does not provide the accent color.
        }
    }

    /// <summary>Near-black or near-white text, whichever reads better on the accent (never pure black or white).</summary>
    internal static Windows.UI.Color ContrastingText(Windows.UI.Color background)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        var luminance = 0.2126 * Channel(background.R) + 0.7152 * Channel(background.G) + 0.0722 * Channel(background.B);
        return luminance > 0.179
            ? Windows.UI.Color.FromArgb(255, 0x10, 0x10, 0x10)
            : Windows.UI.Color.FromArgb(255, 0xFD, 0xFD, 0xFD);
    }

    private static System.Windows.Media.SolidColorBrush Frozen(Windows.UI.Color color)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private static void OnColorValuesChanged(UISettings sender, object args)
    {
        if (_resources is null || _themeName == ElegantBlackTheme) return;
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Apply(_resources, _themeName));
    }

    public static System.Windows.Media.Brush Brush(string key) =>
        System.Windows.Application.Current?.TryFindResource(key) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Gray;

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General || _resources is null || _themeName == ElegantBlackTheme) return;
        if (WindowsUsesLightTheme() == IsLight) return;
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Apply(_resources, _themeName));
    }

    private static bool WindowsUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }
}
