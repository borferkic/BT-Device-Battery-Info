namespace BTDeviceBatteryInfo.Models;

public sealed class AppSettings
{
    public string? DeviceId { get; set; }
    public bool AutoReconnect { get; set; } = true;
    public int RetryDelaySeconds { get; set; } = 5;
    public int MaximumAttempts { get; set; } = 5;
    public bool AlwaysOnTop { get; set; } = true;
    /// <summary>Starts with only the tray icon and taskbar widget; "Start with Windows" itself lives in the registry.</summary>
    public bool StartMinimized { get; set; }
    public bool TaskbarWidgetEnabled { get; set; }
    public string? TaskbarHeadphonesDeviceId { get; set; }
    public string? TaskbarKeyboardDeviceId { get; set; }
    public string? TaskbarMouseDeviceId { get; set; }
    public string? TaskbarGameControllerDeviceId { get; set; }
    public string ThemeName { get; set; } = "System";
    public string Language { get; set; } = "en";
    public bool LowBatteryNotificationsEnabled { get; set; } = true;
    public int LowBatteryThreshold { get; set; } = 15;
    /// <summary>"Left" (after the Windows widgets button) or "Right" (next to the notification area).</summary>
    public string TaskbarWidgetPosition { get; set; } = "Right";
    public double Opacity { get; set; } = .95;
}
