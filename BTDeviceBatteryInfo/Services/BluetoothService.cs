using System.Diagnostics;
using System.Globalization;
using BTDeviceBatteryInfo.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BTDeviceBatteryInfo.Services;

/// <summary>Keeps an in-memory view of Windows Bluetooth endpoints and updates it when Windows reports a change.</summary>
public sealed class BluetoothService : IDisposable
{
    private static readonly TimeSpan InitialDiscoveryTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan BatteryQueryTimeout = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan BatteryUnavailableRetryDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan BatteryRefreshInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan BatteryValueExpiration = TimeSpan.FromMinutes(10);
    private const string BluetoothClassicProtocolId = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
    private const string BluetoothLeProtocolId = "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}";
    private static readonly string BluetoothSelector = $"System.Devices.Aep.ProtocolId:=\"{BluetoothClassicProtocolId}\" OR System.Devices.Aep.ProtocolId:=\"{BluetoothLeProtocolId}\"";
    private static readonly string[] RequestedProperties =
    [
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.IsPaired",
        "System.Devices.Aep.IsPresent",
        "System.Devices.Aep.ContainerId",
        "System.Devices.Aep.DeviceAddress",
        "System.Devices.BatteryLife",
        BluetoothBatteryLevelProperty
    ];
    // Windows exposes Classic headset battery reported through HFP under this raw DEVPROPKEY.
    private const string BluetoothBatteryLevelProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";
    private static readonly string[] PnpBatteryProperties = ["System.Devices.ContainerId", "System.Devices.BatteryLife", BluetoothBatteryLevelProperty];
    private static readonly string[] ContainerBatteryProperty = ["System.Devices.BatteryLife", BluetoothBatteryLevelProperty];
    private static readonly Guid BatteryServiceUuid = new("0000180F-0000-1000-8000-00805F9B34FB");
    private static readonly Guid BatteryLevelCharacteristicUuid = new("00002A19-0000-1000-8000-00805F9B34FB");
    private const string HandsFreeProfileUuid = "0000111E-0000-1000-8000-00805F9B34FB";
    private readonly FileLogger _logger;
    private readonly object _endpointsLock = new();
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private readonly SemaphoreSlim _reconciliationGate = new(1, 1);
    private readonly SemaphoreSlim _watcherRecoveryGate = new(1, 1);
    private readonly Dictionary<string, DeviceInformation> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BatteryCacheEntry> _containerBatteries = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingBatteryContainers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _removedEndpointIds = new(StringComparer.OrdinalIgnoreCase);
    private DeviceWatcher? _deviceWatcher;
    private System.Threading.Timer? _reconciliationTimer;
    private CancellationTokenSource? _initialDiscoveryCts;
    private Stopwatch? _initialDiscoveryStopwatch;
    private string? _selectedId;
    private string? _selectedName;
    private bool _initialized;
    private bool _initialDiscoveryCompleted;
    private bool _isDisposing;
    private readonly CancellationTokenSource _batteryLifetime = new();
    private readonly SemaphoreSlim _gattGate = new(2, 2);
    private readonly Dictionary<string, long> _batteryGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _batteryFailures = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<BluetoothDeviceInfo>? _lastPublishedDevices;
    private bool? _lastConnected;

    public event EventHandler<BluetoothDeviceInfo>? StateChanged;
    public event EventHandler? DevicesChanged;
    public event EventHandler? InitialDiscoveryCompleted;
    public BluetoothService(FileLogger logger) => _logger = logger;

    public bool IsInitialDiscoveryCompleted
    {
        get
        {
            lock (_endpointsLock) return _initialDiscoveryCompleted;
        }
    }

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> FindDevicesAsync()
    {
        await EnsureInitializedAsync();
        lock (_endpointsLock) return BuildDeviceSnapshot();
    }

