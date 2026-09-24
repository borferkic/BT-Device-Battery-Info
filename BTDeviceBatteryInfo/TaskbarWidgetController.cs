using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;
using BTDeviceBatteryInfo.ViewModels;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace BTDeviceBatteryInfo;

/// <summary>Displays the device state already published by the main window in the taskbar.</summary>
internal sealed class TaskbarWidgetController : IDisposable
{
    private readonly MainViewModel _viewModel;
    private readonly AppSettings _settings;
    private readonly Func<BluetoothDeviceCategory, BluetoothDeviceItem, Task> _selectDevice;
    private readonly FileLogger _logger;
    private readonly DispatcherTimer _timer;
    private TaskbarDockWindow? _window;
    private string? _lastStatus;
    private bool _enabled;

    public TaskbarWidgetController(MainViewModel viewModel, AppSettings settings,
        Func<BluetoothDeviceCategory, BluetoothDeviceItem, Task> selectDevice, FileLogger logger)
    {
        _viewModel = viewModel;
        _settings = settings;
        _selectDevice = selectDevice;
        _logger = logger;
        ((INotifyCollectionChanged)_viewModel.ConnectedDevices).CollectionChanged += OnDevicesChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => Refresh();
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (enabled)
        {
            _lastStatus = null;
            _timer.Start();
            Refresh();
        }
        else
        {
            _timer.Stop();
            _window?.Hide();
            SetStatus("Taskbar widget disabled by the user.");
        }
    }

    private void OnDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e) => Refresh();
    public void RefreshNow() => Refresh();

    private async void Refresh()
    {
        if (!_enabled) return;
        try { RefreshCore(); }
        catch (Exception ex)
        {
            _window?.Hide();
            try { await _logger.LogAsync("Taskbar widget docking error: " + ex); }
            catch { /* Diagnostics must not close the application. */ }
        }
    }

    private void RefreshCore()
    {
        var devices = _viewModel.ConnectedDevices
            .Where(device => device.Category != BluetoothDeviceCategory.Unknown)
            .ToArray();
        if (devices.Length == 0)
        {
            _window?.Hide();
            SetStatus($"No compatible connected devices (connected: {_viewModel.ConnectedDevices.Count}; categorized: 0).");
            return;
        }

        if (_window is null || !_window.HasValidHandle)
        {
            CloseWindow();
            _window = new TaskbarDockWindow();
            _window.Show();
        }

        _window.UpdateDevices(devices, _settings, _selectDevice);
        if (_window.TryDock(out var reason))
        {
            _window.Show();
            SetStatus($"Taskbar widget visible (device categories: {devices.Select(device => device.Category).Distinct().Count()}).");
        }
        else
        {
            _window.Hide();
            SetStatus("Taskbar widget enabled but hidden: " + reason);
        }
    }

    private void SetStatus(string status)
    {
        if (_lastStatus == status) return;
        _lastStatus = status;
        _ = LogStatusAsync(status);
    }

    private async Task LogStatusAsync(string status)
    {
        try { await _logger.LogAsync("Taskbar widget status: " + status); }
        catch { /* Logging failures must not interrupt taskbar attachment. */ }
    }

    private void CloseWindow()
    {
        if (_window is null) return;
        try { _window.Close(); }
        catch { /* Explorer may have destroyed the child window handle. */ }
        _window = null;
    }

    public void Dispose()
    {
        _enabled = false;
        _timer.Stop();
        ((INotifyCollectionChanged)_viewModel.ConnectedDevices).CollectionChanged -= OnDevicesChanged;
        CloseWindow();
    }
}

internal sealed class TaskbarDockWindow : Window
{
    private const double TaskbarWidgetHeight = 40;
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000;
    private const long WsPopup = unchecked((int)0x80000000);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpFrameChanged = 0x0020;
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private IntPtr _attachedTaskbar;
    private IntPtr Handle => new WindowInteropHelper(this).Handle;
    public bool HasValidHandle => Handle != IntPtr.Zero && IsWindow(Handle);

    public TaskbarDockWindow()
    {
        Width = 1;
        Height = TaskbarWidgetHeight;
        ShowInTaskbar = false;
        ShowActivated = false;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.Manual;
        Content = _panel;
    }

