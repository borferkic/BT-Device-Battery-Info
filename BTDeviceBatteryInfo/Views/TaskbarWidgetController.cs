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
    /// <summary>Last time each categorized device was seen connected; keeps its pill through brief BLE sleep drops.</summary>
    private readonly Dictionary<string, (BluetoothDeviceItem Device, DateTime LastSeen)> _recentDevices = new(StringComparer.OrdinalIgnoreCase);
    private TaskbarDockWindow? _window;
    private string? _lastStatus;
    private bool _enabled;

    /// <summary>True only while the widget window is actually shown on the taskbar (not merely enabled in the settings).</summary>
    public bool IsDisplayed { get; private set; }
    /// <summary>Why the widget is not displayed (empty while displayed or disabled).</summary>
    public string HiddenReason { get; private set; } = string.Empty;
    /// <summary>Raised on the UI thread whenever <see cref="IsDisplayed"/> or <see cref="HiddenReason"/> changes.</summary>
    public event EventHandler? DisplayStateChanged;

    /// <summary>How long a device that stopped reporting as connected keeps its (dimmed) pill.</summary>
    internal static readonly TimeSpan DisconnectGracePeriod = TimeSpan.FromSeconds(45);

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
        // Resolution, scaling or monitor changes move the taskbar; do not wait for the next 4 s tick.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(Refresh);

    private void SetDisplayState(bool displayed, string reason)
    {
        if (IsDisplayed == displayed && HiddenReason == reason) return;
        IsDisplayed = displayed;
        HiddenReason = reason;
        DisplayStateChanged?.Invoke(this, EventArgs.Empty);
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
            _recentDevices.Clear();
            _window?.Hide();
            SetDisplayState(false, string.Empty);
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
            SetDisplayState(false, "internal error (see log)");
            try { await _logger.LogAsync("Taskbar widget docking error: " + ex); }
            catch { /* Diagnostics must not close the application. */ }
        }
    }

    private void RefreshCore()
    {
        var connected = _viewModel.ConnectedDevices
            .Where(device => device.Category != BluetoothDeviceCategory.Unknown)
            .ToArray();
        var devices = MergeRecentDevices(_recentDevices, connected, DateTime.UtcNow, DisconnectGracePeriod, out var staleIds);
        if (devices.Count == 0)
        {
            _window?.Hide();
            SetDisplayState(false, "no connected device to show");
            SetStatus($"No compatible connected devices (connected: {_viewModel.ConnectedDevices.Count}; categorized: 0).");
            return;
        }

        if (_window is null || !_window.HasValidHandle)
        {
            CloseWindow();
            _window = new TaskbarDockWindow();
            _window.Show();
        }

        _window.UpdateDevices(devices, staleIds, _settings, _selectDevice);
        var result = _window.TryDock(_settings.TaskbarWidgetPosition != "Left", out var reason);
        switch (result)
        {
            case DockResult.Docked:
                _window.Show();
                SetDisplayState(true, string.Empty);
                SetStatus($"Taskbar widget visible (device categories: {devices.Select(device => device.Category).Distinct().Count()}; in grace period: {staleIds.Count}; size: {_window.CurrentDensity}).");
                break;
            case DockResult.KeptLastPosition:
                SetDisplayState(true, string.Empty);
                SetStatus("Taskbar widget kept at its last position: " + reason);
                break;
            default:
                _window.Hide();
                SetDisplayState(false, reason);
                SetStatus("Taskbar widget enabled but hidden: " + reason + " | layout: " + _window.LayoutDiagnostics);
                break;
        }
    }

    /// <summary>
    /// Adds the devices seen connected within <paramref name="gracePeriod"/> to the connected ones, so a keyboard or
    /// mouse that drops for a few seconds while sleeping does not make its pill (or the whole widget) vanish.
    /// </summary>
    internal static IReadOnlyList<BluetoothDeviceItem> MergeRecentDevices(
        Dictionary<string, (BluetoothDeviceItem Device, DateTime LastSeen)> recent, IReadOnlyList<BluetoothDeviceItem> connected,
        DateTime now, TimeSpan gracePeriod, out HashSet<string> staleIds)
    {
        foreach (var device in connected)
            recent[device.PhysicalDeviceId] = (device, now);
        foreach (var expired in recent.Where(entry => now - entry.Value.LastSeen > gracePeriod).Select(entry => entry.Key).ToArray())
            recent.Remove(expired);

        staleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = connected.ToList();
        foreach (var (id, entry) in recent)
        {
            if (connected.Any(device => string.Equals(device.PhysicalDeviceId, id, StringComparison.OrdinalIgnoreCase))) continue;
            staleIds.Add(id);
            merged.Add(entry.Device);
        }
        return merged;
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
        _recentDevices.Clear();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        ((INotifyCollectionChanged)_viewModel.ConnectedDevices).CollectionChanged -= OnDevicesChanged;
        CloseWindow();
    }
}

