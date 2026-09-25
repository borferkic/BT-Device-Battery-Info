using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace BTDeviceBatteryInfo.Services;

/// <summary>A published release that is newer than the running version.</summary>
public sealed record AvailableUpdate(string Version, string Url);

/// <summary>
/// Checks GitHub Releases once for a newer version. This is the only network request of the application:
/// it sends no data about the user or their devices, and any failure is silently ignored.
/// </summary>
public static class UpdateChecker
{
    private const string LatestReleaseApi = "https://api.github.com/repos/borferkic/BT-Device-Battery-Info/releases/latest";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>Version of the running build, e.g. "0.22" or "0.22-dev3" (build metadata after '+' removed).</summary>
    public static string CurrentVersion
    {
        get
        {
            var informational = typeof(UpdateChecker).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }
    }

    public static async Task<AvailableUpdate?> CheckAsync(FileLogger logger)
    {
        try
        {
            using var http = new HttpClient { Timeout = Timeout };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BTDeviceBatteryInfo/" + CurrentVersion);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            await using var stream = await http.GetStreamAsync(LatestReleaseApi);
            using var json = await JsonDocument.ParseAsync(stream);
            var tag = json.RootElement.GetProperty("tag_name").GetString();
            var url = json.RootElement.GetProperty("html_url").GetString();
            if (tag is null || url is null) return null;

            var latest = tag.TrimStart('v', 'V');
            if (CompareVersions(latest, CurrentVersion) <= 0)
            {
                await logger.LogAsync($"Update check: {CurrentVersion} is up to date (latest release {latest}).");
                return null;
            }
            await logger.LogAsync($"Update check: {latest} is available (running {CurrentVersion}).");
            return new AvailableUpdate(latest, url);
        }
        catch (Exception ex)
        {
            await logger.LogAsync("Update check skipped: " + ex.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// Compares versions such as "0.21", "0.22" and "0.22-dev3". Numeric parts are compared first (missing
    /// parts count as 0); with equal numbers, a pre-release ("-dev3") is lower than the release without suffix,
    /// and dev builds compare by their number.
    /// </summary>
    public static int CompareVersions(string left, string right)
    {
        var (leftNumbers, leftSuffix) = Split(left);
        var (rightNumbers, rightSuffix) = Split(right);
        for (var i = 0; i < Math.Max(leftNumbers.Length, rightNumbers.Length); i++)
        {
            var comparison = (i < leftNumbers.Length ? leftNumbers[i] : 0).CompareTo(i < rightNumbers.Length ? rightNumbers[i] : 0);
            if (comparison != 0) return comparison;
        }
        if (leftSuffix is null) return rightSuffix is null ? 0 : 1;
        if (rightSuffix is null) return -1;
        return SuffixNumber(leftSuffix).CompareTo(SuffixNumber(rightSuffix)) is var byNumber and not 0
            ? byNumber
            : string.CompareOrdinal(leftSuffix, rightSuffix);
    }

    private static (int[] Numbers, string? Suffix) Split(string version)
    {
        var dash = version.IndexOf('-');
        var core = dash >= 0 ? version[..dash] : version;
        var suffix = dash >= 0 ? version[(dash + 1)..] : null;
        var numbers = core.Split('.').Select(part => int.TryParse(part, out var number) ? number : 0).ToArray();
        return (numbers, string.IsNullOrWhiteSpace(suffix) ? null : suffix);
    }

    private static int SuffixNumber(string suffix)
    {
        var digits = new string(suffix.SkipWhile(character => !char.IsDigit(character)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var number) ? number : 0;
    }
}
