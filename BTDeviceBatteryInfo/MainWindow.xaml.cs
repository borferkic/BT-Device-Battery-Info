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
    private const double DefaultWindowHeight = 320;
    private const double AdditionalDeviceHeight = 72;
    private const int VisibleDeviceSlots = 2;

    private readonly AppSettings _settings;
    private readonly BluetoothService _bluetooth;
    private readonly FileLogger _logger;
    private Forms.NotifyIcon? _tray;
    private Drawing.Icon? _trayIcon;
    private Forms.ToolStripMenuItem? _toggleWidgetMenuItem;
    private Forms.ToolStripMenuItem? _refreshMenuItem;
    private Forms.ToolStripMenuItem? _startWithWindowsMenuItem;
    private Forms.ToolStripMenuItem? _taskbarWidgetMenuItem;
    private Forms.ToolStripMenuItem? _exitMenuItem;
    private readonly TaskbarWidgetController _taskbarWidget;
    private bool _isExiting;

    public MainViewModel ViewModel { get; }

    public MainWindow(AppSettings settings, BluetoothService bluetooth, FileLogger logger)
    {
        InitializeComponent();

        _settings = settings;
        ThemeManager.Apply(System.Windows.Application.Current.Resources, _settings.ThemeName);
        ThemeManager.ThemeChanged += OnThemeChanged;
        _bluetooth = bluetooth;
        _logger = logger;
        RestorePlacement();
        Topmost = settings.AlwaysOnTop;
        Opacity = settings.Opacity;

        ViewModel = new MainViewModel(settings, bluetooth, logger);
        _taskbarWidget = new TaskbarWidgetController(ViewModel, _settings, SelectTaskbarDeviceAsync, logger);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = ViewModel;

        Loaded += async (_, _) =>
        {
            try
            {
                UpdateWindowHeight();
                await ViewModel.InitializeAsync();
                CreateTrayIcon();
                _taskbarWidget.SetEnabled(_settings.TaskbarWidgetEnabled);
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
        _taskbarWidgetMenuItem = new Forms.ToolStripMenuItem
        {
            Checked = _settings.TaskbarWidgetEnabled
        };
        _taskbarWidgetMenuItem.Click += async (_, _) => await ToggleTaskbarWidgetAsync(hideMainWindow: false);
        menu.Items.Add(_taskbarWidgetMenuItem);
        _refreshMenuItem = new Forms.ToolStripMenuItem();
        _refreshMenuItem.Click += async (_, _) => await ViewModel.RefreshNowAsync();
        menu.Items.Add(_refreshMenuItem);
        _startWithWindowsMenuItem = new Forms.ToolStripMenuItem
        {
            Checked = StartupService.IsEnabled()
        };
        _startWithWindowsMenuItem.Click += async (_, _) => await ToggleStartWithWindowsAsync();
        menu.Items.Add(_startWithWindowsMenuItem);
        _exitMenuItem = new Forms.ToolStripMenuItem();
        _exitMenuItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(_exitMenuItem);
        ApplyTrayTexts();
        AppLanguage.LanguageChanged += OnLanguageChanged;

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
    private async Task ToggleTaskbarWidgetAsync(bool hideMainWindow)
    {
        await SetTaskbarWidgetEnabledAsync(!_settings.TaskbarWidgetEnabled, hideMainWindow);
    }

    private async Task SetTaskbarWidgetEnabledAsync(bool enabled, bool hideMainWindow = false)
    {
        _settings.TaskbarWidgetEnabled = enabled;
        _taskbarWidget.SetEnabled(_settings.TaskbarWidgetEnabled);
        if (_taskbarWidgetMenuItem is not null)
            _taskbarWidgetMenuItem.Checked = _settings.TaskbarWidgetEnabled;
        if (hideMainWindow && _settings.TaskbarWidgetEnabled)
        {
            Hide();
            UpdateWidgetMenuItem();
        }
        try { await SettingsService.SaveAsync(_settings); }
        catch (Exception ex) { await HandleBackgroundErrorAsync("Taskbar mode settings save", ex); }
    }
    private void Exit_Click(object sender, RoutedEventArgs e) => ExitApplication();

    private async void RefreshNow_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshNowAsync();

    private async void Options_Click(object sender, RoutedEventArgs e)
    {
        var optionsWindow = new OptionsWindow(_settings, ViewModel.ConnectedDevices, enabled => SetTaskbarWidgetEnabledAsync(enabled)) { Owner = this };
        if (optionsWindow.ShowDialog() == true)
            _taskbarWidget.RefreshNow();
        await SettingsService.SaveAsync(_settings);
    }

    private async Task SelectTaskbarDeviceAsync(BluetoothDeviceCategory category, BluetoothDeviceItem device)
    {
        switch (category)
        {
            case BluetoothDeviceCategory.Headphones: _settings.TaskbarHeadphonesDeviceId = device.PhysicalDeviceId; break;
            case BluetoothDeviceCategory.Keyboard: _settings.TaskbarKeyboardDeviceId = device.PhysicalDeviceId; break;
            case BluetoothDeviceCategory.Mouse: _settings.TaskbarMouseDeviceId = device.PhysicalDeviceId; break;
            case BluetoothDeviceCategory.GameController: _settings.TaskbarGameControllerDeviceId = device.PhysicalDeviceId; break;
        }
        try
        {
            await SettingsService.SaveAsync(_settings);
            _taskbarWidget.RefreshNow();
        }
        catch (Exception ex) { await HandleBackgroundErrorAsync("Taskbar device selection save", ex); }
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

    private void OnLanguageChanged(object? sender, EventArgs e) => ApplyTrayTexts();

    private void ApplyTrayTexts()
    {
        if (_taskbarWidgetMenuItem is not null) _taskbarWidgetMenuItem.Text = AppLanguage.Get("Tray.TaskbarWidget");
        if (_refreshMenuItem is not null) _refreshMenuItem.Text = AppLanguage.Get("Tray.RefreshNow");
        if (_startWithWindowsMenuItem is not null) _startWithWindowsMenuItem.Text = AppLanguage.Get("Tray.StartWithWindows");
        if (_exitMenuItem is not null) _exitMenuItem.Text = AppLanguage.Get("Tray.Exit");
        UpdateWidgetMenuItem();
    }

    private void UpdateWidgetMenuItem()
    {
        if (_toggleWidgetMenuItem is not null)
            _toggleWidgetMenuItem.Text = AppLanguage.Get(IsVisible ? "Tray.HideWidget" : "Tray.ShowWidget");
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_tray is null) return;
        var previous = _trayIcon;
        _trayIcon = LoadTrayIcon();
        _tray.Icon = _trayIcon;
        previous?.Dispose();
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        var variant = ThemeManager.IsLight ? "light" : "dark";
        var resource = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/Icon/Icon-{variant}.ico", UriKind.Absolute));
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
                $"{AppLanguage.Get("Tray.StartupError")}\n\n{ex.Message}",
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
        AppLanguage.LanguageChanged -= OnLanguageChanged;
        ThemeManager.ThemeChanged -= OnThemeChanged;
        ThemeManager.StopListening();
        _taskbarWidget.Dispose();
        _tray?.Dispose();
        _trayIcon?.Dispose();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.Dispose();
        _bluetooth.Dispose();
    }
}
