using System.IO;

namespace BTDeviceBatteryInfo.Services;

public sealed class FileLogger
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileLogger()
    {
        var folder = System.IO.Path.Combine(AppIdentity.DataDirectory, "Logs");
        Directory.CreateDirectory(folder);
        _path = System.IO.Path.Combine(folder, $"battery-info-{DateTime.Now:yyyyMMdd}.log");
    }

    public async Task LogAsync(string message)
    {
        await _gate.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_path, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
        finally
        {
            _gate.Release();
        }
    }
}