    public async Task RefreshNowAsync()
    {
        await EnsureInitializedAsync();
        await ReconcileEndpointsAsync(delay: false, forceBattery: true);
    }

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> FindConnectedDevicesAsync() =>
        (await FindDevicesAsync()).Where(device => device.IsConnected).ToArray();

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> FindBoseDevicesAsync() =>
        (await FindDevicesAsync())
            .Where(device => device.Name.Contains("Bose", StringComparison.OrdinalIgnoreCase) || device.Name.Contains("QuietComfort", StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public async Task<BluetoothDeviceInfo?> SelectAsync(string id)
    {
        _selectedId = id;
        var current = (await FindDevicesAsync()).FirstOrDefault(device => device.Id == id);
        _selectedName = current?.Name ?? _selectedName;
        _lastConnected = current?.IsConnected;
        if (current is not null) await LogSafeAsync("Device selected: " + current.Name);
        return current;
    }

    public async Task<BluetoothDeviceInfo?> CurrentAsync() =>
        string.IsNullOrWhiteSpace(_selectedId)
            ? null
            : (await FindDevicesAsync()).FirstOrDefault(device => device.Id == _selectedId);

    public async Task<bool> RequestConnectionAsync(CancellationToken token)
    {
        if ((await CurrentAsync())?.IsConnected ?? false) return true;

        await LogSafeAsync("Connection requested: Windows exposes no public command to connect Bluetooth Classic audio.");
        await Task.Delay(1500, token);
        return (await CurrentAsync())?.IsConnected ?? false;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initializeGate.WaitAsync();
        try
        {
            if (_initialized) return;

            _initialDiscoveryCts = new CancellationTokenSource();
            _initialDiscoveryStopwatch = Stopwatch.StartNew();
            _initialized = true;
            _ = CompleteInitialDiscoveryAfterTimeoutAsync(_initialDiscoveryCts.Token);
            await LogSafeAsync("Initial Bluetooth watcher start requested.");
            _ = StartWatcherInBackgroundAsync();
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    private async Task StartWatcherInBackgroundAsync()
    {
        try
        {
            await Task.Run(StartWatcher);
            await LogSafeAsync($"Initial Bluetooth watcher start returned after {_initialDiscoveryStopwatch?.ElapsedMilliseconds ?? 0} ms.");
        }
        catch (Exception ex)
        {
            StopWatcher();
            await LogSafeAsync("Initial Bluetooth watcher start error: " + ex);
            CompleteInitialDiscovery("watcher start failed");
            ScheduleEndpointReconciliation();
        }
    }

    private void OnEndpointAdded(DeviceWatcher sender, DeviceInformation endpoint)
    {
        try
        {
            lock (_endpointsLock)
            {
                _removedEndpointIds.Remove(endpoint.Id);
                _endpoints[endpoint.Id] = endpoint;
                InvalidateBatteryGeneration(endpoint);
            }
            if (GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                QueueBatteryHydration([endpoint], force: true);
            PublishChanges();
        }
        catch (Exception ex)
        {
            _ = LogSafeAsync("Bluetooth endpoint-added callback error: " + ex);
        }
    }

    private void OnEndpointUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        try
        {
            DeviceInformation? endpoint = null;
            var becameConnected = false;
            lock (_endpointsLock)
            {
                if (_endpoints.TryGetValue(update.Id, out endpoint))
                {
                    var wasConnected = GetBoolean(endpoint, "System.Devices.Aep.IsConnected");
                    endpoint.Update(update);
                    becameConnected = !wasConnected && GetBoolean(endpoint, "System.Devices.Aep.IsConnected");
                    if (wasConnected != GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                        InvalidateBatteryGeneration(endpoint);
                }
            }
            if (endpoint is not null && GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                QueueBatteryHydration([endpoint], force: becameConnected);
            else if (IsInitialDiscoveryCompleted && endpoint is null)
                ScheduleEndpointReconciliation(delay: true);
            PublishChanges();
        }
        catch (Exception ex)
        {
            _ = LogSafeAsync("Bluetooth endpoint-updated callback error: " + ex);
        }
    }

    private void OnEndpointRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        try
        {
            lock (_endpointsLock)
            {
                var containerId = _endpoints.TryGetValue(update.Id, out var endpoint)
                    ? GetString(endpoint, "System.Devices.Aep.ContainerId")
                    : null;
                _endpoints.Remove(update.Id);
                if (endpoint is not null) InvalidateBatteryGeneration(endpoint);
                _removedEndpointIds.Add(update.Id);
                if (!string.IsNullOrWhiteSpace(containerId)
                    && !_endpoints.Values.Any(current => string.Equals(GetString(current, "System.Devices.Aep.ContainerId"), containerId, StringComparison.OrdinalIgnoreCase)))
                    _containerBatteries.Remove(containerId);
            }
            PublishChanges();
        }
        catch (Exception ex)
        {
            _ = LogSafeAsync("Bluetooth endpoint-removed callback error: " + ex);
        }
    }

    private void ScheduleEndpointReconciliation(bool delay = false) => _ = ReconcileEndpointsAsync(delay, forceBattery: false);

    private async Task CompleteInitialDiscoveryAfterTimeoutAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(InitialDiscoveryTimeout, token);
            CompleteInitialDiscovery("timeout");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CompleteInitialDiscovery(string reason)
    {
        DeviceInformation[] endpoints;
        long elapsedMilliseconds;
        lock (_endpointsLock)
        {
            if (_initialDiscoveryCompleted) return;
            _initialDiscoveryCompleted = true;
            endpoints = _endpoints.Values.ToArray();
            elapsedMilliseconds = _initialDiscoveryStopwatch?.ElapsedMilliseconds ?? 0;
        }

        _initialDiscoveryCts?.Cancel();
        StartReconciliationTimer();
        _ = LogSafeAsync($"Initial Bluetooth discovery ready after {elapsedMilliseconds} ms ({reason}, {endpoints.Length} endpoints cached).");
        QueueBatteryHydration(endpoints);

        try
        {
            InitialDiscoveryCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _ = LogSafeAsync("Initial Bluetooth discovery callback error: " + ex);
        }
    }

    private void StartReconciliationTimer()
    {
        lock (_endpointsLock)
        {
            if (_isDisposing || _reconciliationTimer is not null) return;
            _reconciliationTimer = new System.Threading.Timer(
                _ => ScheduleEndpointReconciliation(),
                null,
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(10));
        }
    }

    private async Task ReconcileEndpointsAsync(bool delay, bool forceBattery)
    {
        if (!await _reconciliationGate.WaitAsync(0)) return;

        try
        {
            if (delay) await Task.Delay(250);
            var endpoints = await DeviceInformation.FindAllAsync(BluetoothSelector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);

            // A transient adapter reset can produce an empty but otherwise successful query. Confirm it
            // before erasing the in-memory inventory that keeps the widget stable during recovery.
            var confirmEmptyResult = false;
            if (endpoints.Count == 0)
            {
                lock (_endpointsLock) confirmEmptyResult = _endpoints.Count > 0;
            }
            if (confirmEmptyResult)
            {
                await LogSafeAsync("Empty Bluetooth endpoint query; confirming before clearing the inventory.");
                await Task.Delay(TimeSpan.FromSeconds(1));
                endpoints = await DeviceInformation.FindAllAsync(BluetoothSelector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
            }

            lock (_endpointsLock)
            {
                var discovered = endpoints.ToDictionary(endpoint => endpoint.Id, StringComparer.OrdinalIgnoreCase);
                foreach (var id in _endpoints.Keys.Where(id => !discovered.ContainsKey(id)).ToArray())
                {
                    InvalidateBatteryGeneration(_endpoints[id]);
                    _endpoints.Remove(id);
                }
                foreach (var endpoint in endpoints.Where(endpoint => !_removedEndpointIds.Contains(endpoint.Id)))
                {
                    if (!_endpoints.TryGetValue(endpoint.Id, out var previous)
                        || GetBoolean(previous, "System.Devices.Aep.IsConnected") != GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                        InvalidateBatteryGeneration(endpoint);
                    _endpoints[endpoint.Id] = endpoint;
                }
            }
            QueueBatteryHydration(endpoints, force: forceBattery);
            PublishChanges();
        }
        catch (Exception ex)
        {
            await LogSafeAsync("Bluetooth endpoint reconciliation error: " + ex);
        }
        finally
        {
            _reconciliationGate.Release();
        }
    }

    private void PublishChanges()
    {
        if (_isDisposing) return;
        IReadOnlyList<BluetoothDeviceInfo> devices;
        lock (_endpointsLock)
        {
            devices = BuildDeviceSnapshot();
            if (_lastPublishedDevices is not null && devices.SequenceEqual(_lastPublishedDevices)) return;
            _lastPublishedDevices = devices;
        }

        var selected = string.IsNullOrWhiteSpace(_selectedId) ? null : devices.FirstOrDefault(device => device.Id == _selectedId);
        var selectedStateChanged = false;
        if (selected is null && !string.IsNullOrWhiteSpace(_selectedId) && _lastConnected != false)
        {
            _lastConnected = false;
            selected = new BluetoothDeviceInfo(_selectedId, _selectedName ?? "Selected Bluetooth device", false, false, null);
            selectedStateChanged = true;
        }
        else if (selected is not null && _lastConnected != selected.IsConnected)
        {
            _lastConnected = selected.IsConnected;
            selectedStateChanged = true;
        }

        if (selected is not null && selectedStateChanged)
        {
            _ = LogSafeAsync(selected.IsConnected ? "Connected" : "Disconnected");
            try
            {
                StateChanged?.Invoke(this, selected);
            }
            catch (Exception ex)
            {
                _ = LogSafeAsync("Bluetooth state-changed callback error: " + ex);
            }
        }

        try
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _ = LogSafeAsync("Bluetooth devices-changed callback error: " + ex);
        }
    }

    private async Task LogSafeAsync(string message)
    {
        try
        {
            await _logger.LogAsync(message);
        }
        catch
        {
            // Logging must not turn a recoverable device event into an unhandled exception.
        }
    }

    private void QueueBatteryHydration(IEnumerable<DeviceInformation> endpoints, bool force = false)
    {
        BatteryCandidate[] candidates;
        lock (_endpointsLock)
        {
            if (_isDisposing) return;
            var now = DateTimeOffset.UtcNow;
            var containerIds = endpoints
                .Where(endpoint => GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                .Select(endpoint => GetString(endpoint, "System.Devices.Aep.ContainerId"))
                .Where(containerId => !string.IsNullOrWhiteSpace(containerId))
                .Select(containerId => containerId!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var pendingCandidates = new List<BatteryCandidate>();
            foreach (var containerId in containerIds)
            {
                var physicalEndpoints = _endpoints.Values
                    .Where(endpoint => string.Equals(GetString(endpoint, "System.Devices.Aep.ContainerId"), containerId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var directBattery = physicalEndpoints.Select(GetBatteryPercent).FirstOrDefault(battery => battery is not null);
                if (directBattery is int battery)
                {
                    _containerBatteries[containerId] = new BatteryCacheEntry(battery, now, now, "endpoint");
                    continue;
                }

                if (!force && _containerBatteries.TryGetValue(containerId, out var cachedBattery))
                {
                    var retryDelay = cachedBattery.Value is null ? GetBatteryRetryDelay(containerId) : BatteryRefreshInterval;
                    if (now - cachedBattery.LastAttemptUtc < retryDelay) continue;
                }
                if (!_pendingBatteryContainers.Add(containerId)) continue;

                var endpointCandidates = physicalEndpoints
                    .OrderByDescending(endpoint => endpoint.Id.StartsWith("BluetoothLE", StringComparison.OrdinalIgnoreCase))
                    .Select(endpoint => new BatteryEndpointCandidate(endpoint.Id, GetBluetoothAddress(endpoint)))
                    .DistinctBy(candidate => candidate.BluetoothAddress?.ToString("X12") ?? candidate.EndpointId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                pendingCandidates.Add(new BatteryCandidate(containerId, endpointCandidates, _batteryGenerations.GetValueOrDefault(containerId)));
            }

            candidates = pendingCandidates.ToArray();
        }

        foreach (var candidate in candidates) _ = Task.Run(() => HydrateBatteryAsync(candidate));
    }

    // Caller holds _endpointsLock. Results from a previous connection must not replace new data.
    private void InvalidateBatteryGeneration(DeviceInformation endpoint)
    {
        var containerId = GetString(endpoint, "System.Devices.Aep.ContainerId");
        if (containerId is not null)
        {
            _batteryGenerations[containerId] = _batteryGenerations.GetValueOrDefault(containerId) + 1;
            _batteryFailures.Remove(containerId);
        }
    }

    private TimeSpan GetBatteryRetryDelay(string containerId) => _batteryFailures.GetValueOrDefault(containerId) switch
    {
        <= 1 => TimeSpan.FromSeconds(3),
        2 => TimeSpan.FromSeconds(10),
        _ => BatteryUnavailableRetryDelay
    };

    private async Task RetryBatteryAsync(BatteryCandidate candidate, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _batteryLifetime.Token);
            DeviceInformation[] endpoints;
            lock (_endpointsLock)
            {
                if (!IsBatteryCandidateCurrent(candidate)) return;
                endpoints = _endpoints.Values.Where(endpoint => string.Equals(
                    GetString(endpoint, "System.Devices.Aep.ContainerId"), candidate.ContainerId, StringComparison.OrdinalIgnoreCase)).ToArray();
            }
            QueueBatteryHydration(endpoints);
        }
        catch (OperationCanceledException) { }
    }

    private bool IsBatteryCandidateCurrent(BatteryCandidate candidate) =>
        !_isDisposing && _batteryGenerations.GetValueOrDefault(candidate.ContainerId) == candidate.Generation
        && _endpoints.Values.Any(endpoint =>
            string.Equals(GetString(endpoint, "System.Devices.Aep.ContainerId"), candidate.ContainerId, StringComparison.OrdinalIgnoreCase)
            && GetBoolean(endpoint, "System.Devices.Aep.IsConnected"));

    private async Task HydrateBatteryAsync(BatteryCandidate candidate)
    {
        var stopwatch = Stopwatch.StartNew();
        var found = false;
        try
        {
            using var queryCts = CancellationTokenSource.CreateLinkedTokenSource(_batteryLifetime.Token);
            await BatteryQueryRunner.RunAsync(
                new Func<CancellationToken, Task<int?>>[]
                {
                    async token => (await ReadPnpContainerBatteryAsync(candidate)).Value
                }.Concat(candidate.Endpoints.Select(endpoint =>
                    (Func<CancellationToken, Task<int?>>)(async token => (await ReadGattLimitedAsync(endpoint, token)).Value))),
                battery =>
                {
                    lock (_endpointsLock)
                    {
                        if (!IsBatteryCandidateCurrent(candidate)) return;
                        var now = DateTimeOffset.UtcNow;
                        _containerBatteries[candidate.ContainerId] = new BatteryCacheEntry(battery, now, now, "PnP/GATT");
                        _batteryFailures.Remove(candidate.ContainerId);
                        found = true;
                    }
                    PublishChanges();
                    _ = LogSafeAsync($"Batería disponible en {stopwatch.ElapsedMilliseconds} ms (PnP/GATT).");
                }, queryCts);
            if (!found)
            {
                lock (_endpointsLock)
                {
                    if (!IsBatteryCandidateCurrent(candidate)) return;
                    _containerBatteries.TryGetValue(candidate.ContainerId, out var previous);
                    _containerBatteries[candidate.ContainerId] = new BatteryCacheEntry(previous?.Value, DateTimeOffset.UtcNow, previous?.LastSuccessUtc, previous?.Source);
                    _batteryFailures[candidate.ContainerId] = Math.Min(3, _batteryFailures.GetValueOrDefault(candidate.ContainerId) + 1);
                }
            }
        }
        catch (Exception ex)
        {
            await LogSafeAsync("Bluetooth battery hydration error: " + ex);
        }
        finally
        {
            DeviceInformation[] retryEndpoints;
            TimeSpan? retryDelay = null;
            lock (_endpointsLock)
            {
                _pendingBatteryContainers.Remove(candidate.ContainerId);
                if (!found && IsBatteryCandidateCurrent(candidate))
                    retryDelay = _containerBatteries.GetValueOrDefault(candidate.ContainerId)?.Value is null
                        ? GetBatteryRetryDelay(candidate.ContainerId) : BatteryRefreshInterval;
                retryEndpoints = !_isDisposing && _batteryGenerations.GetValueOrDefault(candidate.ContainerId) != candidate.Generation
                    ? _endpoints.Values.Where(endpoint => string.Equals(GetString(endpoint, "System.Devices.Aep.ContainerId"), candidate.ContainerId, StringComparison.OrdinalIgnoreCase)).ToArray()
                    : [];
            }
            if (retryEndpoints.Length > 0) QueueBatteryHydration(retryEndpoints, force: true);
            else if (retryDelay is TimeSpan delay) _ = RetryBatteryAsync(candidate, delay);
            if (!found && stopwatch.Elapsed >= BatteryQueryTimeout)
                await LogSafeAsync($"Consulta de batería finalizada sin dato vigente en {stopwatch.ElapsedMilliseconds} ms.");
        }
    }

    private async Task<BatteryReadResult> ReadGattLimitedAsync(BatteryEndpointCandidate candidate, CancellationToken token)
    {
        await _gattGate.WaitAsync(token);
        try { return await ReadGattBatteryAsync(candidate, token); }
        finally { _gattGate.Release(); }
    }

    private IReadOnlyList<BluetoothDeviceInfo> BuildDeviceSnapshot() =>
        _endpoints.Values
            .Select(endpoint => new
            {
                Device = ToBluetoothDevice(endpoint),
                PhysicalDeviceKey = GetString(endpoint, "System.Devices.Aep.ContainerId")
                    ?? GetString(endpoint, "System.Devices.Aep.DeviceAddress")
                    ?? endpoint.Id
            })
            .Where(item => (item.Device.IsPaired || item.Device.IsConnected) && !string.IsNullOrWhiteSpace(item.Device.Name))
            .GroupBy(item => item.PhysicalDeviceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => string.Equals(item.Device.Id, _selectedId, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(item => item.Device.IsConnected)
                .ThenByDescending(item => item.Device.BatteryPercent.HasValue)
                .ThenByDescending(item => item.Device.Name.Length)
                .First()
                .Device)
            .OrderByDescending(device => device.IsConnected)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    private void StartWatcher()
    {
        var watcher = DeviceInformation.CreateWatcher(BluetoothSelector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
        watcher.Added += OnEndpointAdded;
        watcher.Updated += OnEndpointUpdated;
        watcher.Removed += OnEndpointRemoved;
        watcher.EnumerationCompleted += OnEnumerationCompleted;
        watcher.Stopped += OnWatcherStopped;
        _deviceWatcher = watcher;
        watcher.Start();
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        if (_isDisposing || !ReferenceEquals(sender, _deviceWatcher)) return;

        int endpointCount;
        long elapsedMilliseconds;
        lock (_endpointsLock)
        {
            endpointCount = _endpoints.Count;
            elapsedMilliseconds = _initialDiscoveryStopwatch?.ElapsedMilliseconds ?? 0;
        }

        _ = LogSafeAsync($"Bluetooth watcher enumeration completed in {elapsedMilliseconds} ms ({endpointCount} endpoints cached).");
        StartReconciliationTimer();
        CompleteInitialDiscovery("watcher enumeration completed");
    }

    private void OnWatcherStopped(DeviceWatcher sender, object args)
    {
        if (_isDisposing || !ReferenceEquals(sender, _deviceWatcher)) return;
        var status = sender.Status;
        if (status is not (DeviceWatcherStatus.Aborted or DeviceWatcherStatus.Stopped)) return;

        _ = LogSafeAsync("Bluetooth watcher status: " + status);
        _ = RestartWatcherAsync();
    }

    private async Task RestartWatcherAsync()
    {
        if (!await _watcherRecoveryGate.WaitAsync(0)) return;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            if (_isDisposing) return;

            StopWatcher();
            StartWatcher();
            await LogSafeAsync("Bluetooth watcher restarted.");
            if (IsInitialDiscoveryCompleted)
                await ReconcileEndpointsAsync(delay: false, forceBattery: false);
        }
        catch (Exception ex)
        {
            await LogSafeAsync("Bluetooth watcher restart error: " + ex);
        }
        finally
        {
            _watcherRecoveryGate.Release();
        }
    }

    private void StopWatcher()
    {
        var watcher = _deviceWatcher;
        _deviceWatcher = null;
        if (watcher is null) return;

        watcher.Added -= OnEndpointAdded;
        watcher.Updated -= OnEndpointUpdated;
        watcher.Removed -= OnEndpointRemoved;
        watcher.EnumerationCompleted -= OnEnumerationCompleted;
        watcher.Stopped -= OnWatcherStopped;
        try { watcher.Stop(); } catch { }
    }

    private BluetoothDeviceInfo ToBluetoothDevice(DeviceInformation endpoint)
    {
        var containerId = GetString(endpoint, "System.Devices.Aep.ContainerId");
        var battery = GetBatteryPercent(endpoint);
        if (battery is null
            && containerId is not null
            && _containerBatteries.TryGetValue(containerId, out var containerBattery)
            && containerBattery.Value is int cachedBattery
            && containerBattery.LastSuccessUtc is DateTimeOffset lastSuccess
            && DateTimeOffset.UtcNow - lastSuccess <= BatteryValueExpiration)
            battery = cachedBattery;

        return new BluetoothDeviceInfo(endpoint.Id, endpoint.Name, GetBoolean(endpoint, "System.Devices.Aep.IsPaired"), GetBoolean(endpoint, "System.Devices.Aep.IsConnected"), battery);
    }

    private static async Task<BatteryReadResult> ReadGattBatteryAsync(BatteryEndpointCandidate candidate, CancellationToken token)
    {
        BluetoothLEDevice? device = null;
        try
        {
            // The container property has already been queried above. When Windows does not expose it,
            // ask only the standard BLE Battery Service on this already-known device; no device scan occurs.
            token.ThrowIfCancellationRequested();
            // AssociationEndpoint identifiers do not have a stable textual prefix across Windows versions.
            // Try the known endpoint directly regardless of its spelling, then fall back to its address.
            try { device = await BluetoothLEDevice.FromIdAsync(candidate.EndpointId); }
            catch { /* This endpoint ID may represent Classic or an unsupported association node. */ }
            if (device is null && candidate.BluetoothAddress is ulong address)
                device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);

            if (device is null) return new BatteryReadResult(null, "GATT");

            foreach (var cacheMode in new[] { BluetoothCacheMode.Cached, BluetoothCacheMode.Uncached })
            {
                token.ThrowIfCancellationRequested();
                var servicesResult = await device.GetGattServicesForUuidAsync(BatteryServiceUuid, cacheMode);
                if (servicesResult.Status != GattCommunicationStatus.Success) continue;

                foreach (var service in servicesResult.Services)
                {
                    try
                    {
                        var characteristicsResult = await service.GetCharacteristicsForUuidAsync(BatteryLevelCharacteristicUuid, cacheMode);
                        if (characteristicsResult.Status != GattCommunicationStatus.Success) continue;

                        var characteristic = characteristicsResult.Characteristics.FirstOrDefault();
                        if (characteristic is null) continue;

                        token.ThrowIfCancellationRequested();
                        var valueResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                        if (valueResult.Status != GattCommunicationStatus.Success || valueResult.Value.Length < 1) continue;

                        using var reader = DataReader.FromBuffer(valueResult.Value);
                        var battery = reader.ReadByte();
                        if (battery <= 100) return new BatteryReadResult(battery, "GATT");
                    }
                    finally
                    {
                        service.Dispose();
                    }
                }
            }

            return new BatteryReadResult(null, "GATT");
        }
        catch
        {
            return new BatteryReadResult(null, "GATT");
        }
        finally
        {
            device?.Dispose();
        }
    }

    private static async Task<BatteryReadResult> ReadPnpContainerBatteryAsync(BatteryCandidate candidate)
    {
        // Query the native PnP property first. This is where Windows stores the
        // HFP Battery Level for Classic headsets such as Bose QC Ultra 2 HP.
        var nativeBattery = BluetoothPnP.TryGetBatteryForContainer(candidate.ContainerId);
        if (nativeBattery is int nativeLevel) return new BatteryReadResult(nativeLevel, "PnP HFP native");

        try
        {
            var container = await DeviceInformation.CreateFromIdAsync(candidate.ContainerId, ContainerBatteryProperty, DeviceInformationKind.DeviceContainer);
            var containerBattery = GetBatteryPercent(container);
            if (containerBattery is not null) return new BatteryReadResult(containerBattery, "PnP container");
        }
        catch
        {
            // Some drivers expose the container identity but do not allow direct container access.
        }

        try
        {
            var escapedContainerId = candidate.ContainerId.Replace("\"", "\"\"", StringComparison.Ordinal);
            var containerSelector = $"System.Devices.ContainerId:=\"{escapedContainerId}\"";
            var containerDevices = await DeviceInformation.FindAllAsync(containerSelector, PnpBatteryProperties, DeviceInformationKind.Device);
            foreach (var device in containerDevices)
            {
                var battery = GetBatteryPercent(device);
                if (battery is not null) return new BatteryReadResult(battery, "PnP device");
            }
        }
        catch
        {
            // Continue with address-based PnP nodes when the container selector is unsupported.
        }

        // Windows reports Classic headset battery through the HFP Hands-Free AG
        // device node. It is a BTHENUM service node, not the base DEV_<address>
        // endpoint, so query that service explicitly and join it by ContainerId.
        try
        {
            var hfpSelector = $"System.Devices.DeviceInstanceId:~~\"BTHENUM\\\\{{{HandsFreeProfileUuid}}}*\"";
            var hfpDevices = await DeviceInformation.FindAllAsync(hfpSelector, PnpBatteryProperties, DeviceInformationKind.Device);
            foreach (var device in hfpDevices)
            {
                var deviceContainer = GetString(device, "System.Devices.ContainerId");
                if (!string.Equals(deviceContainer, candidate.ContainerId, StringComparison.OrdinalIgnoreCase)) continue;
                var battery = GetBatteryPercent(device);
                if (battery is not null) return new BatteryReadResult(battery, "PnP HFP");
            }
        }
        catch
        {
            // Continue with address-based PnP nodes and GATT fallback.
        }

        foreach (var address in candidate.Endpoints
                     .Where(endpoint => endpoint.BluetoothAddress.HasValue)
                     .Select(endpoint => endpoint.BluetoothAddress!.Value)
                     .Distinct())
        {
            try
            {
                var addressText = address.ToString("X12", CultureInfo.InvariantCulture);
                // HFP/AVRCP nodes use BTHENUM\\{service-guid}...<address>, while the base
                // endpoint uses BTHENUM\\DEV_<address>. Match every node for this physical address.
                var selector = $"System.Devices.DeviceInstanceId:~~\"BTHLE\\\\*{addressText}*\" OR System.Devices.DeviceInstanceId:~~\"BTHENUM\\\\*{addressText}*\"";
                var devices = await DeviceInformation.FindAllAsync(selector, PnpBatteryProperties, DeviceInformationKind.Device);

                foreach (var device in devices)
                {
                    var directBattery = GetBatteryPercent(device);
                    if (directBattery is not null) return new BatteryReadResult(directBattery, "PnP device");

                    var containerId = GetString(device, "System.Devices.ContainerId");
                    if (string.IsNullOrWhiteSpace(containerId)) continue;

                    var relatedContainer = await DeviceInformation.CreateFromIdAsync(containerId, ContainerBatteryProperty, DeviceInformationKind.DeviceContainer);
                    var relatedBattery = GetBatteryPercent(relatedContainer);
                    if (relatedBattery is not null) return new BatteryReadResult(relatedBattery, "PnP container");
                }
            }
            catch
            {
                // Continue with other addresses; the GATT fallback remains available in parallel.
            }
        }

        return new BatteryReadResult(null, "PnP");
    }

    private static bool GetBoolean(DeviceInformation endpoint, string propertyName) =>
        endpoint.Properties.TryGetValue(propertyName, out var value) && value is bool flag && flag;

    private static string? GetString(DeviceInformation endpoint, string propertyName) =>
        endpoint.Properties.TryGetValue(propertyName, out var value) && value is not null ? Convert.ToString(value, CultureInfo.InvariantCulture) : null;

    private static ulong? GetBluetoothAddress(DeviceInformation endpoint)
    {
        var address = GetString(endpoint, "System.Devices.Aep.DeviceAddress");
        if (string.IsNullOrWhiteSpace(address)) return null;

        var normalized = address.Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);
        return ulong.TryParse(normalized, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static int? GetBatteryPercent(DeviceInformation endpoint)
    {
        if (!endpoint.Properties.TryGetValue("System.Devices.BatteryLife", out var value) || value is null)
            endpoint.Properties.TryGetValue(BluetoothBatteryLevelProperty, out value);
        if (value is null) return null;
        if (value is byte byteBattery) return byteBattery <= 100 ? byteBattery : null;
        return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var battery) && battery is >= 0 and <= 100
            ? battery
            : null;
    }

    public void Dispose()
    {
        _isDisposing = true;
        _batteryLifetime.Cancel();
        _initialDiscoveryCts?.Cancel();
        _initialDiscoveryCts?.Dispose();
        _reconciliationTimer?.Dispose();
        StopWatcher();
        _initializeGate.Dispose();
    }

    private sealed record BatteryCacheEntry(int? Value, DateTimeOffset LastAttemptUtc, DateTimeOffset? LastSuccessUtc, string? Source);
    private sealed record BatteryEndpointCandidate(string EndpointId, ulong? BluetoothAddress);
    private sealed record BatteryCandidate(string ContainerId, IReadOnlyList<BatteryEndpointCandidate> Endpoints, long Generation);
    private sealed record BatteryReadResult(int? Value, string Source);
}
