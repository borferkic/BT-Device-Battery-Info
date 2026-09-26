using BTDeviceBatteryInfo.Models;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Gaming.Input;

namespace BTDeviceBatteryInfo.Services;

/// <summary>
/// Read-only battery fallback for wireless game controllers through Windows.Gaming.Input
/// (for example controllers in X-input mode over Bluetooth). The controller is matched to the
/// Bluetooth container through its HID game-controller interface; ambiguous matches are skipped.
/// </summary>
internal static class GamingInputBattery
{
    public const string SourceName = "Windows.Gaming.Input";
    private const string ContainerIdProperty = "System.Devices.ContainerId";
    private const string VendorIdProperty = "System.DeviceInterface.Hid.VendorId";
    private const string ProductIdProperty = "System.DeviceInterface.Hid.ProductId";
    private static readonly string[] InterfaceProperties = [ContainerIdProperty, VendorIdProperty, ProductIdProperty];

    // Gamepad and joystick top-level collections on the Generic Desktop usage page.
    private static readonly string[] Selectors = [HidDevice.GetDeviceSelector(0x01, 0x05), HidDevice.GetDeviceSelector(0x01, 0x04)];

    static GamingInputBattery()
    {
        // Subscribing makes Windows.Gaming.Input start tracking controllers for this process.
        RawGameController.RawGameControllerAdded += (_, _) => { };
    }

    public static void Initialize() => _ = RawGameController.RawGameControllers.Count;

    public static async Task<BatteryQueryValue> ReadAsync(string containerId, CancellationToken token)
    {
        if (!Guid.TryParse(containerId, out var container)) return BatteryQueryValue.Unavailable;

        var hardwareIds = new HashSet<(ushort Vendor, ushort Product)>();
        foreach (var selector in Selectors)
        {
            token.ThrowIfCancellationRequested();
            foreach (var hidInterface in await DeviceInformation.FindAllAsync(selector, InterfaceProperties))
            {
                if (hidInterface.Properties.TryGetValue(ContainerIdProperty, out var value) && value is Guid id && id == container
                    && hidInterface.Properties.TryGetValue(VendorIdProperty, out var vendor) && vendor is ushort vid
                    && hidInterface.Properties.TryGetValue(ProductIdProperty, out var product) && product is ushort pid)
                    hardwareIds.Add((vid, pid));
            }
        }
        if (hardwareIds.Count != 1) return BatteryQueryValue.Unavailable;

        var (vendorId, productId) = hardwareIds.First();
        var controllers = RawGameController.RawGameControllers
            .Where(controller => controller.IsWireless && controller.HardwareVendorId == vendorId && controller.HardwareProductId == productId)
            .ToArray();
        if (controllers.Length != 1) return BatteryQueryValue.Unavailable;

        var report = controllers[0].TryGetBatteryReport();
        if (report?.RemainingCapacityInMilliwattHours is not int remaining
            || report.FullChargeCapacityInMilliwattHours is not int full || full <= 0)
            return BatteryQueryValue.Unavailable;
        if (IsPlaceholderReport(remaining, full) && IsEightBitDo(await GetContainerNameAsync(containerId)))
            return BatteryQueryValue.Unavailable;

        // X-input controllers only report coarse ranges; keep the level, not a made-up percentage.
        var level = BatteryLevels.FromFraction(Math.Clamp((double)remaining / full, 0, 1));
        var charging = report.Status == Windows.System.Power.BatteryStatus.Charging ? ", charging" : "";
        return new BatteryQueryValue(BatteryLevels.RepresentativePercent(level), $"{SourceName} (level {level}, {remaining}/{full} mWh{charging})");
    }

    /// <summary>
    /// 8BitDo controllers in X-input mode report a fixed 100/1000 mWh whether they are empty or fully charged,
    /// so that value says nothing about the battery and is treated as unavailable. They identify as an
    /// Xbox One S controller, so the rule is limited to 8BitDo devices by name: a real Xbox controller can
    /// report the same value as a genuine low level.
    /// </summary>
    internal static bool IsPlaceholderReport(int remaining, int full) => remaining == 100 && full == 1000;

    internal static bool IsEightBitDo(string? deviceName) =>
        deviceName?.Contains("8BitDo", StringComparison.OrdinalIgnoreCase) == true;

    private static async Task<string?> GetContainerNameAsync(string containerId)
    {
        try
        {
            var container = await DeviceInformation.CreateFromIdAsync($"{{{containerId.Trim('{', '}')}}}", [], DeviceInformationKind.DeviceContainer);
            return container.Name;
        }
        catch
        {
            return null;
        }
    }
}