internal enum PillDensity
{
    /// <summary>Icon + battery ring (the original look).</summary>
    Full,
    /// <summary>Icon + battery ring with tighter padding.</summary>
    Compact,
    /// <summary>Battery ring only; the device is identified by its tooltip.</summary>
    Minimal
}

/// <summary>A free horizontal span of the taskbar, in physical pixels.</summary>
internal readonly record struct TaskbarGap(int Left, int Right, bool NextToTray)
{
    public int Width => Right - Left;
}

internal readonly record struct TaskbarPlacement(PillDensity Density, int PillCount, TaskbarGap Gap);

/// <summary>
/// Pure layout maths for the taskbar widget. The widget is a child of the taskbar and cannot overlap app buttons,
/// so it must fit into one of the free gaps; when the preferred size does not fit, it shrinks instead of vanishing.
/// </summary>
internal static class TaskbarLayout
{
    private const double ContainerPadding = 2;
    private const int Clearance = 8;
    private const int WidgetsClearance = 6;

    public static double Pitch(PillDensity density) => density switch
    {
        PillDensity.Full => 66,
        PillDensity.Compact => 56,
        _ => 30
    };

    public static double ButtonWidth(PillDensity density) => Pitch(density) - 2;

    /// <summary>Window width in device-independent pixels.</summary>
    public static double WidthFor(int pills, PillDensity density) => pills * Pitch(density) + ContainerPadding;

    /// <summary>
    /// Finds the free span just left of the notification area and the one after the Windows widgets button, from the
    /// bounds of the taskbar buttons. Buttons inside the notification area are not obstacles for the left span.
    /// </summary>
    public static (TaskbarGap? Left, TaskbarGap? Right) FindGaps(int taskbarLeft, int taskbarRight, int? trayLeft,
        IEnumerable<(double Left, double Right, string Id)> buttons)
    {
        var obstacles = buttons.Where(button => trayLeft is null || button.Left < trayLeft.Value - 2).ToList();

        TaskbarGap? right = null;
        if (trayLeft is int tray)
        {
            var edge = obstacles.Where(button => button.Right <= tray).Select(button => button.Right).DefaultIfEmpty(taskbarLeft).Max();
            right = new TaskbarGap((int)Math.Ceiling(edge) + WidgetsClearance, tray - Clearance, true);
        }

        var widgets = obstacles.Where(button => button.Id.Contains("WidgetsButton", StringComparison.OrdinalIgnoreCase)).ToList();
        var leftStart = widgets.Count > 0 ? (int)Math.Ceiling(widgets.Max(button => button.Right)) + WidgetsClearance : taskbarLeft + Clearance;
        var apps = obstacles.Where(button => !widgets.Contains(button) && button.Right > leftStart).ToList();
        var leftEnd = apps.Count > 0 ? (int)Math.Floor(apps.Min(button => button.Left)) - Clearance : (trayLeft ?? taskbarRight) - Clearance;
        return (new TaskbarGap(leftStart, leftEnd, false), right);
    }

    /// <summary>
    /// Picks the first gap (in order of preference) into which all pills fit, at the largest density that fits. When even the
    /// smallest density cannot show every pill, shows as many as fit in the widest gap; null when not even one fits.
    /// </summary>
    public static TaskbarPlacement? Choose(int pills, double scale, IReadOnlyList<TaskbarGap> gaps)
    {
        if (pills <= 0 || gaps.Count == 0) return null;
        // The preferred side wins as long as all pills fit there at any size; only then is the other side tried.
        foreach (var gap in gaps)
            foreach (var density in new[] { PillDensity.Full, PillDensity.Compact, PillDensity.Minimal })
                if (gap.Width >= (int)Math.Ceiling(WidthFor(pills, density) * scale))
                    return new TaskbarPlacement(density, pills, gap);
        var widest = gaps.MaxBy(gap => gap.Width);
        var fit = (int)Math.Floor((widest.Width / scale - ContainerPadding) / Pitch(PillDensity.Minimal));
        return fit >= 1 ? new TaskbarPlacement(PillDensity.Minimal, Math.Min(fit, pills), widest) : null;
    }
}

