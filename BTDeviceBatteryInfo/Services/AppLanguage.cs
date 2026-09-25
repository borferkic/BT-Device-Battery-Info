using System.Windows;

namespace BTDeviceBatteryInfo.Services;

/// <summary>
/// Swaps the user-interface string dictionary (Resources/Strings/Strings.{language}.xaml) at runtime.
/// English is the default and the fallback; diagnostic logs are never localized.
/// </summary>
public static class AppLanguage
{
    public const string English = "en";
    public const string Spanish = "es";

    private const string StringsFolder = "Resources/Strings/";
    private static ResourceDictionary? _fallback;

    public static string CurrentLanguage { get; private set; } = English;

    /// <summary>Raised after a language has been applied.</summary>
    public static event EventHandler? LanguageChanged;

    public static string Normalize(string? language) => language == Spanish ? Spanish : English;

    public static void Apply(ResourceDictionary resources, string? language)
    {
        CurrentLanguage = Normalize(language);
        var strings = new ResourceDictionary { Source = new Uri($"pack://application:,,,/BTDeviceBatteryInfo;component/{StringsFolder}Strings.{CurrentLanguage}.xaml") };

        var merged = resources.MergedDictionaries;
        var index = merged.ToList().FindIndex(dictionary =>
            dictionary.Source?.OriginalString.Contains(StringsFolder, StringComparison.OrdinalIgnoreCase) == true);
        if (index >= 0) merged[index] = strings;
        else merged.Add(strings);

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>Returns the string for <paramref name="key"/> (without the "Str." prefix), falling back to English.</summary>
    public static string Get(string key)
    {
        var resourceKey = "Str." + key;
        if (System.Windows.Application.Current?.TryFindResource(resourceKey) is string value) return value;
        _fallback ??= new ResourceDictionary { Source = new Uri($"pack://application:,,,/BTDeviceBatteryInfo;component/{StringsFolder}Strings.{English}.xaml") };
        return _fallback[resourceKey] as string ?? key;
    }

    public static string Format(string key, params object[] args) => string.Format(Get(key), args);
}
