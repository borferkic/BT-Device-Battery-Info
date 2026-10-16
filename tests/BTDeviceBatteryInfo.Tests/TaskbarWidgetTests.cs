using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.ViewModels;

namespace BTDeviceBatteryInfo.Tests;

public class TaskbarGracePeriodTests
{
    private static readonly DateTime Start = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(45);

    private static BluetoothDeviceItem Keyboard(string id = "kb") =>
        new("MX Keys S", true, 80, BluetoothDeviceCategory.Keyboard, id);

    [Fact]
    public void DeviceThatDropsBrieflyKeepsItsPillAsStale()
    {
        var recent = new Dictionary<string, (BluetoothDeviceItem, DateTime)>(StringComparer.OrdinalIgnoreCase);
        TaskbarWidgetController.MergeRecentDevices(recent, [Keyboard()], Start, Grace, out _);

        var devices = TaskbarWidgetController.MergeRecentDevices(recent, [], Start.AddSeconds(10), Grace, out var stale);

        Assert.Single(devices);
        Assert.Contains("kb", stale);
    }

    [Fact]
    public void DeviceDisappearsAfterTheGracePeriod()
    {
        var recent = new Dictionary<string, (BluetoothDeviceItem, DateTime)>(StringComparer.OrdinalIgnoreCase);
        TaskbarWidgetController.MergeRecentDevices(recent, [Keyboard()], Start, Grace, out _);

        var devices = TaskbarWidgetController.MergeRecentDevices(recent, [], Start.AddSeconds(46), Grace, out var stale);

        Assert.Empty(devices);
        Assert.Empty(stale);
        Assert.Empty(recent);
    }

    [Fact]
    public void ConnectedDeviceIsNeverStaleNorDuplicated()
    {
        var recent = new Dictionary<string, (BluetoothDeviceItem, DateTime)>(StringComparer.OrdinalIgnoreCase);
        TaskbarWidgetController.MergeRecentDevices(recent, [Keyboard()], Start, Grace, out _);

        var devices = TaskbarWidgetController.MergeRecentDevices(recent, [Keyboard()], Start.AddSeconds(4), Grace, out var stale);

        Assert.Single(devices);
        Assert.Empty(stale);
    }
}

public class StartMinimizedTests
{
    [Fact]
    public void OpensNormallyByDefault() =>
        Assert.False(App.ShouldStartMinimized(new AppSettings(), []));

    [Fact]
    public void SettingStartsMinimized() =>
        Assert.True(App.ShouldStartMinimized(new AppSettings { StartMinimized = true }, []));

    [Theory]
    [InlineData("--minimized")]
    [InlineData("--MINIMIZED")]
    public void ArgumentStartsMinimized(string argument) =>
        Assert.True(App.ShouldStartMinimized(new AppSettings(), [argument]));
}
