using System.IO;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.Tests;

public class LifecycleTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 60)]
    [InlineData(20, 60)]
    public void WatcherRestartBacksOffUpToOneMinute(int failures, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), BluetoothService.GetWatcherRestartDelay(failures));

    [Fact]
    public void DisposeIsIdempotentAndSafeBeforeInitialization()
    {
        var folder = Path.Combine(Path.GetTempPath(), "btdbi-lifecycle-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new BluetoothService(new FileLogger(folder));
            service.Dispose();
            service.Dispose();
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
    }
}
