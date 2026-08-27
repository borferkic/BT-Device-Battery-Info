namespace BTDeviceBatteryInfo.Services;
public static class ReconnectPolicy { public static IEnumerable<TimeSpan> Delays(int attempts, int seconds) { for(var i=0;i<Math.Max(0,attempts);i++) yield return TimeSpan.FromSeconds(Math.Clamp(seconds,1,300)); } }
