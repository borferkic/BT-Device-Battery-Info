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
    public bool LaunchMinimized { get; set; }
    public double Opacity { get; set; } = .95;
    public double Left { get; set; } = 80;
    public double Top { get; set; } = 80;
}
