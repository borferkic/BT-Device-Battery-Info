using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo;

public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = @"Local\BTDeviceBatteryInfo.SingleInstance";

    private MainWindow? _window;
    private FileLogger? _logger;
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;
    private bool _handlingUnhandledException;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
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

            _logger = new FileLogger();
            var settings = await SettingsService.LoadAsync();
            var bluetooth = new BluetoothService(_logger);
            _window = new MainWindow(settings, bluetooth, _logger);
            _window.Show();
        }
        catch (Exception ex)
        {
            RecordUnhandledException("Startup error", ex);
            System.Windows.MessageBox.Show(
                $"The application could not start.\n\n{ex.Message}",
                "BT Device Battery Info",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        RecordUnhandledException("Unhandled UI error", e.Exception);
        if (_handlingUnhandledException) return;

        _handlingUnhandledException = true;
        System.Windows.MessageBox.Show(
            $"The application encountered an unexpected error and will close.\n\n{e.Exception.Message}",
            "BT Device Battery Info",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(1);
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
            RecordUnhandledException("Unhandled application error", exception);
    }

    private static void RecordUnhandledException(string category, Exception exception)
    {
        try
        {
            var directory = AppIdentity.DataDirectory;
            Directory.CreateDirectory(directory);
            var path = System.IO.Path.Combine(directory, "errors.log");
            File.AppendAllText(path, $"[{DateTime.Now:O}] {category}{Environment.NewLine}{exception}{Environment.NewLine}");
        }
        catch
        {
            // Error reporting must not trigger another unhandled exception.
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _window?.Dispose();
        if (_ownsInstanceMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
