namespace BTDeviceBatteryInfo.Models;
public sealed record BluetoothDeviceInfo(string Id, string Name, bool IsPaired, bool IsConnected, int? BatteryPercent);
