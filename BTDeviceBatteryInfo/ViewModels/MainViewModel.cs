using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace BTDeviceBatteryInfo.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppSettings _settings;
    private readonly BluetoothService _bluetooth;
    private readonly FileLogger _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly ObservableCollection<BluetoothDeviceItem> _devices = [];
    private CancellationTokenSource? _reconnectCts;
    private string _status = "DISCONNECTED";
    private string _deviceListMessage = "Searching for connected Bluetooth devices…";
    private bool _isLoading = true;

    public MainViewModel(AppSettings settings, BluetoothService bluetooth, FileLogger logger)
    {
        _settings = settings;
        _bluetooth = bluetooth;
        _logger = logger;
        ReconnectCommand = new AsyncCommand(() => ReconnectAsync(force: false));
        _bluetooth.StateChanged += OnStateChanged;
        _bluetooth.DevicesChanged += OnDevicesChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand ReconnectCommand { get; }
    public ReadOnlyObservableCollection<BluetoothDeviceItem> Devices => new(_devices);
    public int DeviceCount => _devices.Count;
    public string DeviceCountText => _devices.Count == 1 ? "● 1 connected device" : $"● {_devices.Count} connected devices";
    public string DeviceListMessage
    {
        get => _deviceListMessage;
        private set
        {
            if (_deviceListMessage == value) return;
            _deviceListMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeviceListMessageVisibility));
        }
    }
    public Visibility DeviceListMessageVisibility => string.IsNullOrWhiteSpace(DeviceListMessage) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility LoadingVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;
    public string ActionText => _status == "CONNECTED" ? "RECONNECT" : "CONNECT";
    public System.Windows.Media.Brush StatusBrush => _status == "ERROR" ? System.Windows.Media.Brushes.IndianRed : _devices.Count <= 0 ? System.Windows.Media.Brushes.Goldenrod : System.Windows.Media.Brushes.MediumSeaGreen;
    public bool AutoReconnect { get => _settings.AutoReconnect; set { _settings.AutoReconnect = value; OnPropertyChanged(); } }

    public async Task InitializeAsync()
    {
        try
        {
            var devices = await RefreshDevicesAsync();
            var selected = string.IsNullOrWhiteSpace(_settings.DeviceId)
                ? devices.FirstOrDefault(device => device.Name.Contains("Bose", StringComparison.OrdinalIgnoreCase) || device.Name.Contains("QuietComfort", StringComparison.OrdinalIgnoreCase)) ?? devices.FirstOrDefault()
                : devices.FirstOrDefault(device => device.Id == _settings.DeviceId) ?? devices.FirstOrDefault();

            if (selected is not null)
            {
                _settings.DeviceId = selected.Id;
                _settings.DeviceName = selected.Name;
                await SettingsService.SaveAsync(_settings);
                await _bluetooth.SelectAsync(selected.Id);
                _status = selected.IsConnected ? "CONNECTED" : "DISCONNECTED";
            }

            NotifyState();
        }
        finally
        {
            _isLoading = false;
            OnPropertyChanged(nameof(LoadingVisibility));
        }
    }

    public async Task ReconnectAsync(bool force)
    {
        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();
        var token = _reconnectCts.Token;
        _status = "CONNECTING";
        NotifyState();
        await _logger.LogAsync(force ? "Force reconnect started" : "Reconnect started");

        var attempts = force ? _settings.MaximumAttempts : 1;
        foreach (var (delay, attempt) in ReconnectPolicy.Delays(attempts, _settings.RetryDelaySeconds).Select((delay, index) => (delay, index + 1)))
        {
            try
            {
                DeviceListMessage = $"Reconnect attempt {attempt}/{attempts}";
                if (await _bluetooth.RequestConnectionAsync(token))
                {
                    DeviceListMessage = string.Empty;
                    _status = "CONNECTED";
                    NotifyState();
                    await RefreshDevicesAsync();
                    return;
                }
                await Task.Delay(delay, token);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                DeviceListMessage = ex.Message;
                await _logger.LogAsync("Reconnect error: " + ex.Message);
            }
        }

        _status = "ERROR";
        DeviceListMessage = "Unable to restore the selected connection.";
        NotifyState();
        await _logger.LogAsync("Connection failed");
    }

    private async Task<IReadOnlyList<BluetoothDeviceInfo>> RefreshDevicesAsync()
    {
        if (!await _refreshGate.WaitAsync(0)) return [];

        try
        {
            var devices = await _bluetooth.FindConnectedDevicesAsync();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _devices.Clear();
                foreach (var device in devices) _devices.Add(new BluetoothDeviceItem(device.Name, device.BatteryPercent));
                DeviceListMessage = devices.Count == 0 ? "No connected Bluetooth devices found." : string.Empty;
                OnPropertyChanged(nameof(Devices));
                OnPropertyChanged(nameof(DeviceCount));
                OnPropertyChanged(nameof(DeviceCountText));
                OnPropertyChanged(nameof(StatusBrush));
            });
            return devices;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Bluetooth refresh error: " + ex.Message);
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => DeviceListMessage = "Unable to query Bluetooth devices.");
            return [];
        }
        finally { _refreshGate.Release(); }
    }

    private async void OnStateChanged(object? sender, BluetoothDeviceInfo info)
    {
        _status = info.IsConnected ? "CONNECTED" : "DISCONNECTED";
        NotifyState();
        await RefreshDevicesAsync();
        if (!info.IsConnected && _settings.AutoReconnect)
            await Task.Delay(TimeSpan.FromSeconds(_settings.RetryDelaySeconds)).ContinueWith(async _ => await ReconnectAsync(force: true));
    }

    private async void OnDevicesChanged(object? sender, EventArgs e) => await RefreshDevicesAsync();
    private void NotifyState() { OnPropertyChanged(nameof(ActionText)); OnPropertyChanged(nameof(StatusBrush)); }
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { field = value; OnPropertyChanged(name); }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _reconnectCts?.Cancel();
        _refreshGate.Dispose();
        _bluetooth.StateChanged -= OnStateChanged;
        _bluetooth.DevicesChanged -= OnDevicesChanged;
    }
}

public sealed record BluetoothDeviceItem(string Name, int? BatteryPercent)
{
    public string BatteryText => BatteryPercent is int battery ? $"Battery: {battery}%" : "Battery unavailable";
    public System.Windows.Media.Brush BatteryBrush => BatteryPercent switch { <= 15 => System.Windows.Media.Brushes.IndianRed, null => System.Windows.Media.Brushes.DarkGray, _ => System.Windows.Media.Brushes.White };
    public double BatteryFillWidth => BatteryPercent switch { null => 0, <= 15 => 3, <= 50 => 7, <= 75 => 11, _ => 15 };
}
