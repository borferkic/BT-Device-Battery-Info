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

    public static Geometry Instagram { get; } = Geometry.Parse(
        "M7,2 H17 A5,5 0 0 1 22,7 V17 A5,5 0 0 1 17,22 H7 A5,5 0 0 1 2,17 V7 A5,5 0 0 1 7,2 Z " +
        "M16,11.37 A4,4 0 1 1 12.63,8 A4,4 0 0 1 16,11.37 Z M17.5,6.5 H17.51");

    public static Geometry Twitch { get; } = Geometry.Parse("M21,2 H3 V18 H8 V22 L12,18 H17 L21,14 V2 Z M11,11 V7 M16,11 V7");

    public static Geometry LinkedIn { get; } = Geometry.Parse(
        "M16,8 A6,6 0 0 1 22,14 V21 H18 V14 A2,2 0 0 0 16,12 A2,2 0 0 0 14,14 V21 H10 V14 A6,6 0 0 1 16,8 Z " +
        "M2,9 H6 V21 H2 Z M4,2 A2,2 0 1 1 4,6 A2,2 0 1 1 4,2 Z");

    // Filled brand marks from Simple Icons (CC0); render with Fill, not Stroke.
    public static Geometry PayPal { get; } = Geometry.Parse(
        "M7.016 19.198h-4.2a.562.562 0 0 1-.555-.65L5.093.584A.692.692 0 0 1 5.776 0h7.222c3.417 0 5.904 2.488 5.846 5.5-.006.25-.027.5-.066.747A6.794 6.794 0 0 1 12.071 12H8.743a.69.69 0 0 0-.682.583l-.325 2.056-.013.083-.692 4.39-.015.087z" +
        "M19.79 6.142c-.01.087-.01.175-.023.261a7.76 7.76 0 0 1-7.695 6.598H9.007l-.283 1.795-.013.083-.692 4.39-.134.843-.014.088H6.86l-.497 3.15a.562.562 0 0 0 .555.65h3.612c.34 0 .63-.249.683-.585l.952-6.031a.692.692 0 0 1 .683-.584h2.126a6.793 6.793 0 0 0 6.707-5.752c.306-1.95-.466-3.744-1.89-4.906z");

    public static Geometry Patreon { get; } = Geometry.Parse(
        "M22.957 7.21c-.004-3.064-2.391-5.576-5.191-6.482-3.478-1.125-8.064-.962-11.384.604C2.357 3.231 1.093 7.391 1.046 11.54c-.039 3.411.302 12.396 5.369 12.46 3.765.047 4.326-4.804 6.068-7.141 1.24-1.662 2.836-2.132 4.801-2.618 3.376-.836 5.678-3.501 5.673-7.031Z");

    /// <summary>Windows device glyph (Segoe Fluent Icons / Segoe MDL2 Assets) used by the System theme.</summary>
    public static string SystemGlyphForCategory(BluetoothDeviceCategory category) => category switch
    {
        BluetoothDeviceCategory.Headphones => "",
        BluetoothDeviceCategory.Keyboard => "",
        BluetoothDeviceCategory.Mouse => "",
        BluetoothDeviceCategory.GameController => "",
        _ => ""
    };

    public static Geometry ForCategory(BluetoothDeviceCategory category) => category switch
    {
        BluetoothDeviceCategory.Headphones => Headphones,
        BluetoothDeviceCategory.Keyboard => Keyboard,
        BluetoothDeviceCategory.Mouse => Mouse,
        BluetoothDeviceCategory.GameController => Gamepad,
        _ => Info
    };
}
