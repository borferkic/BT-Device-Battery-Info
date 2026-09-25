using System.IO;
using BTDeviceBatteryInfo.Services;

namespace BTDeviceBatteryInfo.Tests;

/// <summary>Coordination of battery sources; migrated from the former tests/BatteryQueries console validation.</summary>
public class BatteryQueryRunnerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private static TaskCompletionSource<int?> Source() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task FastSourcePublishesWhileNativeOperationIsRetained()
    {
        using var cancellation = new CancellationTokenSource();
        var slow = Source();
        var published = Source();
        var count = 0;
        var run = BatteryQueryRunner.RunAsync(
            [_ => slow.Task, _ => Task.FromResult<int?>(75)],
            value => { count++; published.SetResult(value); }, cancellation);

        Assert.Equal(75, await published.Task.WaitAsync(Timeout));
        Assert.False(run.IsCompleted);
        slow.SetResult(20);
        await run.WaitAsync(Timeout);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task FailuresAndOutOfRangeValuesDoNotBlockAValidZero()
    {
        using var cancellation = new CancellationTokenSource();
        var values = new List<int>();
        await BatteryQueryRunner.RunAsync(
            [_ => Task.FromException<int?>(new IOException()), _ => Task.FromResult<int?>(101),
             _ => Task.FromResult<int?>(null), _ => Task.FromResult<int?>(0)], values.Add, cancellation);

        Assert.Equal([0], values);
    }

    [Fact]
    public async Task CancellationSuppressesLateResultButDrainsTheOperation()
    {
        using var cancellation = new CancellationTokenSource();
        var native = Source();
        var count = 0;
        var run = BatteryQueryRunner.RunAsync([_ => native.Task], _ => count++, cancellation);

        cancellation.Cancel();
        Assert.False(run.IsCompleted);
        native.SetResult(90);
        await run.WaitAsync(Timeout);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task LateSuccessIsKeptAfterOtherSourcesFail()
    {
        using var cancellation = new CancellationTokenSource();
        var late = Source();
        int? value = null;
        var run = BatteryQueryRunner.RunAsync(
            [_ => Task.FromResult<int?>(null), _ => late.Task], result => value = result, cancellation);

        Assert.False(run.IsCompleted);
        late.SetResult(100);
        await run.WaitAsync(Timeout);
        Assert.Equal(100, value);
    }

    [Fact]
    public async Task IndependentDevicesPublishIndependently()
    {
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var blocked = Source();
        var first = BatteryQueryRunner.RunAsync([_ => blocked.Task], _ => { }, firstCancellation);
        int? secondValue = null;

        await BatteryQueryRunner.RunAsync([_ => Task.FromResult<int?>(42)], value => secondValue = value, secondCancellation);

        Assert.Equal(42, secondValue);
        Assert.False(first.IsCompleted);
        blocked.SetResult(null);
        await first.WaitAsync(Timeout);
    }

    [Fact]
    public async Task SourceOfTheFirstValidValueIsKept()
    {
        using var cancellation = new CancellationTokenSource();
        BatteryQueryValue? result = null;
        await BatteryQueryRunner.RunWithSourceAsync(
            [_ => Task.FromResult(new BatteryQueryValue(null, "PnP")),
             _ => Task.FromResult(new BatteryQueryValue(70, "PnP HFP native"))],
            value => result = value, cancellation);

        Assert.Equal(new BatteryQueryValue(70, "PnP HFP native"), result);
    }
}
