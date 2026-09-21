namespace BTDeviceBatteryInfo.Services;

/// <summary>Publishes the first valid value while retaining ownership of native requests until completion.</summary>
internal static class BatteryQueryRunner
{
    public static async Task RunWithSourceAsync(IEnumerable<Func<CancellationToken, Task<BatteryQueryValue>>> sources,
        Action<BatteryQueryValue> publish, CancellationTokenSource cancellation)
    {
        async Task<BatteryQueryValue> ReadAsync(Func<CancellationToken, Task<BatteryQueryValue>> source)
        {
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return await source(cancellation.Token);
            }
            catch { return BatteryQueryValue.Unavailable; }
        }

        var pending = sources.Select(ReadAsync).ToList();
        try
        {
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending);
                pending.Remove(completed);
                var result = await completed;
                if (cancellation.IsCancellationRequested || result.Value is not (>= 0 and <= 100)) continue;
                publish(result);
                cancellation.Cancel();
                break;
            }
        }
        finally
        {
            // Cancellation stops queued work; Windows operations already running must still be observed.
            await Task.WhenAll(pending);
        }
    }

    public static async Task RunAsync(IEnumerable<Func<CancellationToken, Task<int?>>> sources,
        Action<int> publish, CancellationTokenSource cancellation)
    {
        async Task<int?> ReadAsync(Func<CancellationToken, Task<int?>> source)
        {
            try
            {
                cancellation.Token.ThrowIfCancellationRequested();
                return await source(cancellation.Token);
            }
            catch { return null; }
        }

        var pending = sources.Select(ReadAsync).ToList();
        try
        {
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending);
                pending.Remove(completed);
                var value = await completed;
                if (cancellation.IsCancellationRequested || value is not (>= 0 and <= 100)) continue;
                publish(value.Value);
                cancellation.Cancel();
                break;
            }
        }
        finally
        {
            // Cancellation stops queued work; Windows operations already running must still be observed.
            await Task.WhenAll(pending);
        }
    }
}

/// <summary>Battery value together with the Windows source that produced it.</summary>
internal sealed record BatteryQueryValue(int? Value, string Source)
{
    public static readonly BatteryQueryValue Unavailable = new(null, "Unavailable");
}
