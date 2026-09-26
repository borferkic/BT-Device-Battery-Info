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
    private static readonly TimeSpan GamingInputFallbackDelay = TimeSpan.FromMilliseconds(1500);
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
        "System.Devices.Aep.ProtocolId",
        "System.Devices.Aep.Category",
        "System.Devices.Aep.Bluetooth.Cod.Major",
        "System.Devices.Aep.Bluetooth.Cod.Minor",
        "System.Devices.Aep.Bluetooth.Le.Appearance",
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
    private string? _selectedEndpointId;
    private string? _selectedDeviceId;
    private string? _selectedName;
    private bool _initialized;
    private bool _initialDiscoveryCompleted;
    // Windows needs about 30 s for the first association-endpoint enumeration; queries issued before it
    // completes wait for it, so manual refreshes skip the query until then.
    private volatile bool _watcherEnumerationCompleted;
    private volatile bool _isDisposing;
    private int _watcherRestartFailures;
    private readonly CancellationTokenSource _batteryLifetime = new();
    private readonly SemaphoreSlim _gattGate = new(2, 2);
    private readonly Dictionary<string, long> _batteryGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _batteryFailures = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<BluetoothDeviceInfo>? _lastPublishedDevices;
    private bool? _lastConnected;
    private readonly Dictionary<string, bool> _lastDeviceConnections = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _classicProtocolDeviceIds = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<BluetoothDeviceInfo>? StateChanged;
    public event EventHandler? DevicesChanged;
    public event EventHandler? InitialDiscoveryCompleted;
    public BluetoothService(FileLogger logger)
    {
        _logger = logger;
        GamingInputBattery.Initialize();
    }

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
        if (_watcherEnumerationCompleted)
        {
            await ReconcileEndpointsAsync(delay: false, forceBattery: true);
            return;
        }

        // The watcher is still enumerating and keeps delivering connection changes; refresh batteries only.
        DeviceInformation[] endpoints;
        lock (_endpointsLock) endpoints = _endpoints.Values.ToArray();
        QueueBatteryHydration(endpoints, force: true);
    }

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> FindConnectedDevicesAsync() =>
        (await FindDevicesAsync()).Where(device => device.IsConnected).ToArray();

    public async Task<BluetoothDeviceInfo?> SelectAsync(string id)
    {
        await EnsureInitializedAsync();
        BluetoothDeviceInfo? current;
        lock (_endpointsLock)
        {
            var endpoint = _endpoints.Values.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(GetPhysicalDeviceId(candidate), id, StringComparison.OrdinalIgnoreCase));
            if (endpoint is null) return null;

            _selectedEndpointId = endpoint.Id;
            _selectedDeviceId = GetPhysicalDeviceId(endpoint);
            current = BuildDeviceSnapshot().FirstOrDefault(device =>
                string.Equals(device.PhysicalDeviceId, _selectedDeviceId, StringComparison.OrdinalIgnoreCase));
            _selectedName = current?.Name ?? _selectedName;
            _lastConnected = current?.IsConnected;
        }
        if (current is not null) await LogSafeAsync("Device selected: " + current.Name);
        return current;
    }

    public async Task<BluetoothDeviceInfo?> CurrentAsync() =>
        string.IsNullOrWhiteSpace(_selectedDeviceId)
            ? null
            : (await FindDevicesAsync()).FirstOrDefault(device =>
                string.Equals(device.PhysicalDeviceId, _selectedDeviceId, StringComparison.OrdinalIgnoreCase));

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
        if (_isDisposing) return;
        try
        {
            lock (_endpointsLock)
            {
                _removedEndpointIds.Remove(endpoint.Id);
                _endpoints[endpoint.Id] = endpoint;
                RememberClassicProtocol(endpoint);
                InvalidateBatteryGeneration(endpoint);
            }
            if (GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                QueueBatteryHydration([endpoint], force: true);
            PublishChanges("watcher added event");
        }
        catch (Exception ex)
        {
            _ = LogSafeAsync("Bluetooth endpoint-added callback error: " + ex);
        }
    }

    private void OnEndpointUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_isDisposing) return;
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
                    RememberClassicProtocol(endpoint);
                    becameConnected = !wasConnected && GetBoolean(endpoint, "System.Devices.Aep.IsConnected");
                    if (wasConnected != GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                    {
                        InvalidateBatteryGeneration(endpoint);
                        ClearBatteryIfNoConnectedEndpointsLocked(GetString(endpoint, "System.Devices.Aep.ContainerId"));
                    }
                }
            }
            if (endpoint is not null && GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                QueueBatteryHydration([endpoint], force: becameConnected);
            else if (IsInitialDiscoveryCompleted && endpoint is null)
                ScheduleEndpointReconciliation(delay: true);
            PublishChanges("watcher update event");
        }
        catch (Exception ex)
        {
            _ = LogSafeAsync("Bluetooth endpoint-updated callback error: " + ex);
        }
    }

    private void OnEndpointRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (_isDisposing) return;
        try
        {
            lock (_endpointsLock)
            {
                var containerId = _endpoints.TryGetValue(update.Id, out var endpoint)
                    ? GetString(endpoint, "System.Devices.Aep.ContainerId")
                    : null;
                if (endpoint is not null) RememberClassicProtocol(endpoint);
                _endpoints.Remove(update.Id);
                if (endpoint is not null) InvalidateBatteryGeneration(endpoint);
                _removedEndpointIds.Add(update.Id);
                ClearBatteryIfNoConnectedEndpointsLocked(containerId);
            }
            PublishChanges("watcher removed event");
        }
        catch (Exception ex)
        {
            _ = LogSafeAsync("Bluetooth endpoint-removed callback error: " + ex);
        }
    }

    private void ScheduleEndpointReconciliation(bool delay = false)
    {
        if (!_isDisposing) _ = ReconcileEndpointsAsync(delay, forceBattery: false);
    }

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
            if (_isDisposing) return;
            if (confirmEmptyResult)
            {
                await LogSafeAsync("Empty Bluetooth endpoint query; confirming before clearing the inventory.");
                await Task.Delay(TimeSpan.FromSeconds(1));
                endpoints = await DeviceInformation.FindAllAsync(BluetoothSelector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
            }

            var corrected = 0;
            var restored = 0;
            lock (_endpointsLock)
            {
                var discovered = endpoints.ToDictionary(endpoint => endpoint.Id, StringComparer.OrdinalIgnoreCase);
                foreach (var id in _endpoints.Keys.Where(id => !discovered.ContainsKey(id)).ToArray())
                {
                    InvalidateBatteryGeneration(_endpoints[id]);
                    _endpoints.Remove(id);
                }
                // An endpoint removed by the watcher that Windows reports again (for example after the radio
                // is turned back on without an Added event) is present again; stop ignoring it.
                foreach (var endpoint in endpoints.Where(endpoint => _removedEndpointIds.Contains(endpoint.Id)).ToArray())
                {
                    _removedEndpointIds.Remove(endpoint.Id);
                    restored++;
                }
                foreach (var endpoint in endpoints)
                {
                    if (!_endpoints.TryGetValue(endpoint.Id, out var previous)
                        || GetBoolean(previous, "System.Devices.Aep.IsConnected") != GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
                    {
                        if (previous is not null) corrected++;
                        InvalidateBatteryGeneration(endpoint);
                    }
                    _endpoints[endpoint.Id] = endpoint;
                }
                foreach (var endpoint in _endpoints.Values) RememberClassicProtocol(endpoint);
            }
            if (corrected > 0 || restored > 0)
                await LogSafeAsync($"Endpoint reconciliation corrected {corrected} connection state(s) and restored {restored} removed endpoint(s) missed by the watcher.");
            QueueBatteryHydration(endpoints, force: forceBattery);
            PublishChanges("reconciliation query");
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

    private void PublishChanges(string source = "state refresh")
    {
        if (_isDisposing) return;
        IReadOnlyList<BluetoothDeviceInfo> devices;
        lock (_endpointsLock)
        {
            devices = BuildDeviceSnapshot();
            if (_lastPublishedDevices is not null && devices.SequenceEqual(_lastPublishedDevices)) return;
            _lastPublishedDevices = devices;
        }
        LogDeviceTransitions(devices, source);

        var selected = string.IsNullOrWhiteSpace(_selectedDeviceId) ? null : devices.FirstOrDefault(device =>
            string.Equals(device.PhysicalDeviceId, _selectedDeviceId, StringComparison.OrdinalIgnoreCase));
        var selectedStateChanged = false;
        if (selected is null && !string.IsNullOrWhiteSpace(_selectedDeviceId) && _lastConnected != false)
        {
            _lastConnected = false;
            selected = new BluetoothDeviceInfo(_selectedEndpointId ?? _selectedDeviceId,
                _selectedName ?? "Selected Bluetooth device", false, false, null, _selectedDeviceId);
            selectedStateChanged = true;
        }
        else if (selected is not null && _lastConnected != selected.IsConnected)
        {
            _lastConnected = selected.IsConnected;
            selectedStateChanged = true;
        }

        if (selected is not null && selectedStateChanged)
        {
            _ = LogSafeAsync($"{(selected.IsConnected ? "Connected" : "Disconnected")} ({GetConnectionEvidence(selected.PhysicalDeviceId)}).");
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

    // Caller holds _endpointsLock. A paired endpoint may remain after its audio
    // profiles disconnect; its old percentage must never describe a disconnected device.
    private void ClearBatteryIfNoConnectedEndpointsLocked(string? containerId)
    {
        if (string.IsNullOrWhiteSpace(containerId)) return;
        var hasConnectedEndpoint = _endpoints.Values.Any(current =>
            string.Equals(GetString(current, "System.Devices.Aep.ContainerId"), containerId, StringComparison.OrdinalIgnoreCase)
            && GetBoolean(current, "System.Devices.Aep.IsConnected"));
        if (hasConnectedEndpoint) return;

        _containerBatteries.Remove(containerId);
        _batteryFailures.Remove(containerId);
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
            await BatteryQueryRunner.RunWithSourceAsync(
                new Func<CancellationToken, Task<BatteryQueryValue>>[]
                {
                    async token => ToBatteryQueryValue(await ReadPnpContainerBatteryAsync(candidate)),
                    async token =>
                    {
                        // Lowest-priority fallback: give PnP and GATT time to answer first.
                        await Task.Delay(GamingInputFallbackDelay, token);
                        return await GamingInputBattery.ReadAsync(candidate.ContainerId, token);
                    }
                }.Concat(candidate.Endpoints.Select(endpoint =>
                    (Func<CancellationToken, Task<BatteryQueryValue>>)(async token => ToBatteryQueryValue(await ReadGattLimitedAsync(endpoint, token))))),
                result =>
                {
                    lock (_endpointsLock)
                    {
                        if (!IsBatteryCandidateCurrent(candidate)) return;
                        var now = DateTimeOffset.UtcNow;
                        _containerBatteries[candidate.ContainerId] = new BatteryCacheEntry(result.Value, now, now, result.Source);
                        _batteryFailures.Remove(candidate.ContainerId);
                        found = true;
                    }
                    PublishChanges();
                    _ = LogSafeAsync($"Battery available in {stopwatch.ElapsedMilliseconds} ms ({result.Source}).");
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
                await LogSafeAsync($"Battery query completed without a current value in {stopwatch.ElapsedMilliseconds} ms.");
        }
    }

    private async Task<BatteryReadResult> ReadGattLimitedAsync(BatteryEndpointCandidate candidate, CancellationToken token)
    {
        await _gattGate.WaitAsync(token);
        try { return await ReadGattBatteryAsync(candidate, token); }
        finally { _gattGate.Release(); }
    }

    private static BatteryQueryValue ToBatteryQueryValue(BatteryReadResult result) => new(result.Value, result.Source);

    private IReadOnlyList<BluetoothDeviceInfo> BuildDeviceSnapshot() =>
        _endpoints.Values
            .GroupBy(GetPhysicalDeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildPhysicalDeviceSnapshot(group.Key, group.ToArray()))
            .Where(device => (device.IsPaired || device.IsConnected) && !string.IsNullOrWhiteSpace(device.Name))
            .OrderByDescending(device => device.IsConnected)
            .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    private BluetoothDeviceInfo BuildPhysicalDeviceSnapshot(string physicalDeviceId, DeviceInformation[] endpoints)
    {
        var endpointSnapshots = endpoints
            .Select(endpoint => new DeviceEndpointSnapshot(endpoint, ToBluetoothDevice(endpoint)))
            .ToArray();
        var representative = endpointSnapshots
            .OrderByDescending(item => string.Equals(item.Endpoint.Id, _selectedEndpointId, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.Device.BatteryPercent.HasValue)
            .ThenByDescending(item => item.Device.Name.Length)
            .First();
        var isConnected = ResolvePhysicalConnection(physicalDeviceId, endpoints);
        var batterySource = isConnected
            ? endpointSnapshots.Select(item => item.Device).FirstOrDefault(device => device.BatteryPercent.HasValue)
            : null;

        return new BluetoothDeviceInfo(
            representative.Device.Id,
            representative.Device.Name,
            endpointSnapshots.Any(item => item.Device.IsPaired),
            isConnected,
            batterySource?.BatteryPercent,
            physicalDeviceId,
            endpointSnapshots.Select(item => item.Device.Category).FirstOrDefault(category => category != BluetoothDeviceCategory.Unknown),
            batterySource?.BatteryLevel);
    }

    private bool ResolvePhysicalConnection(string physicalDeviceId, DeviceInformation[] endpoints)
    {
        // AEP ProtocolId identifies the discovery transport, not an audio profile.
        // Prefer Classic when the physical container exposes both transports so an
        // auxiliary BLE endpoint cannot keep a disconnected Classic device connected.
        // Remember Classic while sibling endpoints remain, even if its endpoint is removed.
        var classicEndpoints = endpoints.Where(endpoint => IsEndpointUsingProtocol(endpoint, BluetoothClassicProtocolId)).ToArray();
        if (classicEndpoints.Length > 0 || _classicProtocolDeviceIds.Contains(physicalDeviceId))
            return classicEndpoints.Any(endpoint => GetBoolean(endpoint, "System.Devices.Aep.IsConnected"));

        var leEndpoints = endpoints.Where(endpoint => IsEndpointUsingProtocol(endpoint, BluetoothLeProtocolId)).ToArray();
        if (leEndpoints.Length > 0)
            return leEndpoints.Any(endpoint => GetBoolean(endpoint, "System.Devices.Aep.IsConnected"));

        return endpoints.Any(endpoint => GetBoolean(endpoint, "System.Devices.Aep.IsConnected"));
    }

    /// <summary>Logs every physical device whose grouped connection state changed, with Classic/BLE evidence (P-014 QA).</summary>
    private void LogDeviceTransitions(IReadOnlyList<BluetoothDeviceInfo> devices, string source)
    {
        List<string> transitions = [];
        lock (_endpointsLock)
        {
            foreach (var device in devices)
            {
                var known = _lastDeviceConnections.TryGetValue(device.PhysicalDeviceId, out var wasConnected);
                _lastDeviceConnections[device.PhysicalDeviceId] = device.IsConnected;
                // Devices discovered during the initial scan are not transitions; later appearances are.
                if (known ? wasConnected != device.IsConnected : _initialDiscoveryCompleted && device.IsConnected)
                    transitions.Add($"{device.Name}: {(device.IsConnected ? "connected" : "disconnected")}|{device.PhysicalDeviceId}");
            }
            var present = devices.Select(device => device.PhysicalDeviceId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var removed in _lastDeviceConnections.Keys.Where(id => !present.Contains(id)).ToArray())
            {
                if (_lastDeviceConnections[removed]) transitions.Add($"Device {removed[..Math.Min(8, removed.Length)]}…: removed while connected|{removed}");
                _lastDeviceConnections.Remove(removed);
            }
        }
        foreach (var transition in transitions)
        {
            var separator = transition.LastIndexOf('|');
            _ = LogSafeAsync($"Device transition — {transition[..separator]} (reported by Windows via {source}; {GetConnectionEvidence(transition[(separator + 1)..])}).");
        }
    }

    private string GetConnectionEvidence(string physicalDeviceId)
    {
        DeviceInformation[] endpoints;
        lock (_endpointsLock)
        {
            endpoints = _endpoints.Values
                .Where(endpoint => string.Equals(GetPhysicalDeviceId(endpoint), physicalDeviceId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        static string CountConnected(IEnumerable<DeviceInformation> protocolEndpoints)
        {
            var matching = protocolEndpoints.ToArray();
            return $"{matching.Count(endpoint => GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))}/{matching.Length}";
        }

        return $"Classic endpoints connected: {CountConnected(endpoints.Where(endpoint => IsEndpointUsingProtocol(endpoint, BluetoothClassicProtocolId)))}; "
            + $"BLE endpoints connected: {CountConnected(endpoints.Where(endpoint => IsEndpointUsingProtocol(endpoint, BluetoothLeProtocolId)))}";
    }

    private static bool IsEndpointUsingProtocol(DeviceInformation endpoint, string protocolId) =>
        Guid.TryParse(GetString(endpoint, "System.Devices.Aep.ProtocolId"), out var actualProtocol)
        && Guid.TryParse(protocolId, out var expectedProtocol)
        && actualProtocol == expectedProtocol;

    private static string GetPhysicalDeviceId(DeviceInformation endpoint) =>
        GetString(endpoint, "System.Devices.Aep.ContainerId")
        ?? GetString(endpoint, "System.Devices.Aep.DeviceAddress")
        ?? endpoint.Id;

    private void RememberClassicProtocol(DeviceInformation endpoint)
    {
        if (IsEndpointUsingProtocol(endpoint, BluetoothClassicProtocolId))
            _classicProtocolDeviceIds.Add(GetPhysicalDeviceId(endpoint));
    }

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

        _watcherEnumerationCompleted = true;
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

    /// <summary>Delay before watcher restart attempt <paramref name="failures"/> + 1: 1 s, 5 s, 15 s, then every 60 s.</summary>
    internal static TimeSpan GetWatcherRestartDelay(int failures) => failures switch
    {
        <= 0 => TimeSpan.FromSeconds(1),
        1 => TimeSpan.FromSeconds(5),
        2 => TimeSpan.FromSeconds(15),
        _ => TimeSpan.FromSeconds(60)
    };

    private async Task RestartWatcherAsync()
    {
        if (!await _watcherRecoveryGate.WaitAsync(0)) return;

        try
        {
            // Keep retrying with backoff: a failed restart must not leave the service without a watcher.
            while (!_isDisposing)
            {
                var delay = GetWatcherRestartDelay(_watcherRestartFailures);
                try
                {
                    await Task.Delay(delay, _batteryLifetime.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                if (_isDisposing) return;

                try
                {
                    StopWatcher();
                    StartWatcher();
                    await LogSafeAsync(_watcherRestartFailures == 0
                        ? "Bluetooth watcher restarted."
                        : $"Bluetooth watcher restarted after {_watcherRestartFailures + 1} attempts.");
                    _watcherRestartFailures = 0;
                    if (IsInitialDiscoveryCompleted)
                        await ReconcileEndpointsAsync(delay: false, forceBattery: false);
                    return;
                }
                catch (Exception ex)
                {
                    _watcherRestartFailures++;
                    await LogSafeAsync($"Bluetooth watcher restart attempt {_watcherRestartFailures} failed; retrying in {GetWatcherRestartDelay(_watcherRestartFailures).TotalSeconds:0} s: {ex.Message}");
                }
            }
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
        var isConnected = GetBoolean(endpoint, "System.Devices.Aep.IsConnected");
        var category = GetDeviceCategory(endpoint);
        if (!isConnected)
            return new BluetoothDeviceInfo(endpoint.Id, endpoint.Name,
                GetBoolean(endpoint, "System.Devices.Aep.IsPaired"), false, null, GetPhysicalDeviceId(endpoint), category);

        var containerId = GetString(endpoint, "System.Devices.Aep.ContainerId");
        var battery = GetBatteryPercent(endpoint);
        BatteryLevel? level = null;
        if (battery is null
            && containerId is not null
            && _containerBatteries.TryGetValue(containerId, out var containerBattery)
            && containerBattery.Value is int cachedBattery
            && containerBattery.LastSuccessUtc is DateTimeOffset lastSuccess
            && DateTimeOffset.UtcNow - lastSuccess <= BatteryValueExpiration)
        {
            battery = cachedBattery;
            if (containerBattery.Source?.StartsWith(GamingInputBattery.SourceName, StringComparison.Ordinal) == true)
                level = BatteryLevels.FromFraction(cachedBattery / 100.0);
        }

        return new BluetoothDeviceInfo(endpoint.Id, endpoint.Name,
            GetBoolean(endpoint, "System.Devices.Aep.IsPaired"), true, battery, GetPhysicalDeviceId(endpoint), category, level);
    }

    private static BluetoothDeviceCategory GetDeviceCategory(DeviceInformation endpoint)
    {
        if (endpoint.Properties.TryGetValue("System.Devices.Aep.Category", out var rawCategories)
            && rawCategories is IEnumerable<string> categories)
        {
            foreach (var category in categories)
            {
                if (category.Contains("Headphone", StringComparison.OrdinalIgnoreCase)
                    || category.Contains("Headset", StringComparison.OrdinalIgnoreCase)
                    || category.Contains("Earbud", StringComparison.OrdinalIgnoreCase)) return BluetoothDeviceCategory.Headphones;
                if (category.Contains("Keyboard", StringComparison.OrdinalIgnoreCase)) return BluetoothDeviceCategory.Keyboard;
                if (category.Contains("Mouse", StringComparison.OrdinalIgnoreCase)
                    || category.Contains("Pointing", StringComparison.OrdinalIgnoreCase)) return BluetoothDeviceCategory.Mouse;
                if (category.Contains("Gaming", StringComparison.OrdinalIgnoreCase)
                    || category.Contains("Gamepad", StringComparison.OrdinalIgnoreCase)
                    || category.Contains("Joystick", StringComparison.OrdinalIgnoreCase)) return BluetoothDeviceCategory.GameController;
            }
        }

        var appearance = GetUnsignedProperty(endpoint, "System.Devices.Aep.Bluetooth.Le.Appearance");
        if (appearance is uint leAppearance)
        {
            switch (leAppearance)
            {
                case 0x03C1: return BluetoothDeviceCategory.Keyboard;
                case 0x03C2: return BluetoothDeviceCategory.Mouse;
                case 0x03C3:
                case 0x03C4: return BluetoothDeviceCategory.GameController;
                case 0x0941:
                case 0x0942:
                case 0x0943: return BluetoothDeviceCategory.Headphones;
            }
        }

        var major = GetUnsignedProperty(endpoint, "System.Devices.Aep.Bluetooth.Cod.Major");
        var minor = GetUnsignedProperty(endpoint, "System.Devices.Aep.Bluetooth.Cod.Minor");
        if (major == 4 && minor is 1 or 2 or 6)
            return BluetoothDeviceCategory.Headphones;
        if (major == 5 && minor is uint peripheral)
        {
            if ((peripheral & 0x30) == 0x10) return BluetoothDeviceCategory.Keyboard;
            if ((peripheral & 0x30) == 0x20) return BluetoothDeviceCategory.Mouse;
            if ((peripheral & 0x0F) is 1 or 2) return BluetoothDeviceCategory.GameController;
        }
        return BluetoothDeviceCategory.Unknown;
    }

    private static uint? GetUnsignedProperty(DeviceInformation endpoint, string name) =>
        endpoint.Properties.TryGetValue(name, out var value)
        && uint.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed : null;

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
        // HFP Battery Level for Classic Bluetooth headsets.
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

    private sealed record DeviceEndpointSnapshot(DeviceInformation Endpoint, BluetoothDeviceInfo Device);

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
        if (endpoint.Properties.TryGetValue("System.Devices.BatteryLife", out var systemBattery)
            && TryParseBatteryPercent(systemBattery, out var battery)) return battery;
        if (endpoint.Properties.TryGetValue(BluetoothBatteryLevelProperty, out var rawBattery)
            && TryParseBatteryPercent(rawBattery, out battery)) return battery;
        return null;
    }

    private static bool TryParseBatteryPercent(object? value, out int battery)
    {
        if (value is byte byteBattery)
        {
            battery = byteBattery;
            return battery <= 100;
        }

        if (int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out battery)
            && battery is >= 0 and <= 100) return true;

        battery = 0;
        return false;
    }

    public void Dispose()
    {
        if (_isDisposing) return;
        _isDisposing = true;
        System.Threading.Timer? timer;
        lock (_endpointsLock)
        {
            timer = _reconciliationTimer;
            _reconciliationTimer = null;
        }
        timer?.Dispose();
        StopWatcher();
        _batteryLifetime.Cancel();
        _initialDiscoveryCts?.Cancel();
        // Gates and cancellation sources are not disposed: background work may still be completing and
        // would otherwise observe ObjectDisposedException; they hold no unmanaged resources.
    }

    private sealed record BatteryCacheEntry(int? Value, DateTimeOffset LastAttemptUtc, DateTimeOffset? LastSuccessUtc, string? Source);
    private sealed record BatteryEndpointCandidate(string EndpointId, ulong? BluetoothAddress);
    private sealed record BatteryCandidate(string ContainerId, IReadOnlyList<BatteryEndpointCandidate> Endpoints, long Generation);
    private sealed record BatteryReadResult(int? Value, string Source);
}
