using System.IO;

namespace BTDeviceBatteryInfo.Services;

/// <summary>
/// Local diagnostic log (English). One file per day, kept for <see cref="RetentionDays"/> days and capped at
/// <see cref="MaxDailyBytes"/> per day so a flapping device cannot grow the log without bound.
/// </summary>
public sealed class FileLogger
{
    public const int RetentionDays = 14;
    public const long MaxDailyBytes = 2 * 1024 * 1024;
    private const string FilePrefix = "battery-info-";

    private readonly string _folder;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _currentPath;
    private bool _limitReached;

    public FileLogger() : this(Path.Combine(AppIdentity.DataDirectory, "Logs"))
    {
    }

    internal FileLogger(string folder)
    {
        _folder = folder;
        Directory.CreateDirectory(_folder);
    }

    public async Task LogAsync(string message)
    {
        await _gate.WaitAsync();
        try
        {
            var now = DateTime.Now;
            var path = Path.Combine(_folder, $"{FilePrefix}{now:yyyyMMdd}.log");
            if (path != _currentPath)
            {
                // New day (or first write): start the daily file and drop files past the retention window.
                _currentPath = path;
                _limitReached = false;
                DeleteExpiredFiles(now);
            }
            if (_limitReached) return;

            var line = $"[{now:HH:mm:ss}] {message}{Environment.NewLine}";
            var length = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (length + line.Length > MaxDailyBytes)
            {
                _limitReached = true;
                line = $"[{now:HH:mm:ss}] Daily log size limit reached ({MaxDailyBytes / (1024 * 1024)} MB); further entries for today are skipped.{Environment.NewLine}";
            }
            await File.AppendAllTextAsync(path, line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never break the application.
        }
        finally
        {
            _gate.Release();
        }
    }

    internal void DeleteExpiredFiles(DateTime now)
    {
        var oldest = now.Date.AddDays(-(RetentionDays - 1));
        foreach (var file in Directory.EnumerateFiles(_folder, $"{FilePrefix}*.log"))
        {
            var stamp = Path.GetFileNameWithoutExtension(file)[FilePrefix.Length..];
            if (DateTime.TryParseExact(stamp, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date) && date < oldest)
            {
                try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
