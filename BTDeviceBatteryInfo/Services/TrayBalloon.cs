using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BTDeviceBatteryInfo.Models;
using Forms = System.Windows.Forms;

namespace BTDeviceBatteryInfo.Services;

/// <summary>
/// Shows the tray balloon with the device's icon instead of the warning triangle. WinForms only offers the
/// stock icons, so this sends the same balloon through Shell_NotifyIcon with NIIF_USER and its own icon.
/// </summary>
public static class TrayBalloon
{
    private const uint NimModify = 0x1;
    private const uint NifInfo = 0x10;
    private const uint NiifUser = 0x4;
    private const uint NiifLargeIcon = 0x20;
    private const int IconSize = 64;

    private static readonly Dictionary<BluetoothDeviceCategory, IntPtr> Icons = [];

    /// <summary>Returns false when the balloon could not be sent, so the caller can use the stock warning balloon.</summary>
    public static bool TryShow(Forms.NotifyIcon tray, string title, string text, BluetoothDeviceCategory category, out string? error)
    {
        try
        {
            var window = typeof(Forms.NotifyIcon).GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(tray) as Forms.NativeWindow;
            var id = typeof(Forms.NotifyIcon).GetField("_id", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(tray);
            if (window is null || window.Handle == IntPtr.Zero || id is null)
            {
                error = "the tray icon window was not found";
                return false;
            }

            var data = new NotifyIconData
            {
                cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
                hWnd = window.Handle,
                uID = Convert.ToUInt32(id),
                uFlags = NifInfo,
                szInfo = Truncate(text, 255),
                szInfoTitle = Truncate(title, 63),
                dwInfoFlags = NiifUser | NiifLargeIcon,
                hBalloonIcon = IconFor(category)
            };
            if (!Shell_NotifyIcon(NimModify, ref data))
            {
                error = "Shell_NotifyIcon refused the balloon";
                return false;
            }
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    /// <summary>Renders the category's Lucide icon in the low-battery red, cached per category for the app's lifetime.</summary>
    private static IntPtr IconFor(BluetoothDeviceCategory category)
    {
        if (Icons.TryGetValue(category, out var cached)) return cached;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var pen = new System.Windows.Media.Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44)), 2)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            // Lucide icons use a 24 px grid with 1 px of padding; scale it to fill the icon.
            dc.PushTransform(new ScaleTransform(IconSize / 24.0, IconSize / 24.0));
            dc.DrawGeometry(null, pen, LucideIcons.ForCategory(category));
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(IconSize, IconSize, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var pixels = new byte[IconSize * IconSize * 4];
        bitmap.CopyPixels(pixels, IconSize * 4, 0);

        using var gdi = new System.Drawing.Bitmap(IconSize, IconSize, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        var locked = gdi.LockBits(new System.Drawing.Rectangle(0, 0, IconSize, IconSize),
            System.Drawing.Imaging.ImageLockMode.WriteOnly, gdi.PixelFormat);
        Marshal.Copy(pixels, 0, locked.Scan0, pixels.Length);
        gdi.UnlockBits(locked);

        var handle = gdi.GetHicon();
        Icons[category] = handle;
        return handle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
}
