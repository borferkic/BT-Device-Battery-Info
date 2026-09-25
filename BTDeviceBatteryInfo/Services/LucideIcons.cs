using System.Windows.Media;
using BTDeviceBatteryInfo.Models;

namespace BTDeviceBatteryInfo.Services;

/// <summary>Lightweight Lucide-style outline geometries shared by the WPF views.</summary>
public static class LucideIcons
{
    public static Geometry Headphones { get; } = Geometry.Parse(
        "M3,14 H6 A2,2 0 0 1 8,16 V19 A2,2 0 0 1 6,21 H5 A2,2 0 0 1 3,19 V11 A9,9 0 0 1 21,11 V19 A2,2 0 0 1 19,21 H18 A2,2 0 0 1 16,19 V16 A2,2 0 0 1 18,14 H21");

    public static Geometry Keyboard { get; } = Geometry.Parse(
        "M4,6 H20 A2,2 0 0 1 22,8 V16 A2,2 0 0 1 20,18 H4 A2,2 0 0 1 2,16 V8 A2,2 0 0 1 4,6 Z " +
        "M6,10 H6.01 M9,10 H9.01 M12,10 H12.01 M15,10 H15.01 M18,10 H18.01 " +
        "M6,13 H6.01 M9,13 H9.01 M12,13 H12.01 M15,13 H15.01 M18,13 H18.01 M9,16 H15");

    public static Geometry Mouse { get; } = Geometry.Parse(
        "M12,2 A7,7 0 0 0 5,9 V15 A7,7 0 0 0 19,15 V9 A7,7 0 0 0 12,2 Z M12,6 V10");

    public static Geometry Gamepad { get; } = Geometry.Parse(
        "M6,8 H18 A4,4 0 0 1 21.8,10.7 L23,14.8 A3.5,3.5 0 0 1 18,19 L14,17 H10 L6,19 A3.5,3.5 0 0 1 1,14.8 L2.2,10.7 A4,4 0 0 1 6,8 Z " +
        "M6,12 H10 M8,10 V14 M16,12 H16.01 M19,14 H19.01");

    public static Geometry Info { get; } = Geometry.Parse(
        "M12,22 A10,10 0 1 0 12,2 A10,10 0 0 0 12,22 Z M12,11 V16 M12,8 H12.01");

    public static Geometry Settings { get; } = Geometry.Parse(
        "M10,2 H14 L15,5 L17,6 L20,5 L22,9 L20,11 V13 L22,15 L20,19 L17,18 L15,19 L14,22 H10 L9,19 L7,18 L4,19 L2,15 L4,13 V11 L2,9 L4,5 L7,6 L9,5 Z " +
        "M12,9 A3,3 0 1 0 12,15 A3,3 0 1 0 12,9 Z");

    public static Geometry Minimize { get; } = Geometry.Parse("M5,12 H19");

    public static Geometry Close { get; } = Geometry.Parse("M18,6 L6,18 M6,6 L18,18");

    public static Geometry ChevronRight { get; } = Geometry.Parse("M9,18 L15,12 L9,6");

    public static Geometry ChevronDown { get; } = Geometry.Parse("M6,9 L12,15 L18,9");

    public static Geometry Check { get; } = Geometry.Parse("M20,6 L9,17 L4,12");

    public static Geometry LoaderCircle { get; } = Geometry.Parse("M21,12 A9,9 0 1 1 14.781,3.44");

    public static Geometry Bluetooth { get; } = Geometry.Parse("M7,7 L17,17 L12,22 V2 L17,7 L7,17");

    public static Geometry ForCategory(BluetoothDeviceCategory category) => category switch
    {
        BluetoothDeviceCategory.Headphones => Headphones,
        BluetoothDeviceCategory.Keyboard => Keyboard,
        BluetoothDeviceCategory.Mouse => Mouse,
        BluetoothDeviceCategory.GameController => Gamepad,
        _ => Info
    };
}
