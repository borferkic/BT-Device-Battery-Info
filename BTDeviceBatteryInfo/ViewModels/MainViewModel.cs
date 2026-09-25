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
    private readonly BluetoothRadioService _radio;
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
    private string _deviceListMessageKey = "Main.Searching";
    private bool _isLoading = true;
    private bool _isTurningOnBluetooth;
    private string _bluetoothActionStatus = string.Empty;

    public MainViewModel(AppSettings settings, BluetoothService bluetooth, FileLogger logger)
    {
        _settings = settings;
        _bluetooth = bluetooth;
        _logger = logger;
        _readOnlyConnectedDevices = new ReadOnlyObservableCollection<BluetoothDeviceItem>(_connectedDevices);
        _readOnlyDisconnectedDevices = new ReadOnlyObservableCollection<BluetoothDeviceItem>(_disconnectedDevices);
        ReconnectCommand = new AsyncCommand(() => ReconnectAsync(force: false));
        TurnOnBluetoothCommand = new AsyncCommand(TurnOnBluetoothAsync);
        OpenBluetoothSettingsCommand = new AsyncCommand(OpenBluetoothSettingsAsync);
        _radio = new BluetoothRadioService(logger);
        _radio.StatusChanged += OnRadioStatusChanged;
        AppLanguage.LanguageChanged += OnLanguageChanged;
        _bluetooth.StateChanged += OnStateChanged;
        _bluetooth.DevicesChanged += OnDevicesChanged;
        _bluetooth.InitialDiscoveryCompleted += OnInitialDiscoveryCompleted;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand ReconnectCommand { get; }
    public ICommand TurnOnBluetoothCommand { get; }
    public ICommand OpenBluetoothSettingsCommand { get; }

    /// <summary>True when Windows reports the Bluetooth radio as off, disabled, or missing.</summary>
    public bool IsBluetoothUnavailable => _radio.Status is BluetoothRadioStatus.Off or BluetoothRadioStatus.Disabled or BluetoothRadioStatus.NoAdapter;
    public Visibility BluetoothUnavailableVisibility => IsBluetoothUnavailable ? Visibility.Visible : Visibility.Collapsed;
    public string BluetoothUnavailableTitle => _radio.Status switch
    {
        BluetoothRadioStatus.Disabled => AppLanguage.Get("Radio.DisabledTitle"),
        BluetoothRadioStatus.NoAdapter => AppLanguage.Get("Radio.NoAdapterTitle"),
        _ => AppLanguage.Get("Radio.OffTitle")
    };
    public string BluetoothUnavailableDescription => _radio.Status switch
    {
        BluetoothRadioStatus.Disabled => AppLanguage.Get("Radio.DisabledDescription"),
        BluetoothRadioStatus.NoAdapter => AppLanguage.Get("Radio.NoAdapterDescription"),
        _ => AppLanguage.Get("Radio.OffDescription")
    };
    public Visibility TurnOnBluetoothVisibility => _radio.Status == BluetoothRadioStatus.Off ? Visibility.Visible : Visibility.Collapsed;
    public bool IsTurningOnBluetooth
    {
        get => _isTurningOnBluetooth;
        private set { if (_isTurningOnBluetooth == value) return; _isTurningOnBluetooth = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanTurnOnBluetooth)); }
    }
    public bool CanTurnOnBluetooth => !IsTurningOnBluetooth;
    public string BluetoothActionStatus
    {
        get => _bluetoothActionStatus;
        private set
        {
            if (_bluetoothActionStatus == value) return;
            _bluetoothActionStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(BluetoothActionStatusVisibility));
        }
    }
    public Visibility BluetoothActionStatusVisibility => string.IsNullOrWhiteSpace(BluetoothActionStatus) ? Visibility.Collapsed : Visibility.Visible;
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
    public string DeviceCountText => DeviceCount == 1 ? AppLanguage.Get("Main.DeviceCountOne") : AppLanguage.Format("Main.DeviceCountMany", DeviceCount);
    /// <summary>Localized list message. The setter takes a localization key; unknown keys (for example exception text) are shown as-is.</summary>
    public string DeviceListMessage
    {
        get => string.IsNullOrWhiteSpace(_deviceListMessageKey) ? string.Empty : AppLanguage.Get(_deviceListMessageKey);
        private set
        {
            if (_deviceListMessageKey == value) return;
            _deviceListMessageKey = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeviceListMessageVisibility));
        }
    }
    public Visibility DeviceListMessageVisibility => string.IsNullOrWhiteSpace(DeviceListMessage) ? Visibility.Collapsed : Visibility.Visible;
    private bool _isRefreshing;
    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set
        {
            if (_isRefreshing == value) return;
            _isRefreshing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRefresh));
            OnPropertyChanged(nameof(RefreshButtonText));
        }
    }
    public bool CanRefresh => !IsRefreshing;
    public string RefreshButtonText => AppLanguage.Get(IsRefreshing ? "Main.Refreshing" : "Main.RefreshNow");
    public Visibility LoadingVisibility => _isLoading && !IsBluetoothUnavailable ? Visibility.Visible : Visibility.Collapsed;
    public string ActionText => _status == "CONNECTED" ? "RECONNECT" : "CONNECT";
    public System.Windows.Media.Brush StatusBrush => _status == "ERROR" ? ThemeManager.Brush("ShadcnDestructiveBrush") : ConnectedDeviceCount <= 0 ? ThemeManager.Brush("ShadcnWarningBrush") : ThemeManager.Brush("ShadcnSuccessBrush");
    public bool AutoReconnect { get => _settings.AutoReconnect; set { _settings.AutoReconnect = value; OnPropertyChanged(); } }

    private async Task InvokeOnUiAsync(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (_isDisposed || dispatcher is null || dispatcher.HasShutdownStarted) return;
        await dispatcher.InvokeAsync(action);
    }

    public async Task InitializeAsync()
    {
        await _radio.InitializeAsync();
        NotifyRadioState();
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
            await InvokeOnUiAsync(() => IsRefreshing = true);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await _bluetooth.RefreshNowAsync();
            var queryMilliseconds = stopwatch.ElapsedMilliseconds;
            await RefreshDevicesAsync();
            await _logger.LogAsync($"Manual refresh completed in {stopwatch.ElapsedMilliseconds} ms (Windows query {queryMilliseconds} ms).");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Manual Bluetooth refresh error: " + ex.Message);
        }
        finally
        {
            // Only the latest refresh clears the indicator, so an overlapping refresh cannot leave it stuck or clear it early.
            if (ReferenceEquals(Volatile.Read(ref _manualRefreshCts), refreshCts))
                await InvokeOnUiAsync(() => IsRefreshing = false);
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
            foreach (var delay in ReconnectPolicy.Delays(attempts, _settings.RetryDelaySeconds))
            {
                token.ThrowIfCancellationRequested();
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
                DeviceListMessage = "Main.RestoreError";
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
                            ? "Main.NoDevices"
                            : "Main.Searching"
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
                await InvokeOnUiAsync(() => DeviceListMessage = "Main.QueryError");
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
            var item = new BluetoothDeviceItem(device.Name, device.IsConnected, device.BatteryPercent, device.Category, device.PhysicalDeviceId);
            if (index == target.Count) target.Add(item);
            else if (target[index] != item) target[index] = item;
            index++;
        }
        while (target.Count > index) target.RemoveAt(target.Count - 1);
    }

    private async Task TrySelectPreferredDeviceAsync(IReadOnlyList<BluetoothDeviceInfo> devices, bool discoveryCompleted)
    {
        if (_selectionResolved || devices.Count == 0) return;

        BluetoothDeviceInfo? current = null;
        if (!string.IsNullOrWhiteSpace(_settings.DeviceId))
        {
            current = await _bluetooth.SelectAsync(_settings.DeviceId);
            if (current is null && !discoveryCompleted) return;
        }

        if (current is null)
        {
            var selected = string.IsNullOrWhiteSpace(_settings.DeviceId)
                ? devices.FirstOrDefault(device => device.IsConnected)
                : null;
            if (selected is null && !discoveryCompleted) return;
            selected ??= devices.FirstOrDefault();
            if (selected is null) return;
            current = await _bluetooth.SelectAsync(selected.Id);
        }

        if (current is null) return;

        _selectionResolved = true;
        _settings.DeviceId = current.PhysicalDeviceId;
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

    private async Task TurnOnBluetoothAsync()
    {
        if (IsTurningOnBluetooth) return;
        IsTurningOnBluetooth = true;
        BluetoothActionStatus = AppLanguage.Get("Radio.TurningOn");
        try
        {
            var result = await _radio.TurnOnAsync();
            // On success the radio StateChanged event refreshes the view; report failures as Windows returned them.
            BluetoothActionStatus = result.Succeeded ? string.Empty : AppLanguage.Get(result.MessageKey);
        }
        finally
        {
            IsTurningOnBluetooth = false;
        }
    }

    private static Task OpenBluetoothSettingsAsync()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private void OnRadioStatusChanged(object? sender, EventArgs e) => _ = InvokeOnUiAsync(async () =>
    {
        NotifyRadioState();
        if (_radio.Status == BluetoothRadioStatus.On)
        {
            BluetoothActionStatus = string.Empty;
            await RefreshNowAsync();
        }
    });

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(DeviceListMessage));
        OnPropertyChanged(nameof(RefreshButtonText));
        OnPropertyChanged(nameof(DeviceCountText));
        NotifyRadioState();
        // Device cards compute their texts on access; replace each item so the bindings re-read them.
        foreach (var collection in new[] { _connectedDevices, _disconnectedDevices })
            for (var i = 0; i < collection.Count; i++) collection[i] = collection[i] with { };
    }

    private void NotifyRadioState()
    {
        OnPropertyChanged(nameof(IsBluetoothUnavailable));
        OnPropertyChanged(nameof(BluetoothUnavailableVisibility));
        OnPropertyChanged(nameof(BluetoothUnavailableTitle));
        OnPropertyChanged(nameof(BluetoothUnavailableDescription));
        OnPropertyChanged(nameof(TurnOnBluetoothVisibility));
        OnPropertyChanged(nameof(LoadingVisibility));
    }

    public void Dispose()
    {
        _isDisposed = true;
        _radio.StatusChanged -= OnRadioStatusChanged;
        AppLanguage.LanguageChanged -= OnLanguageChanged;
        _radio.Dispose();
        _lifetimeCts.Cancel();
        _manualRefreshCts?.Cancel();
        Interlocked.Exchange(ref _reconnectCts, null)?.Cancel();
        _bluetooth.StateChanged -= OnStateChanged;
        _bluetooth.DevicesChanged -= OnDevicesChanged;
        _bluetooth.InitialDiscoveryCompleted -= OnInitialDiscoveryCompleted;
    }
}

public sealed record BluetoothDeviceItem(string Name, bool IsConnected, int? BatteryPercent, BluetoothDeviceCategory Category, string PhysicalDeviceId)
{
    public string ConnectionText => AppLanguage.Get(IsConnected ? "Device.Connected" : "Device.Disconnected");
    public bool HasBattery => BatteryPercent is not null;
    public string BatteryText => BatteryPercent is int battery ? AppLanguage.Format("Device.Battery", battery) : AppLanguage.Get("Device.BatteryUnavailable");
    public bool IsBatteryLow => BatteryPercent <= 15;
    public double BatteryFillWidth => BatteryPercent switch { null => 0, <= 15 => 3, <= 50 => 7, <= 75 => 11, _ => 15 };
}
