using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BTDeviceBatteryInfo.Models;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppSettings _settings;
    private readonly BluetoothService _bluetooth;
    private readonly FileLogger _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly ObservableCollection<BluetoothDeviceItem> _connectedDevices = [];
    private readonly ObservableCollection<BluetoothDeviceItem> _disconnectedDevices = [];
    private readonly ReadOnlyObservableCollection<BluetoothDeviceItem> _readOnlyConnectedDevices;
    private readonly ReadOnlyObservableCollection<BluetoothDeviceItem> _readOnlyDisconnectedDevices;
    private CancellationTokenSource? _reconnectCts;
    private CancellationTokenSource? _manualRefreshCts;
    private int _refreshQueued;
    private int _reconnectGeneration;
    private bool _isDisposed;
    private bool _selectionResolved;
    private bool _disconnectedDevicesExpanded;
    private string _status = "DISCONNECTED";
    private string _deviceListMessage = "Searching for paired or connected Bluetooth devices…";
    private string _refreshStatus = string.Empty;
    private bool _isLoading = true;

    public MainViewModel(AppSettings settings, BluetoothService bluetooth, FileLogger logger)
    {
        _settings = settings;
        _bluetooth = bluetooth;
        _logger = logger;
        _readOnlyConnectedDevices = new ReadOnlyObservableCollection<BluetoothDeviceItem>(_connectedDevices);
        _readOnlyDisconnectedDevices = new ReadOnlyObservableCollection<BluetoothDeviceItem>(_disconnectedDevices);
        ReconnectCommand = new AsyncCommand(() => ReconnectAsync(force: false));
        _bluetooth.StateChanged += OnStateChanged;
        _bluetooth.DevicesChanged += OnDevicesChanged;
        _bluetooth.InitialDiscoveryCompleted += OnInitialDiscoveryCompleted;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand ReconnectCommand { get; }
    public ReadOnlyObservableCollection<BluetoothDeviceItem> ConnectedDevices => _readOnlyConnectedDevices;
    public ReadOnlyObservableCollection<BluetoothDeviceItem> DisconnectedDevices => _readOnlyDisconnectedDevices;
    public int DeviceCount => ConnectedDeviceCount + DisconnectedDeviceCount;
    public int ConnectedDeviceCount => _connectedDevices.Count;
    public int DisconnectedDeviceCount => _disconnectedDevices.Count;
    public string DisconnectedDeviceCountText => DisconnectedDeviceCount.ToString();
    public Visibility DisconnectedDevicesVisibility => DisconnectedDeviceCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    public int VisibleDeviceRowCount => ConnectedDeviceCount + (DisconnectedDeviceCount > 0 ? 1 : 0) + (IsDisconnectedDevicesExpanded ? DisconnectedDeviceCount : 0);
    public bool IsDisconnectedDevicesExpanded
    {
        get => _disconnectedDevicesExpanded;
        set
        {
            if (_disconnectedDevicesExpanded == value) return;
            _disconnectedDevicesExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VisibleDeviceRowCount));
        }
    }
    public string DeviceCountText => DeviceCount == 1 ? "● 1 Bluetooth device" : $"● {DeviceCount} Bluetooth devices";
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
    public string RefreshStatus
    {
        get => _refreshStatus;
        private set
        {
            if (_refreshStatus == value) return;
            _refreshStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RefreshStatusVisibility));
        }
    }
    public Visibility RefreshStatusVisibility => string.IsNullOrWhiteSpace(RefreshStatus) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility LoadingVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;
    public string ActionText => _status == "CONNECTED" ? "RECONNECT" : "CONNECT";
    public System.Windows.Media.Brush StatusBrush => _status == "ERROR" ? System.Windows.Media.Brushes.IndianRed : ConnectedDeviceCount <= 0 ? System.Windows.Media.Brushes.Goldenrod : System.Windows.Media.Brushes.MediumSeaGreen;
    public bool AutoReconnect { get => _settings.AutoReconnect; set { _settings.AutoReconnect = value; OnPropertyChanged(); } }

    private async Task InvokeOnUiAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (_isDisposed || dispatcher is null || dispatcher.HasShutdownStarted) return;
        await dispatcher.InvokeAsync(action);
    }

    public async Task InitializeAsync()
    {
        await RefreshDevicesAsync();
        if (_bluetooth.IsInitialDiscoveryCompleted)
            await CompleteLoadingAsync();
    }

    public async Task RefreshNowAsync()
    {
        if (_isDisposed) return;

        var refreshCts = new CancellationTokenSource();
        var previousRefresh = Interlocked.Exchange(ref _manualRefreshCts, refreshCts);
        previousRefresh?.Cancel();
        var token = refreshCts.Token;

        try
        {
            await InvokeOnUiAsync(() => RefreshStatus = "Refreshing devices and battery…");
            await _bluetooth.RefreshNowAsync();
            await RefreshDevicesAsync();

            await Task.Delay(TimeSpan.FromSeconds(10), token);
            await InvokeOnUiAsync(() => RefreshStatus = string.Empty);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Manual Bluetooth refresh error: " + ex.Message);
            await InvokeOnUiAsync(() => RefreshStatus = "Unable to refresh Bluetooth devices.");
        }
        finally
        {
            if (ReferenceEquals(Volatile.Read(ref _manualRefreshCts), refreshCts))
                Interlocked.CompareExchange(ref _manualRefreshCts, null, refreshCts);
            refreshCts.Dispose();
        }
    }

    public async Task ReconnectAsync(bool force)
    {
        if (_isDisposed) return;

        var reconnectCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
        var previousReconnect = Interlocked.Exchange(ref _reconnectCts, reconnectCts);
        previousReconnect?.Cancel();
        Interlocked.Increment(ref _reconnectGeneration);
        var token = reconnectCts.Token;

        try
        {
            await InvokeOnUiAsync(() =>
            {
                _status = "CONNECTING";
                NotifyState();
            });
            await _logger.LogAsync(force ? "Force reconnect started" : "Reconnect started");

            var attempts = force ? _settings.MaximumAttempts : 1;
            foreach (var (delay, attempt) in ReconnectPolicy.Delays(attempts, _settings.RetryDelaySeconds).Select((delay, index) => (delay, index + 1)))
            {
                token.ThrowIfCancellationRequested();
                await InvokeOnUiAsync(() => DeviceListMessage = $"Reconnect attempt {attempt}/{attempts}");
                if (await _bluetooth.RequestConnectionAsync(token))
                {
                    await InvokeOnUiAsync(() =>
                    {
                        DeviceListMessage = string.Empty;
                        _status = "CONNECTED";
                        NotifyState();
                    });
                    await RefreshDevicesAsync();
                    return;
                }
                await Task.Delay(delay, token);
            }

            await InvokeOnUiAsync(() =>
            {
                _status = "ERROR";
                DeviceListMessage = "Unable to restore the selected connection.";
                NotifyState();
            });
            await _logger.LogAsync("Connection failed");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await InvokeOnUiAsync(() => DeviceListMessage = ex.Message);
            await _logger.LogAsync("Reconnect error: " + ex.Message);
        }
    }

    private async Task<IReadOnlyList<BluetoothDeviceInfo>> RefreshDevicesAsync()
    {
        if (_isDisposed) return [];
        if (Interlocked.Exchange(ref _refreshQueued, 1) != 0) return [];

        var refreshFailed = false;
        var gateEntered = false;

        try
        {
            await _refreshGate.WaitAsync(_lifetimeCts.Token);
            gateEntered = true;
            IReadOnlyList<BluetoothDeviceInfo> latestDevices = [];
            do
            {
                Interlocked.Exchange(ref _refreshQueued, 0);
                latestDevices = await _bluetooth.FindDevicesAsync();
                var discoveryCompleted = _bluetooth.IsInitialDiscoveryCompleted;
                await InvokeOnUiAsync(() =>
                {
                    SynchronizeItems(_connectedDevices, latestDevices.Where(device => device.IsConnected));
                    SynchronizeItems(_disconnectedDevices, latestDevices.Where(device => !device.IsConnected));
                    if (_disconnectedDevices.Count == 0)
                        _disconnectedDevicesExpanded = false;
                    DeviceListMessage = latestDevices.Count == 0
                        ? discoveryCompleted
                            ? "No paired or connected Bluetooth devices found."
                            : "Searching for paired or connected Bluetooth devices…"
                        : string.Empty;
                    OnPropertyChanged(nameof(DeviceCount));
                    OnPropertyChanged(nameof(ConnectedDeviceCount));
                    OnPropertyChanged(nameof(DisconnectedDeviceCount));
                    OnPropertyChanged(nameof(DisconnectedDeviceCountText));
                    OnPropertyChanged(nameof(DisconnectedDevicesVisibility));
                    OnPropertyChanged(nameof(IsDisconnectedDevicesExpanded));
                    OnPropertyChanged(nameof(VisibleDeviceRowCount));
                    OnPropertyChanged(nameof(DeviceCountText));
                    OnPropertyChanged(nameof(StatusBrush));
                });
                await TrySelectPreferredDeviceAsync(latestDevices, discoveryCompleted);
                if (latestDevices.Count > 0 || discoveryCompleted)
                    await CompleteLoadingAsync();
            }
            while (Volatile.Read(ref _refreshQueued) != 0);

            return latestDevices;
        }
        catch (OperationCanceledException) when (_isDisposed) { return []; }
        catch (Exception ex)
        {
            refreshFailed = true;
            if (!_isDisposed)
            {
                await _logger.LogAsync("Bluetooth refresh error: " + ex.Message);
                await InvokeOnUiAsync(() => DeviceListMessage = "Unable to query Bluetooth devices.");
            }
            return [];
        }
        finally
        {
            if (gateEntered) _refreshGate.Release();
            if (refreshFailed && !_isDisposed && Interlocked.Exchange(ref _refreshQueued, 0) != 0)
                _ = RefreshDevicesAsync();
        }
    }

    private static void SynchronizeItems(ObservableCollection<BluetoothDeviceItem> target, IEnumerable<BluetoothDeviceInfo> devices)
    {
        var index = 0;
        foreach (var device in devices)
        {
            var item = new BluetoothDeviceItem(device.Name, device.IsConnected, device.BatteryPercent);
            if (index == target.Count) target.Add(item);
            else if (target[index] != item) target[index] = item;
            index++;
        }
        while (target.Count > index) target.RemoveAt(target.Count - 1);
    }

    private async Task TrySelectPreferredDeviceAsync(IReadOnlyList<BluetoothDeviceInfo> devices, bool discoveryCompleted)
    {
        if (_selectionResolved || devices.Count == 0) return;

        BluetoothDeviceInfo? selected;
        if (!string.IsNullOrWhiteSpace(_settings.DeviceId))
        {
            selected = devices.FirstOrDefault(device => device.Id == _settings.DeviceId);
            if (selected is null && !discoveryCompleted) return;
        }
        else
        {
            selected = devices.FirstOrDefault(device =>
                device.Name.Contains("Bose", StringComparison.OrdinalIgnoreCase)
                || device.Name.Contains("QuietComfort", StringComparison.OrdinalIgnoreCase));
            if (selected is null && !discoveryCompleted) return;
        }

        selected ??= devices.FirstOrDefault();
        if (selected is null) return;

        var current = await _bluetooth.SelectAsync(selected.Id);
        if (current is null) return;

        _selectionResolved = true;
        _settings.DeviceId = current.Id;
        _settings.DeviceName = current.Name;
        await SettingsService.SaveAsync(_settings);
        await InvokeOnUiAsync(() =>
        {
            _status = current.IsConnected ? "CONNECTED" : "DISCONNECTED";
            NotifyState();
        });
    }

    private async Task CompleteLoadingAsync()
    {
        await InvokeOnUiAsync(() =>
        {
            if (!_isLoading) return;
            _isLoading = false;
            OnPropertyChanged(nameof(LoadingVisibility));
        });
    }

    private async void OnStateChanged(object? sender, BluetoothDeviceInfo info)
    {
        try
        {
            await InvokeOnUiAsync(() =>
            {
                _status = info.IsConnected ? "CONNECTED" : "DISCONNECTED";
                NotifyState();
            });
            await RefreshDevicesAsync();
            if (!info.IsConnected && _settings.AutoReconnect)
            {
                var generationAtDisconnection = Volatile.Read(ref _reconnectGeneration);
                await Task.Delay(TimeSpan.FromSeconds(_settings.RetryDelaySeconds), _lifetimeCts.Token);
                if (_isDisposed || generationAtDisconnection != Volatile.Read(ref _reconnectGeneration)) return;
                if ((await _bluetooth.CurrentAsync())?.IsConnected ?? false) return;
                await ReconnectAsync(force: true);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await _logger.LogAsync("Bluetooth state-change error: " + ex.Message); }
    }

    private async void OnDevicesChanged(object? sender, EventArgs e) => await RefreshDevicesAsync();

    private async void OnInitialDiscoveryCompleted(object? sender, EventArgs e)
    {
        try
        {
            await RefreshDevicesAsync();
            await CompleteLoadingAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Initial Bluetooth discovery completion error: " + ex.Message);
        }
    }
    private void NotifyState() { OnPropertyChanged(nameof(ActionText)); OnPropertyChanged(nameof(StatusBrush)); }
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { field = value; OnPropertyChanged(name); }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _isDisposed = true;
        _lifetimeCts.Cancel();
        _manualRefreshCts?.Cancel();
        Interlocked.Exchange(ref _reconnectCts, null)?.Cancel();
        _bluetooth.StateChanged -= OnStateChanged;
        _bluetooth.DevicesChanged -= OnDevicesChanged;
        _bluetooth.InitialDiscoveryCompleted -= OnInitialDiscoveryCompleted;
    }
}

public sealed record BluetoothDeviceItem(string Name, bool IsConnected, int? BatteryPercent)
{
    public string ConnectionText => IsConnected ? "Connected" : "Disconnected";
    public System.Windows.Media.Brush ConnectionBrush => IsConnected ? System.Windows.Media.Brushes.MediumSeaGreen : System.Windows.Media.Brushes.DarkGray;
    public string BatteryText => BatteryPercent is int battery ? $"Battery: {battery}%" : "Battery unavailable";
    public System.Windows.Media.Brush BatteryBrush => BatteryPercent switch { <= 15 => System.Windows.Media.Brushes.IndianRed, null => System.Windows.Media.Brushes.DarkGray, _ => System.Windows.Media.Brushes.White };
    public double BatteryFillWidth => BatteryPercent switch { null => 0, <= 15 => 3, <= 50 => 7, <= 75 => 11, _ => 15 };
}
