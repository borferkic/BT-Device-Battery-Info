namespace BTDeviceBatteryInfo.Services;

internal static class AppIdentity
{
    public const string DisplayName = "BT Device Battery Info";
    public static string DataDirectory => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BTDeviceBatteryInfo");
}
