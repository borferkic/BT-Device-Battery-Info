using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;
using BTDeviceBatteryInfo.ViewModels;
using System.ComponentModel;
using System.Windows;
using Forms = System.Windows.Forms;

namespace BTDeviceBatteryInfo;

public partial class MainWindow : Window, IDisposable
{
    private const double DefaultWindowHeight = 250;
    private const double AdditionalDeviceHeight = 56;
    private const int VisibleDeviceSlots = 2;

    private readonly AppSettings _settings;
    private readonly BluetoothService _bluetooth;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _toggleWidgetMenuItem;
    private Forms.ToolStripMenuItem? _startWithWindowsMenuItem;
    private bool _isExiting;

    public MainViewModel ViewModel { get; }

    public MainWindow(AppSettings settings, BluetoothService bluetooth, FileLogger logger)
    {
        InitializeComponent();

        _settings = settings;
        _bluetooth = bluetooth;
        RestorePlacement();
        Topmost = settings.AlwaysOnTop;
        Opacity = settings.Opacity;

        ViewModel = new MainViewModel(settings, bluetooth, logger);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = ViewModel;

        Loaded += async (_, _) =>
        {
            CreateTrayIcon();
            UpdateWindowHeight();
            await ViewModel.InitializeAsync();
        };
        LocationChanged += async (_, _) =>
        {
            _settings.Left = Left;
            _settings.Top = Top;
            await SettingsService.SaveAsync(_settings);
        };
        Closing += OnClosing;
    }

    public void CreateTrayIcon()
    {
        if (_tray != null) return;

        var menu = new Forms.ContextMenuStrip();
        _toggleWidgetMenuItem = new Forms.ToolStripMenuItem();
        _toggleWidgetMenuItem.Click += (_, _) => ToggleWidgetFromTray();
        menu.Items.Add(_toggleWidgetMenuItem);
        _startWithWindowsMenuItem = new Forms.ToolStripMenuItem("Start with Windows")
        {
            Checked = StartupService.IsEnabled()
        };
        _startWithWindowsMenuItem.Click += async (_, _) => await ToggleStartWithWindowsAsync();
        menu.Items.Add(_startWithWindowsMenuItem);
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _tray = new Forms.NotifyIcon
        {
            Text = AppIdentity.DisplayName,
            Icon = System.Drawing.SystemIcons.Information,
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowWidget();
        UpdateWidgetMenuItem();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.DeviceCount))
            Dispatcher.BeginInvoke(UpdateWindowHeight);
    }

    private void UpdateWindowHeight()
    {
        var extraDevices = Math.Max(0, ViewModel.DeviceCount - VisibleDeviceSlots);
        var targetHeight = DefaultWindowHeight + extraDevices * AdditionalDeviceHeight;
        Height = targetHeight;
    }

    private void RestorePlacement()
    {
        var right = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        var bottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        if (_settings.Left < SystemParameters.VirtualScreenLeft || _settings.Top < SystemParameters.VirtualScreenTop || _settings.Left > right - 50 || _settings.Top > bottom - 50)
        {
            Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
            Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - Height) / 2;
        }
        else
        {
            Left = _settings.Left;
            Top = _settings.Top;
        }
    }

    private void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        UpdateWidgetMenuItem();
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void ExitApplication()
    {
        _isExiting = true;
        if (_tray is not null) _tray.Visible = false;
        System.Windows.Application.Current.Shutdown();
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_isExiting && _tray is not null)
        {
            e.Cancel = true;
            Hide();
            UpdateWidgetMenuItem();
        }
    }

    private void ToggleWidgetFromTray()
    {
        if (IsVisible)
        {
            Hide();
            UpdateWidgetMenuItem();
        }
        else
        {
            ShowWidget();
        }
    }

    private void ShowWidget()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        UpdateWidgetMenuItem();
    }

    private void UpdateWidgetMenuItem()
    {
        if (_toggleWidgetMenuItem is not null)
            _toggleWidgetMenuItem.Text = IsVisible ? "Hide widget" : "Show widget";
    }

    private async Task ToggleStartWithWindowsAsync()
    {
        var enabled = _startWithWindowsMenuItem?.Checked != true;

        try
        {
            StartupService.SetEnabled(enabled);
            _settings.StartWithWindows = enabled;
            await SettingsService.SaveAsync(_settings);

            if (_startWithWindowsMenuItem is not null)
                _startWithWindowsMenuItem.Checked = enabled;
        }
        catch (Exception ex)
        {
            if (_startWithWindowsMenuItem is not null)
                _startWithWindowsMenuItem.Checked = StartupService.IsEnabled();

            System.Windows.MessageBox.Show(
                $"Could not update the Windows startup setting.\n\n{ex.Message}",
                "BT Device Battery Info",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void Dispose()
    {
        _tray?.Dispose();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Dispose();
        _bluetooth.Dispose();
    }
}
