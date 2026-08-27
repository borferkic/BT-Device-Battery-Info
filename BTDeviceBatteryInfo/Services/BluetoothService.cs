using BTDeviceBatteryInfo.Models;
using System.Diagnostics;
using System.Globalization;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace BTDeviceBatteryInfo.Services;

/// <summary>Keeps an in-memory view of Windows Bluetooth endpoints and updates it when Windows reports a change.</summary>
public sealed class BluetoothService : IDisposable
{
    private const string BluetoothClassicProtocolId = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
    private const string BluetoothLeProtocolId = "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}";
    private static readonly string BluetoothSelector = $"System.Devices.Aep.ProtocolId:=\"{BluetoothClassicProtocolId}\" OR System.Devices.Aep.ProtocolId:=\"{BluetoothLeProtocolId}\"";
    private static readonly string ConnectedBluetoothSelector = $"({BluetoothSelector}) AND System.Devices.Aep.IsConnected:=System.StructuredQueryType.Boolean#True";
    private static readonly string[] RequestedProperties =
    [
        "System.Devices.Aep.IsConnected",
        "System.Devices.Aep.IsPaired",
        "System.Devices.Aep.IsPresent",
        "System.Devices.Aep.ContainerId",
        "System.Devices.Aep.DeviceAddress",
        "System.Devices.BatteryLife"
    ];
    private static readonly string[] PnpBatteryProperties = ["System.Devices.ContainerId", "System.Devices.BatteryLife"];
    private static readonly string[] ContainerBatteryProperty = ["System.Devices.BatteryLife"];
    private static readonly Guid BatteryServiceUuid = new("0000180F-0000-1000-8000-00805F9B34FB");
    private static readonly Guid BatteryLevelCharacteristicUuid = new("00002A19-0000-1000-8000-00805F9B34FB");
    private readonly FileLogger _logger;
    private readonly object _endpointsLock = new();
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private readonly SemaphoreSlim _reconciliationGate = new(1, 1);
    private readonly Dictionary<string, DeviceInformation> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int?> _containerBatteries = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingBatteryContainers = new(StringComparer.OrdinalIgnoreCase);
    private DeviceWatcher? _deviceWatcher;
    private string? _selectedId;
    private bool _initialized;
    private bool? _lastConnected;

    public event EventHandler<BluetoothDeviceInfo>? StateChanged;
    public event EventHandler? DevicesChanged;
    public BluetoothService(FileLogger logger) => _logger = logger;

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> FindConnectedDevicesAsync()
    {
        await EnsureInitializedAsync();
        lock (_endpointsLock) return BuildConnectedSnapshot();
    }

