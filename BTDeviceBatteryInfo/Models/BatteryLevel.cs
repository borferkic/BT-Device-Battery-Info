namespace BTDeviceBatteryInfo.Models;

/// <summary>
/// Coarse battery level for sources that only report ranges (for example X-input controllers through
/// Windows.Gaming.Input), where an exact percentage is not known.
/// </summary>
public enum BatteryLevel
{
    Empty,
    Low,
    Medium,
    Full
}

public static class BatteryLevels
{
    /// <summary>Maps a reported fraction of the full charge to a level.</summary>
    public static BatteryLevel FromFraction(double fraction) => fraction switch
    {
        <= 0.05 => BatteryLevel.Empty,
        <= 0.35 => BatteryLevel.Low,
        <= 0.70 => BatteryLevel.Medium,
        _ => BatteryLevel.Full
    };

    /// <summary>Representative value used only to draw the level (battery fill and taskbar ring).</summary>
    public static int RepresentativePercent(BatteryLevel level) => level switch
    {
        BatteryLevel.Empty => 5,
        BatteryLevel.Low => 25,
        BatteryLevel.Medium => 50,
        _ => 100
    };

    public static bool IsLow(BatteryLevel level) => level is BatteryLevel.Empty or BatteryLevel.Low;
}
