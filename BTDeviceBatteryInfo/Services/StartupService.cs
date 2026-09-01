using Microsoft.Win32;
namespace BTDeviceBatteryInfo.Services;

public static class StartupService
{
    private const string Name = AppIdentity.DisplayName;
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue(Name) is not null;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            key.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(Name, false);
        }
    }
}
