namespace BTDeviceBatteryInfo.Services;

/// <summary>A device reading used to decide low-battery alerts.</summary>
/// <remarks><paramref name="IsLevelLow"/> is set for coarse level sources, which ignore the percentage threshold.</remarks>
public sealed record LowBatteryReading(string DeviceId, string Name, bool IsConnected, int? BatteryPercent, bool? IsLevelLow = null);

/// <summary>
/// Decides when to raise a low-battery alert: once per device when it drops to or below the threshold,
/// and again only after it has been recharged above the threshold. Holds no UI or Windows dependencies.
/// </summary>
public sealed class LowBatteryNotifier
{
    public const int DefaultThreshold = 15;
    public static readonly int[] ThresholdOptions = [10, 15, 20, 25, 30];

    private readonly HashSet<string> _alerted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the devices that must be alerted now and updates the alerted state.</summary>
    public IReadOnlyList<LowBatteryReading> Evaluate(IEnumerable<LowBatteryReading> readings, bool enabled, int threshold)
    {
        List<LowBatteryReading> alerts = [];
        foreach (var reading in readings)
        {
            if (!reading.IsConnected || reading.BatteryPercent is not int battery) continue;

            var isLow = reading.IsLevelLow ?? battery <= threshold;
            if (!isLow)
            {
                // Recharged above the threshold: the next drop alerts again.
                _alerted.Remove(reading.DeviceId);
            }
            else if (_alerted.Add(reading.DeviceId) && enabled)
            {
                alerts.Add(reading);
            }
        }
        return alerts;
    }

    public static int NormalizeThreshold(int threshold) =>
        ThresholdOptions.Contains(threshold) ? threshold : DefaultThreshold;
}
