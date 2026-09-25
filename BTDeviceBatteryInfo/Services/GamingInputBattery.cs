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

        var percent = (int)Math.Round(Math.Clamp(remaining * 100.0 / full, 0, 100));
        var charging = report.Status == Windows.System.Power.BatteryStatus.Charging ? ", charging" : "";
        return new BatteryQueryValue(percent, $"Windows.Gaming.Input{charging}");
    }
}
