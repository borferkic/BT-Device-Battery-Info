using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.Tests;

public class LogitechReceiverTests
{
    [Theory]
    [InlineData(0, BluetoothDeviceCategory.Keyboard)]
    [InlineData(3, BluetoothDeviceCategory.Mouse)]
    [InlineData(4, BluetoothDeviceCategory.Mouse)]  // trackpad
    [InlineData(5, BluetoothDeviceCategory.Mouse)]  // trackball
    [InlineData(1, BluetoothDeviceCategory.Unknown)] // remote control
    public void HidppDeviceTypeMapsToCategory(byte deviceType, BluetoothDeviceCategory expected) =>
        Assert.Equal(expected, LogitechReceiverBattery.ToCategory(deviceType));

    [Fact]
    public void PhysicalIdSeparatesSlotsAndReceivers()
    {
        Assert.NotEqual(LogitechReceiverBattery.GetPhysicalDeviceId(0xC548, 1, "MX Keys S"), LogitechReceiverBattery.GetPhysicalDeviceId(0xC548, 2, "MX Keys S"));
        Assert.NotEqual(LogitechReceiverBattery.GetPhysicalDeviceId(0xC548, 1, "Mouse"), LogitechReceiverBattery.GetPhysicalDeviceId(0xC547, 1, "Mouse"));
    }

    [Theory]
    [InlineData(4200, 100)]
    [InlineData(3811, 50)]
    [InlineData(3835, 55)]   // halfway between the 50 % and 60 % points
    [InlineData(3400, 0)]
    public void VoltageMapsToPercent(int millivolts, int expected) =>
        Assert.Equal(expected, LogitechReceiverBattery.PercentFromVoltage(millivolts));

    [Fact]
    public void ImplausibleVoltageIsUnavailable() => Assert.Null(LogitechReceiverBattery.PercentFromVoltage(0));
}