internal enum DockResult
{
    Docked,
    /// <summary>The taskbar could not be read for a moment; the widget stays where it was.</summary>
    KeptLastPosition,
    Hidden
}

internal sealed class TaskbarDockWindow : Window
{
    /// <summary>Consecutive refreshes (about 4 s apart) with an unreadable taskbar tolerated before hiding (~1 min).</summary>
    private const int MaxTransientFailures = 15;
    private const double TaskbarWidgetHeight = 40;
    private const int GwlStyle = -16;
    private const long WsChild = 0x40000000;
    private const long WsPopup = unchecked((int)0x80000000);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpFrameChanged = 0x0020;
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private IntPtr _attachedTaskbar;
    private List<(System.Windows.Rect Bounds, string Id)>? _lastButtons;
    private NativeRect _lastTaskbarRect;
    private int _transientFailures;
    private bool _docked;
    private IReadOnlyList<BluetoothDeviceItem>? _devices;
    private IReadOnlySet<string>? _staleIds;
    private AppSettings? _settings;
    private Func<BluetoothDeviceCategory, BluetoothDeviceItem, Task>? _selectDevice;
    private PillDensity _density = PillDensity.Full;
    private int _maxPills = int.MaxValue;
    /// <summary>Human-readable geometry of the last docking attempt (taskbar, tray, gaps, scale), logged when the widget is hidden.</summary>
    public string LayoutDiagnostics { get; private set; } = "n/a";
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

    private bool MenuOpen => _panel.Children.OfType<Button>().Any(button => button.ContextMenu?.IsOpen == true);

    /// <summary>Number of pills wanted: one per device category.</summary>
    public int DesiredPillCount => _devices?.Select(device => device.Category).Distinct().Count() ?? 0;

    public void UpdateDevices(IReadOnlyList<BluetoothDeviceItem> devices, IReadOnlySet<string> staleIds, AppSettings settings,
        Func<BluetoothDeviceCategory, BluetoothDeviceItem, Task> selectDevice)
    {
        if (MenuOpen) return;
        _devices = devices;
        _staleIds = staleIds;
        _settings = settings;
        _selectDevice = selectDevice;
        Rebuild();
    }

