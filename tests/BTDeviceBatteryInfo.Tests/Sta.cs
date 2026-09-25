using System.Runtime.ExceptionServices;

namespace BTDeviceBatteryInfo.Tests;

/// <summary>Runs code that loads WPF resources on an STA thread with the application assembly registered for pack URIs.</summary>
internal static class Sta
{
    static Sta()
    {
        _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
        if (System.Windows.Application.ResourceAssembly is null)
            System.Windows.Application.ResourceAssembly = typeof(App).Assembly;
    }

    public static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ExceptionDispatchInfo.Capture(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
