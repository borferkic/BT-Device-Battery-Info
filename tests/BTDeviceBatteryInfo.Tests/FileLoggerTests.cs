using System.IO;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.Tests;

public sealed class FileLoggerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "btdbi-log-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string DailyFile(DateTime date) => Path.Combine(_folder, $"battery-info-{date:yyyyMMdd}.log");

    [Fact]
    public void RetentionKeepsTheLastFourteenDaysOnly()
    {
        var logger = new FileLogger(_folder);
        var today = new DateTime(2026, 9, 25);
        var kept = today.AddDays(-(FileLogger.RetentionDays - 1));
        var expired = today.AddDays(-FileLogger.RetentionDays);
        File.WriteAllText(DailyFile(kept), "kept");
        File.WriteAllText(DailyFile(expired), "expired");
        File.WriteAllText(Path.Combine(_folder, "notes.txt"), "not a log");

        logger.DeleteExpiredFiles(today);

        Assert.True(File.Exists(DailyFile(kept)));
        Assert.False(File.Exists(DailyFile(expired)));
        Assert.True(File.Exists(Path.Combine(_folder, "notes.txt")));
    }

    [Fact]
    public async Task DailyFileStopsGrowingAtTheSizeLimit()
    {
        var logger = new FileLogger(_folder);
        var path = DailyFile(DateTime.Now);
        File.WriteAllText(path, new string('x', (int)FileLogger.MaxDailyBytes - 10));

        await logger.LogAsync("first entry past the limit");
        var afterLimit = new FileInfo(path).Length;
        await logger.LogAsync("skipped entry");

        var content = File.ReadAllText(path);
        Assert.Contains("Daily log size limit reached", content);
        Assert.DoesNotContain("skipped entry", content);
        Assert.Equal(afterLimit, new FileInfo(path).Length);
    }

    [Fact]
    public async Task EntriesAreTimestamped()
    {
        var logger = new FileLogger(_folder);
        await logger.LogAsync("hello");

        Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] hello", File.ReadAllText(DailyFile(DateTime.Now)));
    }
}
