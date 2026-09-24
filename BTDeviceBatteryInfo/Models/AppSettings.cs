namespace BTDeviceBatteryInfo.Models;

public sealed class AppSettings
{
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public bool AutoReconnect { get; set; } = true;
    public int RetryDelaySeconds { get; set; } = 5;
    public int MaximumAttempts { get; set; } = 5;
    public bool AlwaysOnTop { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool TaskbarWidgetEnabled { get; set; }
    public string? TaskbarHeadphonesDeviceId { get; set; }
    public string? TaskbarKeyboardDeviceId { get; set; }
    public string? TaskbarMouseDeviceId { get; set; }
    public string? TaskbarGameControllerDeviceId { get; set; }
    public string ThemeName { get; set; } = "System";
    public double Opacity { get; set; } = .95;
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
}