    public async Task<IReadOnlyList<BluetoothDeviceInfo>> FindBoseDevicesAsync() =>
        (await FindConnectedDevicesAsync())
            .Where(device => device.Name.Contains("Bose", StringComparison.OrdinalIgnoreCase) || device.Name.Contains("QuietComfort", StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public async Task<BluetoothDeviceInfo?> SelectAsync(string id)
    {
        _selectedId = id;
        var current = (await FindConnectedDevicesAsync()).FirstOrDefault(device => device.Id == id);
        if (current is not null) await _logger.LogAsync("Device selected: " + current.Name);
        return current;
    }

    public async Task<BluetoothDeviceInfo?> CurrentAsync() =>
        string.IsNullOrWhiteSpace(_selectedId)
            ? null
            : (await FindConnectedDevicesAsync()).FirstOrDefault(device => device.Id == _selectedId);

    public async Task<bool> RequestConnectionAsync(CancellationToken token)
    {
        if ((await CurrentAsync())?.IsConnected ?? false) return true;

        await _logger.LogAsync("Connection requested: Windows exposes no public command to connect Bluetooth Classic audio.");
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

            // The first screen only needs devices that are already connected; do not enumerate all paired endpoints first.
            var stopwatch = Stopwatch.StartNew();
            var endpoints = await DeviceInformation.FindAllAsync(ConnectedBluetoothSelector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
            lock (_endpointsLock)
            {
                foreach (var endpoint in endpoints) _endpoints[endpoint.Id] = endpoint;
            }
            _ = _logger.LogAsync($"Initial connected-device query completed in {stopwatch.ElapsedMilliseconds} ms ({endpoints.Count} endpoints).");

            _deviceWatcher = DeviceInformation.CreateWatcher(BluetoothSelector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
            _deviceWatcher.Added += OnEndpointAdded;
            _deviceWatcher.Updated += OnEndpointUpdated;
            _deviceWatcher.Removed += OnEndpointRemoved;
            _deviceWatcher.Start();
            _initialized = true;
            QueueBatteryHydration(endpoints);
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    private void OnEndpointAdded(DeviceWatcher sender, DeviceInformation endpoint)
    {
        lock (_endpointsLock) _endpoints[endpoint.Id] = endpoint;
        if (GetBoolean(endpoint, "System.Devices.Aep.IsConnected")) QueueBatteryHydration([endpoint]);
        PublishChanges();
    }

    private void OnEndpointUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        DeviceInformation? endpoint = null;
        lock (_endpointsLock)
        {
            if (_endpoints.TryGetValue(update.Id, out endpoint)) endpoint.Update(update);
        }
        if (endpoint is not null && GetBoolean(endpoint, "System.Devices.Aep.IsConnected"))
            QueueBatteryHydration([endpoint]);
        else if (endpoint is null)
            ScheduleConnectedEndpointReconciliation();
        PublishChanges();
    }

    private void OnEndpointRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        lock (_endpointsLock) _endpoints.Remove(update.Id);
        PublishChanges();
    }

    private void ScheduleConnectedEndpointReconciliation() => _ = ReconcileConnectedEndpointsAsync();

    private async Task ReconcileConnectedEndpointsAsync()
    {
        if (!await _reconciliationGate.WaitAsync(0)) return;

        try
        {
            await Task.Delay(250);
            var endpoints = await DeviceInformation.FindAllAsync(ConnectedBluetoothSelector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
            lock (_endpointsLock)
            {
                foreach (var endpoint in endpoints) _endpoints[endpoint.Id] = endpoint;
            }
            QueueBatteryHydration(endpoints);
            PublishChanges();
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Connected-device reconciliation error: " + ex.Message);
        }
        finally
        {
            _reconciliationGate.Release();
        }
    }

    private void PublishChanges()
    {
        IReadOnlyList<BluetoothDeviceInfo> devices;
        lock (_endpointsLock) devices = BuildConnectedSnapshot();

        var selected = string.IsNullOrWhiteSpace(_selectedId) ? null : devices.FirstOrDefault(device => device.Id == _selectedId);
        if (selected is not null && _lastConnected != selected.IsConnected)
        {
            _lastConnected = selected.IsConnected;
            _ = _logger.LogAsync(selected.IsConnected ? "Connected" : "Disconnected");
            StateChanged?.Invoke(this, selected);
        }

        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void QueueBatteryHydration(IEnumerable<DeviceInformation> endpoints)
    {
        BatteryCandidate[] candidates;
        lock (_endpointsLock)
        {
            candidates = endpoints
                .Where(endpoint => GetBoolean(endpoint, "System.Devices.Aep.IsConnected") && GetBatteryPercent(endpoint) is null)
                .Select(endpoint =>
                {
                    var containerId = GetString(endpoint, "System.Devices.Aep.ContainerId");
                    return string.IsNullOrWhiteSpace(containerId)
                        ? null
                        : new BatteryCandidate(containerId, endpoint.Id, GetBluetoothAddress(endpoint));
                })
                .Where(candidate => candidate is not null)
                .Select(candidate => candidate!)
                .GroupBy(candidate => candidate.ContainerId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(candidate => candidate.BluetoothAddress.HasValue).First())
                .Where(candidate => !_containerBatteries.ContainsKey(candidate.ContainerId) && _pendingBatteryContainers.Add(candidate.ContainerId))
                .ToArray();
        }

        if (candidates.Length > 0) _ = HydrateBatteriesAsync(candidates);
    }

    private async Task HydrateBatteriesAsync(IReadOnlyCollection<BatteryCandidate> candidates)
    {
        var stopwatch = Stopwatch.StartNew();
        var batteries = await Task.WhenAll(candidates.Select(ReadBatteryAsync));
        lock (_endpointsLock)
        {
            foreach (var (candidate, result) in candidates.Zip(batteries))
            {
                _pendingBatteryContainers.Remove(candidate.ContainerId);
                _containerBatteries[candidate.ContainerId] = result.Value;
            }
        }

        var found = batteries.Count(pair => pair.Value is not null);
        await _logger.LogAsync($"Battery query (PnP/GATT) completed in {stopwatch.ElapsedMilliseconds} ms ({found}/{batteries.Length} devices with data).");
        PublishChanges();
    }

    private static async Task<KeyValuePair<string, int?>> ReadBatteryAsync(BatteryCandidate candidate)
    {
        var pnpTask = ReadPnpContainerBatteryAsync(candidate);
        var gattTask = ReadGattBatteryAsync(candidate);
        var pending = new List<Task<int?>> { pnpTask, gattTask };
        var timeout = Task.Delay(TimeSpan.FromSeconds(5));

        while (pending.Count > 0)
        {
            var completed = await Task.WhenAny(pending.Append(timeout));
            if (completed == timeout) break;

            var battery = await (Task<int?>)completed;
            if (battery is not null) return new KeyValuePair<string, int?>(candidate.ContainerId, battery);
            pending.Remove((Task<int?>)completed);
        }

        return new KeyValuePair<string, int?>(candidate.ContainerId, null);
    }

    private IReadOnlyList<BluetoothDeviceInfo> BuildConnectedSnapshot() =>
        _endpoints.Values
            .Select(endpoint => new
            {
                Device = ToBluetoothDevice(endpoint),
                PhysicalDeviceKey = GetString(endpoint, "System.Devices.Aep.ContainerId")
                    ?? GetString(endpoint, "System.Devices.Aep.DeviceAddress")
                    ?? endpoint.Id
            })
            .Where(item => item.Device.IsConnected && !string.IsNullOrWhiteSpace(item.Device.Name))
            .GroupBy(item => item.PhysicalDeviceKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(item => item.Device.BatteryPercent.HasValue)
                .ThenByDescending(item => item.Device.Name.Length)
                .First()
                .Device)
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

    private BluetoothDeviceInfo ToBluetoothDevice(DeviceInformation endpoint)
    {
        var containerId = GetString(endpoint, "System.Devices.Aep.ContainerId");
        var battery = GetBatteryPercent(endpoint);
        if (battery is null && containerId is not null && _containerBatteries.TryGetValue(containerId, out var containerBattery))
            battery = containerBattery;

        return new BluetoothDeviceInfo(endpoint.Id, endpoint.Name, GetBoolean(endpoint, "System.Devices.Aep.IsPaired"), GetBoolean(endpoint, "System.Devices.Aep.IsConnected"), battery);
    }

    private static async Task<int?> ReadGattBatteryAsync(BatteryCandidate candidate)
    {
        BluetoothLEDevice? device = null;
        try
        {
            // The container property has already been queried above. When Windows does not expose it,
            // ask only the standard BLE Battery Service on this already-known device; no device scan occurs.
            device = await BluetoothLEDevice.FromIdAsync(candidate.EndpointId);
            if (device is null && candidate.BluetoothAddress is ulong address)
                device = await BluetoothLEDevice.FromBluetoothAddressAsync(address);

            if (device is null) return null;

            var servicesResult = await device.GetGattServicesForUuidAsync(BatteryServiceUuid, BluetoothCacheMode.Uncached);
            if (servicesResult.Status != GattCommunicationStatus.Success)
                return null;

            foreach (var service in servicesResult.Services)
            {
                try
                {
                    var characteristicsResult = await service.GetCharacteristicsForUuidAsync(BatteryLevelCharacteristicUuid, BluetoothCacheMode.Uncached);
                    if (characteristicsResult.Status != GattCommunicationStatus.Success) continue;

                    var characteristic = characteristicsResult.Characteristics.FirstOrDefault();
                    if (characteristic is null) continue;

                    var valueResult = await characteristic.ReadValueAsync(BluetoothCacheMode.Uncached);
                    if (valueResult.Status != GattCommunicationStatus.Success || valueResult.Value.Length < 1) continue;

                    using var reader = DataReader.FromBuffer(valueResult.Value);
                    var battery = reader.ReadByte();
                    if (battery <= 100) return battery;
                }
                finally
                {
                    service.Dispose();
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            device?.Dispose();
        }
    }

    private static async Task<int?> ReadPnpContainerBatteryAsync(BatteryCandidate candidate)
    {
        if (candidate.BluetoothAddress is not ulong address) return null;

        try
        {
            var addressText = address.ToString("X12", CultureInfo.InvariantCulture);
            var selector = $"System.Devices.DeviceInstanceId:~~\"BTHLE\\\\DEV_{addressText}*\" OR System.Devices.DeviceInstanceId:~~\"BTHENUM\\\\DEV_{addressText}*\"";
            var devices = await DeviceInformation.FindAllAsync(selector, PnpBatteryProperties, DeviceInformationKind.Device);

            foreach (var device in devices)
            {
                var directBattery = GetBatteryPercent(device);
                if (directBattery is not null) return directBattery;

                var containerId = GetString(device, "System.Devices.ContainerId");
                if (string.IsNullOrWhiteSpace(containerId)) continue;

                var container = await DeviceInformation.CreateFromIdAsync(containerId, ContainerBatteryProperty, DeviceInformationKind.DeviceContainer);
                var containerBattery = GetBatteryPercent(container);
                if (containerBattery is not null) return containerBattery;
            }
        }
        catch
        {
            // Some drivers do not allow their container to be enumerated; the GATT fallback remains available.
        }

        return null;
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
        if (!endpoint.Properties.TryGetValue("System.Devices.BatteryLife", out var value) || value is null) return null;
        return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var battery) && battery is >= 0 and <= 100
            ? battery
            : null;
    }

    public void Dispose()
    {
        if (_deviceWatcher is not null)
        {
            _deviceWatcher.Added -= OnEndpointAdded;
            _deviceWatcher.Updated -= OnEndpointUpdated;
            _deviceWatcher.Removed -= OnEndpointRemoved;
            _deviceWatcher.Stop();
        }
        _initializeGate.Dispose();
    }

    private sealed record BatteryCandidate(string ContainerId, string EndpointId, ulong? BluetoothAddress);
}
