using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.ViewModels;

namespace BTDeviceBatteryInfo.Tests;

public class DeviceItemTests
{
    private static BluetoothDeviceItem Item(int? battery, bool connected = true) =>
        new("Device", connected, battery, BluetoothDeviceCategory.Headphones, "id");

    [Theory]
    [InlineData(0, true)]
    [InlineData(15, true)]
    [InlineData(16, false)]
    [InlineData(100, false)]
    public void BatteryIsLowAtFifteenPercentOrLess(int battery, bool expected) =>
        Assert.Equal(expected, Item(battery).IsBatteryLow);

    [Fact]
    public void UnknownBatteryIsNeitherLowNorAvailable()
    {
        var item = Item(null);
        Assert.False(item.IsBatteryLow);
        Assert.False(item.HasBattery);
        Assert.Equal(0, item.BatteryFillWidth);
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(15, 3)]
    [InlineData(16, 7)]
    [InlineData(50, 7)]
    [InlineData(75, 11)]
    [InlineData(76, 15)]
    [InlineData(100, 15)]
    public void BatteryFillWidthFollowsLevelSteps(int battery, double expected) =>
        Assert.Equal(expected, Item(battery).BatteryFillWidth);

    [Fact]
    public void TextsUseEnglishFallbackWithoutApplication() => Sta.Run(() =>
    {
        Assert.Equal("Battery: 80%", Item(80).BatteryText);
        Assert.Equal("Battery unavailable", Item(null).BatteryText);
        Assert.Equal("Connected", Item(80).ConnectionText);
        Assert.Equal("Disconnected", Item(80, connected: false).ConnectionText);
    });
}
