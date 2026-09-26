using System.IO;
using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.Tests;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "btdbi-settings-" + Guid.NewGuid().ToString("N"));
    private string SettingsFile => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task SavedSettingsRoundTrip()
    {
        var original = new AppSettings
        {
            ThemeName = "Elegant Black",
            Language = "es",
            TaskbarWidgetEnabled = true,
            TaskbarHeadphonesDeviceId = "headphones-id",
            LowBatteryNotificationsEnabled = false,
            LowBatteryThreshold = 25,
            Left = -9,
            Top = 281
        };

        await SettingsService.SaveAsync(original, SettingsFile);
        var loaded = await SettingsService.LoadAsync(SettingsFile);

        Assert.Equal(original.ThemeName, loaded.ThemeName);
        Assert.Equal(original.Language, loaded.Language);
        Assert.Equal(original.TaskbarWidgetEnabled, loaded.TaskbarWidgetEnabled);
        Assert.Equal(original.TaskbarHeadphonesDeviceId, loaded.TaskbarHeadphonesDeviceId);
        Assert.Equal(original.LowBatteryNotificationsEnabled, loaded.LowBatteryNotificationsEnabled);
        Assert.Equal(original.LowBatteryThreshold, loaded.LowBatteryThreshold);
        Assert.Equal(original.Left, loaded.Left);
        Assert.Equal(original.Top, loaded.Top);
        Assert.False(File.Exists(SettingsFile + ".tmp"));
    }

    [Fact]
    public async Task SettingsFromOlderVersionsKeepNewDefaults()
    {
        // A 0.20-era file: no Language and no low-battery fields.
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsFile, """{ "ThemeName": "System", "TaskbarWidgetEnabled": true, "Left": 10, "Top": 20 }""");

        var loaded = await SettingsService.LoadAsync(SettingsFile);

        Assert.Equal("System", loaded.ThemeName);
        Assert.True(loaded.TaskbarWidgetEnabled);
        Assert.Equal("en", loaded.Language);
        Assert.True(loaded.LowBatteryNotificationsEnabled);
        Assert.Equal(LowBatteryNotifier.DefaultThreshold, loaded.LowBatteryThreshold);
        Assert.Equal("Right", loaded.TaskbarWidgetPosition);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ not json")]
    [InlineData("")]
    public async Task MissingOrCorruptFileYieldsDefaults(string? content)
    {
        if (content is not null)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SettingsFile, content);
        }

        var loaded = await SettingsService.LoadAsync(SettingsFile);

        Assert.Equal("System", loaded.ThemeName);
        Assert.Equal("en", loaded.Language);
    }
}

public class LowBatteryNotifierTests
{
    private static LowBatteryReading Reading(int? battery, bool connected = true, string id = "bose") =>
        new(id, "Bose QC Ultra 2 HP", connected, battery);

    [Fact]
    public void AlertsOnceWhenDroppingToTheThreshold()
    {
        var notifier = new LowBatteryNotifier();

        Assert.Empty(notifier.Evaluate([Reading(40)], enabled: true, threshold: 15));
        Assert.Single(notifier.Evaluate([Reading(15)], enabled: true, threshold: 15));
        Assert.Empty(notifier.Evaluate([Reading(12)], enabled: true, threshold: 15));
        Assert.Empty(notifier.Evaluate([Reading(8)], enabled: true, threshold: 15));
    }

    [Fact]
    public void AlertsAgainOnlyAfterRechargingAboveTheThreshold()
    {
        var notifier = new LowBatteryNotifier();
        notifier.Evaluate([Reading(10)], enabled: true, threshold: 15);

        Assert.Empty(notifier.Evaluate([Reading(15)], enabled: true, threshold: 15));
        Assert.Empty(notifier.Evaluate([Reading(60)], enabled: true, threshold: 15));
        Assert.Single(notifier.Evaluate([Reading(14)], enabled: true, threshold: 15));
    }

    [Fact]
    public void IgnoresDisconnectedDevicesAndUnknownBattery()
    {
        var notifier = new LowBatteryNotifier();

        Assert.Empty(notifier.Evaluate([Reading(5, connected: false), Reading(null, id: "mouse")], enabled: true, threshold: 15));
    }

    [Fact]
    public void DisabledNotificationsNeverAlertAndDoNotReplayWhenEnabled()
    {
        var notifier = new LowBatteryNotifier();

        Assert.Empty(notifier.Evaluate([Reading(5)], enabled: false, threshold: 15));
        Assert.Empty(notifier.Evaluate([Reading(5)], enabled: true, threshold: 15));
    }

    [Fact]
    public void EachDeviceIsTrackedSeparately()
    {
        var notifier = new LowBatteryNotifier();

        var alerts = notifier.Evaluate([Reading(10, id: "bose"), Reading(12, id: "keys")], enabled: true, threshold: 15);

        Assert.Equal(2, alerts.Count);
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(17, LowBatteryNotifier.DefaultThreshold)]
    [InlineData(0, LowBatteryNotifier.DefaultThreshold)]
    public void ThresholdIsLimitedToTheOffers(int threshold, int expected) =>
        Assert.Equal(expected, LowBatteryNotifier.NormalizeThreshold(threshold));
}
