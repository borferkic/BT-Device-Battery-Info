using BTDeviceBatteryInfo.Services;

static TaskCompletionSource<int?> Source() => new(TaskCreationOptions.RunContinuationsAsynchronously);
static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

// A fast source publishes while a native operation is still running; ownership is retained.
using (var cancellation = new CancellationTokenSource())
{
    var slow = Source();
    var published = Source();
    var count = 0;
    var run = BatteryQueryRunner.RunAsync(
        [token => slow.Task, token => Task.FromResult<int?>(75)],
        value => { count++; published.SetResult(value); }, cancellation);
    Check(await published.Task.WaitAsync(TimeSpan.FromSeconds(2)) == 75, "No se publicó el resultado rápido.");
    Check(!run.IsCompleted, "Se liberó una consulta nativa todavía pendiente.");
    slow.SetResult(20);
    await run.WaitAsync(TimeSpan.FromSeconds(2));
    Check(count == 1, "Un resultado secundario sobrescribió al primero.");
}

// Invalid readings and failures do not prevent a valid source from winning.
using (var cancellation = new CancellationTokenSource())
{
    var values = new List<int>();
    await BatteryQueryRunner.RunAsync(
        [token => Task.FromException<int?>(new IOException()), token => Task.FromResult<int?>(101),
         token => Task.FromResult<int?>(null), token => Task.FromResult<int?>(0)], values.Add, cancellation);
    Check(values.SequenceEqual([0]), "No se respetan fallos, rangos o el nivel cero.");
}

// Closing suppresses a late native result but still drains the operation.
using (var cancellation = new CancellationTokenSource())
{
    var native = Source();
    var count = 0;
    var run = BatteryQueryRunner.RunAsync([token => native.Task], value => count++, cancellation);
    cancellation.Cancel();
    Check(!run.IsCompleted, "Se abandonó una operación nativa al cancelar.");
    native.SetResult(90);
    await run.WaitAsync(TimeSpan.FromSeconds(2));
    Check(count == 0, "Se publicó después del cierre.");
}

// A late success remains usable; failures from other sources do not discard it.
using (var cancellation = new CancellationTokenSource())
{
    var late = Source();
    int? value = null;
    var run = BatteryQueryRunner.RunAsync(
        [token => Task.FromResult<int?>(null), token => late.Task], result => value = result, cancellation);
    Check(!run.IsCompleted, "Se descartó prematuramente la fuente pendiente.");
    late.SetResult(100);
    await run.WaitAsync(TimeSpan.FromSeconds(2));
    Check(value == 100, "Se perdió el resultado tardío.");
}

// Independent devices publish independently, even if another device has no response yet.
using (var firstCancellation = new CancellationTokenSource())
using (var secondCancellation = new CancellationTokenSource())
{
    var blocked = Source();
    var first = BatteryQueryRunner.RunAsync([token => blocked.Task], _ => { }, firstCancellation);
    int? secondValue = null;
    await BatteryQueryRunner.RunAsync([token => Task.FromResult<int?>(42)], value => secondValue = value, secondCancellation);
    Check(secondValue == 42 && !first.IsCompleted, "Un dispositivo bloqueó a otro.");
    blocked.SetResult(null);
    await first.WaitAsync(TimeSpan.FromSeconds(2));
}

// The diagnostic source follows the first valid value without changing coordination semantics.
using (var cancellation = new CancellationTokenSource())
{
    BatteryQueryValue? result = null;
    await BatteryQueryRunner.RunWithSourceAsync(
        [token => Task.FromResult(new BatteryQueryValue(null, "PnP")),
         token => Task.FromResult(new BatteryQueryValue(70, "PnP HFP native"))],
        value => result = value, cancellation);
    Check(result is { Value: 70, Source: "PnP HFP native" }, "No se conservó la fuente del resultado válido.");
}

Console.WriteLine("Correcto: 6 escenarios de coordinación de batería; sin hardware Bluetooth.");