    /// <summary>Switches the pill size / number of pills to what fits the free taskbar gap.</summary>
    private void SetLayout(PillDensity density, int maxPills)
    {
        if (density == _density && maxPills == _maxPills) return;
        if (MenuOpen) return;
        _density = density;
        _maxPills = maxPills;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_devices is null || _staleIds is null || _settings is null || _selectDevice is null || MenuOpen) return;
        var devices = _devices;
        var staleIds = _staleIds;
        var settings = _settings;
        var selectDevice = _selectDevice;
        _panel.Children.Clear();
        // Per category: the chosen device if connected, else another connected one, else one in its grace period.
        var selectedDevices = devices.GroupBy(device => device.Category)
            .Select(group => group
                .OrderBy(device => staleIds.Contains(device.PhysicalDeviceId))
                .ThenBy(device => !string.Equals(device.PhysicalDeviceId, GetSelectedDeviceId(settings, group.Key), StringComparison.OrdinalIgnoreCase))
                .First())
            .Take(_maxPills)
            .ToArray();
        var density = _density;
        foreach (var device in selectedDevices)
        {
            var showIcon = density != PillDensity.Minimal;
            var content = new Grid
            {
                Width = density == PillDensity.Full ? 50 : showIcon ? 48 : 23,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (showIcon)
            {
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(density == PillDensity.Full ? 5 : 3) });
            }
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(23) });
            var ringColumn = showIcon ? 2 : 0;
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
                VerticalAlignment = VerticalAlignment.Center
            };
            var iconBrush = device.IsBatteryLow ? "ShadcnDestructiveBrush" : "ShadcnWidgetForegroundBrush";
            icon.SetResourceReference(Shape.StrokeProperty, iconBrush);
            icon.SetResourceReference(VisibilityProperty, "ShadcnLucideDeviceIconVisibility");
            Grid.SetColumn(icon, 0);
            if (showIcon) content.Children.Add(icon);
            var glyph = new TextBlock
            {
                Text = LucideIcons.SystemGlyphForCategory(device.Category),
                FontSize = 18,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            glyph.SetResourceReference(TextBlock.FontFamilyProperty, "SystemIconFontFamily");
            glyph.SetResourceReference(TextBlock.ForegroundProperty, iconBrush);
            glyph.SetResourceReference(VisibilityProperty, "ShadcnSystemDeviceIconVisibility");
            Grid.SetColumn(glyph, 0);
            if (showIcon) content.Children.Add(glyph);
            var batteryRing = new BatteryRing(device.BatteryPercent, device.BatteryLevelName?[..1]);
            Grid.SetColumn(batteryRing, ringColumn);
            content.Children.Add(batteryRing);
            var button = new Button
            {
                Content = content,
                Width = TaskbarLayout.ButtonWidth(density),
                Padding = density switch
                {
                    PillDensity.Full => new Thickness(8, 0, 4, 0),
                    PillDensity.Compact => new Thickness(4, 0, 2, 0),
                    _ => new Thickness(2, 0, 2, 0)
                },
                Margin = new Thickness(2, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = AppLanguage.Format("Widget.Tooltip", device.Name, device.BatteryText, CategoryLabel(device.Category)),
                Cursor = Cursors.Hand,
                // Dimmed while the device is in its disconnect grace period.
                Opacity = staleIds.Contains(device.PhysicalDeviceId) ? 0.5 : 1
            };
            button.SetResourceReference(HeightProperty, "ShadcnWidgetButtonHeight");
            button.SetResourceReference(System.Windows.Controls.Control.BorderThicknessProperty, "ShadcnBorderThickness");
            button.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ShadcnWidgetBrush");
            button.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "ShadcnWidgetForegroundBrush");
            button.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "ShadcnWidgetBorderBrush");
            button.MouseEnter += (_, _) => button.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ShadcnWidgetHoverBrush");
            button.MouseLeave += (_, _) => button.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ShadcnWidgetBrush");
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetResourceReference(Border.CornerRadiusProperty, "ShadcnWidgetCornerRadius");
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
        Width = TaskbarLayout.WidthFor(selectedDevices.Length, density);
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
        BluetoothDeviceCategory.Headphones => AppLanguage.Get("Widget.Headphones"),
        BluetoothDeviceCategory.Keyboard => AppLanguage.Get("Widget.Keyboard"),
        BluetoothDeviceCategory.Mouse => AppLanguage.Get("Widget.Mouse"),
        BluetoothDeviceCategory.GameController => AppLanguage.Get("Widget.Controller"),
        _ => AppLanguage.Get("Widget.Device")
    };

    /// <summary>
    /// Docks the widget. Explorer often answers UI Automation with an empty tree for a few seconds (Start menu,
    /// animations, load); hiding then made the widget blink or vanish, so meanwhile the last good button layout is
    /// reused, or the widget stays where it was, until the failure lasts about a minute.
    /// </summary>
    public DockResult TryDock(bool nextToNotificationArea, out string reason)
    {
        if (TryDockCore(nextToNotificationArea, out reason, out var transient))
        {
            _transientFailures = transient ? _transientFailures + 1 : 0;
            _docked = true;
            return DockResult.Docked;
        }
        if (transient && _docked && ++_transientFailures <= MaxTransientFailures)
            return DockResult.KeptLastPosition;
        _transientFailures = 0;
        _docked = false;
        return DockResult.Hidden;
    }

    /// <param name="transient">True when the taskbar could not be read reliably; on success, the cached layout was used.</param>
    private bool TryDockCore(bool nextToNotificationArea, out string reason, out bool transient)
    {
        transient = false;
        var taskbar = FindWindow("Shell_TrayWnd", null);
        if (taskbar == IntPtr.Zero || !GetWindowRect(taskbar, out var taskbarRect))
            return Fail("Windows primary taskbar was not found", out reason);
        if (taskbarRect.Right - taskbarRect.Left <= taskbarRect.Bottom - taskbarRect.Top)
            return Fail("the detected taskbar is not horizontal", out reason);

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            transient = true;
            return Fail("Windows has not exposed the WPF window for positioning yet", out reason);
        }
        var transform = source.CompositionTarget.TransformToDevice;
        var scale = transform.M11;
        var height = (int)Math.Ceiling(Height * transform.M22);
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
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException or TimeoutException)
        {
            buttons.Clear();
        }
        if (buttons.Count > 0)
        {
            _lastButtons = buttons;
            _lastTaskbarRect = taskbarRect;
        }
        else
        {
            transient = true;
            // The cached layout is only valid while the taskbar keeps its size and the failure is short.
            if (_lastButtons is null || !_lastTaskbarRect.Equals(taskbarRect) || _transientFailures >= MaxTransientFailures)
                return Fail("UI Automation could not read the taskbar buttons", out reason);
            buttons = _lastButtons;
        }

        // On current Windows 11 builds TrayNotifyWnd spans the whole notification area (inline tray icons, input
        // indicator, clock); everything left of it is either an app button or free space.
        var tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
        int? trayLeft = tray != IntPtr.Zero && GetWindowRect(tray, out var trayRect) ? trayRect.Left : null;
        var (leftGap, rightGap) = TaskbarLayout.FindGaps(taskbarRect.Left, taskbarRect.Right, trayLeft,
            buttons.Select(button => (button.Bounds.Left, button.Bounds.Right, button.Id)));
        var gaps = new List<TaskbarGap>();
        foreach (var gap in nextToNotificationArea ? new[] { rightGap, leftGap } : new[] { leftGap, rightGap })
            if (gap is TaskbarGap value) gaps.Add(value);

        var count = DesiredPillCount;
        var smallestNeed = (int)Math.Ceiling(TaskbarLayout.WidthFor(1, PillDensity.Minimal) * scale);
        var fullNeed = (int)Math.Ceiling(TaskbarLayout.WidthFor(count, PillDensity.Full) * scale);
        var smallestForAll = (int)Math.Ceiling(TaskbarLayout.WidthFor(count, PillDensity.Minimal) * scale);
        LayoutDiagnostics =
            $"scale={scale:0.##}, taskbar={taskbarRect.Left}-{taskbarRect.Right}x{taskbarRect.Top}-{taskbarRect.Bottom}, " +
            $"tray={(trayLeft is int t ? t.ToString() : "n/a")}, appButtons={buttons.Count(b => trayLeft is null || b.Bounds.Left < trayLeft - 2)}, " +
            $"leftGap={Describe(leftGap)}, rightGap={Describe(rightGap)}, pills={count}, needFull={fullNeed}px, needSmallest={smallestForAll}px";

        var plan = TaskbarLayout.Choose(count, scale, gaps);
        if (plan is not TaskbarPlacement placement)
            return Fail($"not enough free width on the taskbar (need at least {smallestNeed}px for one pill, {smallestForAll}px for all {count}; " +
                $"free: left {Math.Max(0, leftGap?.Width ?? 0)}px, right {Math.Max(0, rightGap?.Width ?? 0)}px)", out reason);

        SetLayout(placement.Density, placement.PillCount >= count ? int.MaxValue : placement.PillCount);
        var width = (int)Math.Ceiling(Width * scale);
        if (width > placement.Gap.Width)
        {
            // A device menu is open, so the pills could not be resized yet; retry on the next refresh.
            transient = true;
            return Fail("the free taskbar space changed while a widget menu was open", out reason);
        }
        var left = placement.Gap.NextToTray ? placement.Gap.Right - width : placement.Gap.Left;
        var top = taskbarRect.Top + (taskbarRect.Bottom - taskbarRect.Top - height) / 2;

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

    private static string Describe(TaskbarGap? gap) => gap is TaskbarGap g ? $"{g.Left}-{g.Right}({g.Width}px)" : "n/a";

    public PillDensity CurrentDensity => _density;

    private static bool Fail(string message, out string reason)
    {
        reason = message;
        return false;
    }


    private sealed class BatteryRing : Grid
    {
        public BatteryRing(int? percent, string? levelLabel = null)
        {
            Width = Height = 23;
            var track = new Ellipse
            {
                Width = 21,
                Height = 21,
                StrokeThickness = 2
            };
            track.SetResourceReference(Shape.StrokeProperty, "ShadcnWidgetTrackBrush");
            Children.Add(track);
            if (percent is int value && value > 0)
            {
                var angle = 2 * Math.PI * Math.Min(value, 99.9) / 100;
                var end = new Point(11.5 + 9.5 * Math.Sin(angle), 11.5 - 9.5 * Math.Cos(angle));
                var path = new Path
                {
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
                path.SetResourceReference(Shape.StrokeProperty, value <= 15 ? "ShadcnDestructiveBrush" : "ShadcnWidgetRingBrush");
                Children.Add(path);
            }
            // "100" is too wide for the ring and collides with it; a full ring already says 100%.
            if (levelLabel is null && percent >= 100)
                return;
            var label = new TextBlock
            {
                Text = levelLabel ?? (percent is int battery ? battery.ToString() : "–"),
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ShadcnWidgetForegroundBrush");
            label.SetResourceReference(TextBlock.FontFamilyProperty, "AppFontFamily");
            Children.Add(label);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);
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
