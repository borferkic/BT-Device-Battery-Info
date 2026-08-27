using BTDeviceBatteryInfo.Services;
using System.Threading;
using System.Windows;

namespace BTDeviceBatteryInfo;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = @"Local\BTDeviceBatteryInfo.SingleInstance";

    private MainWindow? _window;
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            System.Windows.MessageBox.Show(
                "BT Device Battery Info is already open.",
                "Application already open",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _ownsInstanceMutex = true;

        var settings = await SettingsService.LoadAsync();
        var logger = new FileLogger();
        var bluetooth = new BluetoothService(logger);
        _window = new MainWindow(settings, bluetooth, logger);

        if (!settings.LaunchMinimized)
            _window.Show();
        else
            _window.CreateTrayIcon();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _window?.Dispose();
        if (_ownsInstanceMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
