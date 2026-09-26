using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;
using BTDeviceBatteryInfo.ViewModels;

namespace BTDeviceBatteryInfo.Tests;

public class BatteryLevelTests
{
    [Theory]
    [InlineData(0.0, BatteryLevel.Empty)]
    [InlineData(0.05, BatteryLevel.Empty)]
    [InlineData(0.10, BatteryLevel.Low)]      // 100/1000 mWh reported by the 8BitDo Arcade Stick
    [InlineData(0.35, BatteryLevel.Low)]
    [InlineData(0.50, BatteryLevel.Medium)]
    [InlineData(0.70, BatteryLevel.Medium)]
    [InlineData(1.00, BatteryLevel.Full)]
    public void FractionMapsToLevel(double fraction, BatteryLevel expected) =>
        Assert.Equal(expected, BatteryLevels.FromFraction(fraction));

    [Theory]
    [InlineData(BatteryLevel.Empty)]
    [InlineData(BatteryLevel.Low)]
    [InlineData(BatteryLevel.Medium)]
    [InlineData(BatteryLevel.Full)]
    public void RepresentativeValueMapsBackToTheSameLevel(BatteryLevel level) =>
        Assert.Equal(level, BatteryLevels.FromFraction(BatteryLevels.RepresentativePercent(level) / 100.0));

    [Theory]
    [InlineData(BatteryLevel.Empty, true, 1)]
    [InlineData(BatteryLevel.Low, true, 4)]
    [InlineData(BatteryLevel.Medium, false, 8)]
    [InlineData(BatteryLevel.Full, false, 15)]
    public void DeviceWithLevelUsesTheLevelForLowStateAndFill(BatteryLevel level, bool low, double fill)
    {
        var item = new BluetoothDeviceItem("Arcade Stick", true, BatteryLevels.RepresentativePercent(level),
            BluetoothDeviceCategory.GameController, "stick", level);

        Assert.Equal(low, item.IsBatteryLow);
        Assert.Equal(fill, item.BatteryFillWidth);
    }

    [Fact]
    public void DeviceWithLevelShowsTheLevelNameInsteadOfAPercentage() => Sta.Run(() =>
    {
        var item = new BluetoothDeviceItem("Arcade Stick", true, 25, BluetoothDeviceCategory.GameController, "stick", BatteryLevel.Low);

        Assert.Equal("Low", item.BatteryShortText);
        Assert.Contains("approximate", item.BatteryText);
        Assert.DoesNotContain("%", item.BatteryShortText);
    });

    [Fact]
    public void LevelSourcesAlertAtLowRegardlessOfThreshold()
    {
        var notifier = new LowBatteryNotifier();
        LowBatteryReading Reading(bool levelLow) => new("stick", "Arcade Stick", true, levelLow ? 25 : 100, levelLow);

        Assert.Single(notifier.Evaluate([Reading(levelLow: true)], enabled: true, threshold: 10));
        Assert.Empty(notifier.Evaluate([Reading(levelLow: true)], enabled: true, threshold: 10));
        Assert.Empty(notifier.Evaluate([Reading(levelLow: false)], enabled: true, threshold: 10));
        Assert.Single(notifier.Evaluate([Reading(levelLow: true)], enabled: true, threshold: 10));
    }

    [Theory]
    [InlineData("8BitDo Arcade Stick", true)]
    [InlineData("8bitdo Ultimate 2C", true)]
    [InlineData("Xbox Wireless Controller", false)]
    [InlineData(null, false)]
    public void PlaceholderRuleOnlyAppliesTo8BitDo(string? name, bool expected) =>
        Assert.Equal(expected, BTDeviceBatteryInfo.Services.GamingInputBattery.IsEightBitDo(name));

    [Theory]
    [InlineData(100, 1000, true)]
    [InlineData(500, 1000, false)]
    [InlineData(100, 2000, false)]
    public void FixedGamingInputReportIsDetected(int remaining, int full, bool placeholder) =>
        Assert.Equal(placeholder, BTDeviceBatteryInfo.Services.GamingInputBattery.IsPlaceholderReport(remaining, full));
}
