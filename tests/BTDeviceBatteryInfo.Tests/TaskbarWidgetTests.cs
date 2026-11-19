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

public class TaskbarLayoutTests
{
    // Taskbar geometry captured from a Windows 11 machine at 150% scaling with a crowded, centered taskbar:
    // widgets button 15-81, Start at 239, last app button ends at 1830, notification area starts at 1917.
    private static readonly (double Left, double Right, string Id)[] CrowdedWindows11 =
    [
        (15, 81, "WidgetsButton"), (239, 307, "StartButton"), (708, 774, "Appid: Explorer"), (1764, 1830, "Appid: Obsidian"),
        (1917, 1965, "NotifyItemIcon"), (2751, 2856, "SystemTrayIcon")
    ];

    [Fact]
    public void FindsTheFreeSpansAroundTheCenteredButtons()
    {
        var (left, right) = TaskbarLayout.FindGaps(0, 2880, 1917, CrowdedWindows11);

        Assert.Equal(new TaskbarGap(87, 231, false), left);
        Assert.Equal(new TaskbarGap(1836, 1909, true), right);
    }

    [Fact]
    public void ThreeDevicesDoNotFitFullSizeInTheCrowdedTaskbarButShrinkToTheLeftGap()
    {
        var (left, right) = TaskbarLayout.FindGaps(0, 2880, 1917, CrowdedWindows11);

        var placement = TaskbarLayout.Choose(3, 1.5, [right!.Value, left!.Value]);

        Assert.NotNull(placement);
        Assert.Equal(PillDensity.Minimal, placement.Value.Density);
        Assert.Equal(3, placement.Value.PillCount);
        Assert.False(placement.Value.Gap.NextToTray);
    }

    [Fact]
    public void KeepsTheFullSizeWhenItFitsThePreferredGap()
    {
        var placement = TaskbarLayout.Choose(3, 1.0, [new TaskbarGap(1000, 1500, true)]);

        Assert.Equal(PillDensity.Full, placement!.Value.Density);
    }

    [Fact]
    public void PreferredSideWinsAtASmallerSizeOverTheOtherSideAtFullSize()
    {
        var placement = TaskbarLayout.Choose(3, 1.0, [new TaskbarGap(1000, 1110, true), new TaskbarGap(0, 500, false)]);

        Assert.True(placement!.Value.Gap.NextToTray);
        Assert.Equal(PillDensity.Minimal, placement.Value.Density);
    }

    [Fact]
    public void ShowsOnlyThePillsThatFitWhenNothingFitsAll()
    {
        var placement = TaskbarLayout.Choose(3, 1.5, [new TaskbarGap(1836, 1909, true)]);

        Assert.Equal(PillDensity.Minimal, placement!.Value.Density);
        Assert.Equal(1, placement.Value.PillCount);
    }

    [Fact]
    public void ReportsNothingWhenNotEvenOnePillFits() =>
        Assert.Null(TaskbarLayout.Choose(2, 1.5, [new TaskbarGap(100, 130, true)]));
}
