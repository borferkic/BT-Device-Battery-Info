using System.IO;
using System.Text.Json;
using BTDeviceBatteryInfo.Models;

namespace BTDeviceBatteryInfo.Services;

public static class SettingsService
{
    private static readonly string Path = System.IO.Path.Combine(AppIdentity.DataDirectory, "settings.json");
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static Task<AppSettings> LoadAsync() => LoadAsync(Path);

    public static Task SaveAsync(AppSettings settings) => SaveAsync(settings, Path);

    /// <summary>Loads settings; a missing, unreadable, or corrupt file yields defaults. Missing fields keep their defaults.</summary>
    internal static async Task<AppSettings> LoadAsync(string path)
    {
        await Gate.WaitAsync();
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<AppSettings>(file) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
        finally { Gate.Release(); }
    }

    /// <summary>Writes settings atomically through a temporary file.</summary>
    internal static async Task SaveAsync(AppSettings settings, string path)
    {
        await Gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(file, settings, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temporary, path, true);
        }
        finally { Gate.Release(); }
    }
}
