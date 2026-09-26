namespace BTDeviceBatteryInfo.Models;

public sealed record BluetoothDeviceInfo(
    string Id,
    string Name,
    bool IsPaired,
    bool IsConnected,
    int? BatteryPercent,
    string PhysicalDeviceId,
    BluetoothDeviceCategory Category = BluetoothDeviceCategory.Unknown,
    BatteryLevel? BatteryLevel = null);
