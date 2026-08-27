using BTDeviceBatteryInfo.Models;
using System.IO;
using System.Text.Json;

namespace BTDeviceBatteryInfo.Services;

public static class SettingsService
{
    private static readonly string Path = System.IO.Path.Combine(AppIdentity.DataDirectory, "settings.json");
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<AppSettings> LoadAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (!File.Exists(Path)) return new AppSettings();
            await using var file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<AppSettings>(file) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
        finally { Gate.Release(); }
    }

    public static async Task SaveAsync(AppSettings settings)
    {
        await Gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temporary = Path + ".tmp";
            await using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(file, settings, new JsonSerializerOptions { WriteIndented = true });
            File.Move(temporary, Path, true);
        }
        finally { Gate.Release(); }
    }
}
