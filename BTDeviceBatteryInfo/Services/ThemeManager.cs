using System.Windows;
using Microsoft.Win32;

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
            _listening = true;
        }
        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void StopListening()
    {
        if (!_listening) return;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _listening = false;
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
