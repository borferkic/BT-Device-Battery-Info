using Windows.Devices.Radios;

namespace BTDeviceBatteryInfo.Services;

public enum BluetoothRadioStatus { Unknown, On, Off, Disabled, NoAdapter }

/// <summary>Result of a turn-on request; <see cref="MessageKey"/> is a localization key.</summary>
public sealed record BluetoothTurnOnResult(bool Succeeded, string MessageKey);

/// <summary>
/// Tracks the Windows Bluetooth radio through Windows.Devices.Radios and turns it on only when
/// the user asks. It never turns the radio off and never touches other radios or pairings.
/// </summary>
public sealed class BluetoothRadioService : IDisposable
{
    private readonly FileLogger _logger;
    private Radio? _radio;

    public BluetoothRadioService(FileLogger logger) => _logger = logger;

    public BluetoothRadioStatus Status { get; private set; } = BluetoothRadioStatus.Unknown;

    public event EventHandler? StatusChanged;

    public async Task InitializeAsync()
    {
        try
        {
            var radios = await Radio.GetRadiosAsync();
            _radio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);
            if (_radio is not null) _radio.StateChanged += OnRadioStateChanged;
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Bluetooth radio query error: " + ex.Message);
        }
        UpdateStatus();
    }

    public async Task<BluetoothTurnOnResult> TurnOnAsync()
    {
        if (_radio is null)
            return new(false, "Radio.ErrorNoAdapter");
        if (_radio.State == RadioState.On)
            return new(true, "");
        if (_radio.State == RadioState.Disabled)
            return new(false, "Radio.ErrorDisabled");

        try
        {
            var access = await Radio.RequestAccessAsync();
            if (access != RadioAccessStatus.Allowed)
                return new(false, DescribeAccess(access));

            var result = await _radio.SetStateAsync(RadioState.On);
            await _logger.LogAsync($"Bluetooth turn-on requested: {result}.");
            return result == RadioAccessStatus.Allowed
                ? new(true, "")
                : new(false, DescribeAccess(result));
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Bluetooth turn-on error: " + ex.Message);
            return new(false, "Radio.ErrorGeneric");
        }
    }

    public void Dispose()
    {
        if (_radio is not null) _radio.StateChanged -= OnRadioStateChanged;
    }

    private void OnRadioStateChanged(Radio sender, object args) => UpdateStatus();

    private void UpdateStatus()
    {
        var status = _radio?.State switch
        {
            null => BluetoothRadioStatus.NoAdapter,
            RadioState.On => BluetoothRadioStatus.On,
            RadioState.Off => BluetoothRadioStatus.Off,
            RadioState.Disabled => BluetoothRadioStatus.Disabled,
            _ => BluetoothRadioStatus.Unknown
        };
        if (status == Status) return;
        Status = status;
        _ = _logger.LogAsync($"Bluetooth radio status: {status}.");
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string DescribeAccess(RadioAccessStatus status) => status switch
    {
        RadioAccessStatus.DeniedByUser => "Radio.ErrorDeniedByUser",
        RadioAccessStatus.DeniedBySystem => "Radio.ErrorDeniedBySystem",
        _ => "Radio.ErrorGeneric"
    };
}
