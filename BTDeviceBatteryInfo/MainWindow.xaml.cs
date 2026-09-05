using System.ComponentModel;
using System.Windows;
using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;
using BTDeviceBatteryInfo.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace BTDeviceBatteryInfo;

public partial class MainWindow : Window, IDisposable
{
    private const double DefaultWindowHeight = 250;
    private const double AdditionalDeviceHeight = 56;
    private const int VisibleDeviceSlots = 2;

    private readonly AppSettings _settings;
    private readonly BluetoothService _bluetooth;
    private readonly FileLogger _logger;
    private Forms.NotifyIcon? _tray;
    private Drawing.Icon? _trayIcon;
    private Forms.ToolStripMenuItem? _toggleWidgetMenuItem;
    private Forms.ToolStripMenuItem? _startWithWindowsMenuItem;
    private bool _isExiting;

    public MainViewModel ViewModel { get; }

    public MainWindow(AppSettings settings, BluetoothService bluetooth, FileLogger logger)
    {
        InitializeComponent();

        _settings = settings;
        _bluetooth = bluetooth;
        _logger = logger;
        RestorePlacement();
        Topmost = settings.AlwaysOnTop;
        Opacity = settings.Opacity;

        ViewModel = new MainViewModel(settings, bluetooth, logger);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = ViewModel;

        Loaded += async (_, _) =>
        {
            try
            {
                UpdateWindowHeight();
                await ViewModel.InitializeAsync();
                CreateTrayIcon();
            }
            catch (Exception ex)
            {
                await HandleBackgroundErrorAsync("Window initialization", ex);
            }
        };
        LocationChanged += (_, _) =>
        {
            _settings.Left = Left;
            _settings.Top = Top;
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
            Icon = _trayIcon ??= LoadTrayIcon(),
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowWidget();
        UpdateWidgetMenuItem();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.DeviceCount) or nameof(MainViewModel.VisibleDeviceRowCount))
            Dispatcher.BeginInvoke(UpdateWindowHeight);
    }

    private void UpdateWindowHeight()
    {
        var extraDevices = Math.Max(0, ViewModel.VisibleDeviceRowCount - VisibleDeviceSlots);
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

    private async void DragWindow(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        try
        {
            DragMove();
            await SettingsService.SaveAsync(_settings);
        }
        catch (Exception ex) { await HandleBackgroundErrorAsync("Window placement save", ex); }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        UpdateWidgetMenuItem();
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow { Owner = this };
        aboutWindow.ShowDialog();
    }

    private async void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;
        try { await SettingsService.SaveAsync(_settings); }
        catch (Exception ex) { await HandleBackgroundErrorAsync("Exit settings save", ex); }
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

    private static Drawing.Icon LoadTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Icon.ico", UriKind.Absolute));
        if (resource is null) return (Drawing.Icon)Drawing.SystemIcons.Information.Clone();

        using (resource.Stream)
        using (var icon = new Drawing.Icon(resource.Stream))
            return (Drawing.Icon)icon.Clone();
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

    private async Task HandleBackgroundErrorAsync(string operation, Exception exception)
    {
        try
        {
            await _logger.LogAsync($"{operation} error: {exception}");
        }
        catch
        {
            // An error while recording a background failure must not become a second UI failure.
        }
    }

    public void Dispose()
    {
        _tray?.Dispose();
        _trayIcon?.Dispose();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Dispose();
        _bluetooth.Dispose();
    }
}