    public void UpdateDevices(IReadOnlyList<BluetoothDeviceItem> devices, AppSettings settings,
        Func<BluetoothDeviceCategory, BluetoothDeviceItem, Task> selectDevice)
    {
        if (_panel.Children.OfType<Button>().Any(button => button.ContextMenu?.IsOpen == true)) return;
        _panel.Children.Clear();
        var selectedDevices = devices.GroupBy(device => device.Category)
            .Select(group => group.FirstOrDefault(device => string.Equals(
                device.PhysicalDeviceId, GetSelectedDeviceId(settings, group.Key), StringComparison.OrdinalIgnoreCase)) ?? group.First())
            .ToArray();
        foreach (var device in selectedDevices)
        {
            var content = new Grid { Width = 50, VerticalAlignment = VerticalAlignment.Center };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(23) });
            var icon = new Path
            {
                Data = LucideIcons.ForCategory(device.Category),
                Width = 21,
                Height = 21,
                Stretch = Stretch.Uniform,
                Fill = Brushes.Transparent,
                StrokeThickness = 1.7,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stroke = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);
            content.Children.Add(icon);
            var batteryRing = new BatteryRing(device.BatteryPercent);
            Grid.SetColumn(batteryRing, 2);
            content.Children.Add(batteryRing);
            var button = new Button
            {
                Content = content,
                Width = 64,
                Height = 36,
                Padding = new Thickness(8, 0, 4, 0),
                Margin = new Thickness(2, 2, 0, 2),
                Background = new SolidColorBrush(Color.FromRgb(45, 48, 55)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(75, 78, 84)),
                BorderThickness = new Thickness(1),
                ToolTip = $"{device.Name} — {device.BatteryText}. Click to choose another {CategoryLabel(device.Category)}.",
                Cursor = Cursors.Hand
            };
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(16));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Button.Background))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(Button.BorderBrush))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(Button.BorderThickness))
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.MarginProperty,
                new TemplateBindingExtension(System.Windows.Controls.Control.PaddingProperty));
            border.AppendChild(contentPresenter);
            button.Template = new ControlTemplate(typeof(Button)) { VisualTree = border };
            var deviceMenu = new ContextMenu { Placement = PlacementMode.Bottom, PlacementTarget = button };
            foreach (var option in devices.Where(option => option.Category == device.Category))
            {
                var menuItem = new MenuItem
                {
                    Header = $"{option.Name} — {option.BatteryText}",
                    IsCheckable = true,
                    IsChecked = string.Equals(option.PhysicalDeviceId, device.PhysicalDeviceId, StringComparison.OrdinalIgnoreCase),
                    ToolTip = option.Name
                };
                menuItem.Click += async (_, _) =>
                {
                    deviceMenu.IsOpen = false;
                    await selectDevice(device.Category, option);
                };
                deviceMenu.Items.Add(menuItem);
            }
            button.ContextMenu = deviceMenu;
            button.Click += (_, _) => deviceMenu.IsOpen = true;
            _panel.Children.Add(button);
        }
        Width = selectedDevices.Length * 66 + 2;
    }

    private static string? GetSelectedDeviceId(AppSettings settings, BluetoothDeviceCategory category) => category switch
    {
        BluetoothDeviceCategory.Headphones => settings.TaskbarHeadphonesDeviceId,
        BluetoothDeviceCategory.Keyboard => settings.TaskbarKeyboardDeviceId,
        BluetoothDeviceCategory.Mouse => settings.TaskbarMouseDeviceId,
        BluetoothDeviceCategory.GameController => settings.TaskbarGameControllerDeviceId,
        _ => null
    };

    private static string CategoryLabel(BluetoothDeviceCategory category) => category switch
    {
        BluetoothDeviceCategory.Headphones => "headphones",
        BluetoothDeviceCategory.Keyboard => "keyboard",
        BluetoothDeviceCategory.Mouse => "mouse",
        BluetoothDeviceCategory.GameController => "controller",
        _ => "device"
    };

    public bool TryDock(out string reason)
    {
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var taskbarRect))
            return Fail("Windows primary taskbar was not found", out reason);
        if (taskbarRect.Right - taskbarRect.Left <= taskbarRect.Bottom - taskbarRect.Top)
            return Fail("the detected taskbar is not horizontal", out reason);

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
            return Fail("Windows has not exposed the WPF window for positioning yet", out reason);
        var transform = source.CompositionTarget.TransformToDevice;
        var width = (int)Math.Ceiling(Width * transform.M11);
        var height = (int)Math.Ceiling(Height * transform.M22);
        var left = taskbarRect.Left + 8;
        var buttons = new List<(System.Windows.Rect Bounds, string Id)>();
        try
        {
            var root = AutomationElement.FromHandle(taskbar);
            var elements = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement element in elements)
            {
                if (element.Current.ProcessId == Environment.ProcessId) continue;
                var rect = element.Current.BoundingRectangle;
                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0) continue;
                buttons.Add((rect, element.Current.AutomationId));
            }
        }
        catch (ElementNotAvailableException)
        {
            return Fail("UI Automation could not read the taskbar tree", out reason);
        }
        catch (InvalidOperationException)
        {
            return Fail("UI Automation could not read the taskbar tree", out reason);
        }
        if (buttons.Count == 0)
        {
            return Fail("UI Automation found no taskbar buttons", out reason);
        }

        foreach (var button in buttons)
            if (button.Id.Contains("WidgetsButton", StringComparison.OrdinalIgnoreCase))
                left = Math.Max(left, (int)Math.Ceiling(button.Bounds.Right) + 6);

        var top = taskbarRect.Top + (taskbarRect.Bottom - taskbarRect.Top - height) / 2;
        var proposed = new System.Windows.Rect(left, top, width, height);
        if (left + width > taskbarRect.Right - 8) return Fail("there is not enough free width on the taskbar", out reason);
        if (buttons.Any(button => button.Bounds.IntersectsWith(proposed)))
            return Fail("the left taskbar area is occupied by Windows or app buttons", out reason);

        if (_attachedTaskbar != taskbar || GetParent(Handle) != taskbar)
        {
            var currentStyle = GetWindowLongPtr(Handle, GwlStyle).ToInt64();
            SetWindowLongPtr(Handle, GwlStyle, new IntPtr((currentStyle & ~WsPopup) | WsChild));
            SetParent(Handle, taskbar);
            if (GetParent(Handle) != taskbar)
                return Fail("Explorer rejected the auxiliary window attachment", out reason);
            _attachedTaskbar = taskbar;
        }

        var location = new NativePoint { X = left, Y = top };
        if (!ScreenToClient(taskbar, ref location))
            return Fail("could not convert the widget position to taskbar coordinates", out reason);
        if (!SetWindowPos(Handle, IntPtr.Zero, location.X, location.Y, width, height,
            SwpNoActivate | SwpShowWindow | SwpFrameChanged))
            return Fail("Windows could not position the device pills", out reason);

        reason = string.Empty;
        return true;
    }

    private static bool Fail(string message, out string reason)
    {
        reason = message;
        return false;
    }


    private sealed class BatteryRing : Grid
    {
        public BatteryRing(int? percent)
        {
            Width = Height = 23;
            Children.Add(new Ellipse
            {
                Width = 21,
                Height = 21,
                Stroke = new SolidColorBrush(Color.FromRgb(110, 113, 119)),
                StrokeThickness = 2
            });
            if (percent is int value && value > 0)
            {
                var angle = 2 * Math.PI * Math.Min(value, 99.9) / 100;
                var end = new Point(11.5 + 9.5 * Math.Sin(angle), 11.5 - 9.5 * Math.Cos(angle));
                var path = new Path
                {
                    Stroke = value <= 15 ? Brushes.IndianRed : Brushes.MediumSeaGreen,
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Data = new PathGeometry(new[]
                    {
                        new PathFigure(new Point(11.5, 2), new PathSegment[]
                        {
                            new ArcSegment(end, new Size(9.5, 9.5), 0, angle > Math.PI,
                                SweepDirection.Clockwise, true)
                        }, false)
                    })
                };
                Children.Add(path);
            }
            Children.Add(new TextBlock
            {
                Text = percent is int battery ? battery.ToString() : "–",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr window);
    [DllImport("user32.dll")]
    private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
