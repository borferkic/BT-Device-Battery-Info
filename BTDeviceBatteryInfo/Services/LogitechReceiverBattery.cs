using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BTDeviceBatteryInfo.Models;
using Microsoft.Win32.SafeHandles;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;

namespace BTDeviceBatteryInfo.Services;

/// <summary>
/// Read-only battery source for devices paired to a Logitech USB receiver (Logi Bolt, Lightspeed or Unifying), through the
/// HID++ 2.0 protocol on the receiver's vendor-defined long-report collection. WinRT HidDevice cannot
/// open that collection from an unpackaged app, so it is opened with CreateFile in shared mode.
/// </summary>
internal static class LogitechReceiverBattery
{
    public const string SourceName = "Logitech receiver HID++";
    private const ushort LogitechVendorId = 0x046D;
    // Receivers whose paired devices speak HID++ 2.0: Unifying, Logi Bolt and the Lightspeed family.
    private static readonly ushort[] ReceiverProductIds = [0xC52B, 0xC532, 0xC548, 0xC539, 0xC53A, 0xC53D, 0xC53F, 0xC541, 0xC545, 0xC547, 0xC54D];
    private const byte LongReportId = 0x11;
    private const int LongReportLength = 20;
    private const byte PingSoftwareId = 0x01;
    private const byte MaxSlot = 6;
    private const ushort DeviceNameFeature = 0x0005;
    private const ushort BatteryStatusFeature = 0x1000;
    private const ushort BatteryVoltageFeature = 0x1001;
    private const ushort UnifiedBatteryFeature = 0x1004;
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMilliseconds(1500);
    // A sleeping device can take over a second to answer its first request after waking.
    private static readonly TimeSpan DiscoveryWindow = TimeSpan.FromSeconds(2);
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Reads every device that answers on a connected receiver. Offline or empty slots are omitted.</summary>
    public static async Task<IReadOnlyList<BluetoothDeviceInfo>> ReadAsync(CancellationToken token)
    {
        await Gate.WaitAsync(token);
        try
        {
            var devices = new List<BluetoothDeviceInfo>();
            foreach (var productId in ReceiverProductIds)
            {
                var selector = HidDevice.GetDeviceSelector(0xFF00, 0x0002, LogitechVendorId, productId);
                foreach (var receiver in await DeviceInformation.FindAllAsync(selector))
                {
                    token.ThrowIfCancellationRequested();
                    using var channel = Channel.Open(receiver.Id);
                    if (channel is null) continue;
                    foreach (var slot in await channel.PingAllAsync(token))
                    {
                        if (await ReadSlotAsync(channel, receiver.Id, productId, slot, token) is { } device) devices.Add(device);
                    }
                }
            }
            return devices;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<BluetoothDeviceInfo?> ReadSlotAsync(Channel channel, string receiverId, ushort productId, byte slot, CancellationToken token)
    {
        var name = $"Logitech device {slot}";
        var category = BluetoothDeviceCategory.Unknown;
        if (await GetFeatureIndexAsync(channel, slot, DeviceNameFeature, token) is byte nameIndex)
        {
            name = await ReadNameAsync(channel, slot, nameIndex, token) ?? name;
            if (await channel.RequestAsync(slot, nameIndex, 2, [], token) is { } type) category = ToCategory(type[4]);
        }

        int? battery = null;
        if (await GetFeatureIndexAsync(channel, slot, UnifiedBatteryFeature, token) is byte unifiedIndex)
        {
            // get_status: state of charge in percent, level flags, charging status, external power.
            if (await channel.RequestAsync(slot, unifiedIndex, 1, [], token) is { } status && status[4] <= 100) battery = status[4];
        }
        else if (await GetFeatureIndexAsync(channel, slot, BatteryStatusFeature, token) is byte statusIndex)
        {
            if (await channel.RequestAsync(slot, statusIndex, 0, [], token) is { } status && status[4] is > 0 and <= 100) battery = status[4];
        }
        else if (await GetFeatureIndexAsync(channel, slot, BatteryVoltageFeature, token) is byte voltageIndex)
        {
            // Many Lightspeed gaming mice only report the cell voltage in millivolts.
            if (await channel.RequestAsync(slot, voltageIndex, 0, [], token) is { } voltage) battery = PercentFromVoltage((voltage[4] << 8) | voltage[5]);
        }

        return new BluetoothDeviceInfo($"{receiverId}#{slot}", name, true, true, battery, GetPhysicalDeviceId(productId, slot, name), category);
    }

    /// <summary>Stable identity for a receiver device; the receiver keeps the slot of a device while it stays paired.</summary>
    internal static string GetPhysicalDeviceId(ushort productId, byte slot, string name) => $"logi-receiver:{productId:X4}:{slot}:{name}";

    // Approximate discharge curve of a single Li-ion cell under light load (millivolts, percent).
    private static readonly (int Millivolts, int Percent)[] VoltageCurve =
        [(4186, 100), (4067, 90), (3989, 80), (3922, 70), (3859, 60), (3811, 50), (3778, 40), (3751, 30), (3717, 20), (3671, 10), (3500, 0)];

    /// <summary>Estimates the charge from the cell voltage, interpolating between points of the curve.</summary>
    internal static int? PercentFromVoltage(int millivolts)
    {
        if (millivolts is < 2500 or > 5000) return null;
        if (millivolts >= VoltageCurve[0].Millivolts) return 100;
        for (var i = 1; i < VoltageCurve.Length; i++)
        {
            var (upperVolts, upperPercent) = VoltageCurve[i - 1];
            var (lowerVolts, lowerPercent) = VoltageCurve[i];
            if (millivolts < lowerVolts) continue;
            return lowerPercent + (int)Math.Round((double)(millivolts - lowerVolts) / (upperVolts - lowerVolts) * (upperPercent - lowerPercent));
        }
        return 0;
    }

    internal static BluetoothDeviceCategory ToCategory(byte deviceType) => deviceType switch
    {
        0 => BluetoothDeviceCategory.Keyboard,
        3 or 4 or 5 => BluetoothDeviceCategory.Mouse, // mouse, trackpad, trackball
        _ => BluetoothDeviceCategory.Unknown
    };

    private static async Task<byte?> GetFeatureIndexAsync(Channel channel, byte slot, ushort feature, CancellationToken token)
    {
        var response = await channel.RequestAsync(slot, 0, 0, [(byte)(feature >> 8), (byte)feature], token);
        return response is null || response[4] == 0 ? null : response[4];
    }

    private static async Task<string?> ReadNameAsync(Channel channel, byte slot, byte nameIndex, CancellationToken token)
    {
        if (await channel.RequestAsync(slot, nameIndex, 0, [], token) is not { } count) return null;
        var length = count[4];
        var name = new StringBuilder();
        while (name.Length < length)
        {
            if (await channel.RequestAsync(slot, nameIndex, 1, [(byte)name.Length], token) is not { } chunk) return null;
            name.Append(Encoding.ASCII.GetString(chunk, 4, Math.Min(LongReportLength - 4, length - name.Length)));
        }
        var text = name.ToString().TrimEnd('\0').Trim();
        return text.Length == 0 ? null : text;
    }

    private sealed class Channel : IDisposable
    {
        private readonly FileStream _stream;
        private readonly CancellationTokenSource _closing = new();
        private readonly System.Threading.Channels.Channel<byte[]> _reports = System.Threading.Channels.Channel.CreateUnbounded<byte[]>();
        private readonly Task _reader;
        private byte _softwareId = PingSoftwareId;

        private Channel(FileStream stream)
        {
            _stream = stream;
            // One continuous reader: cancelling individual HID reads on a timeout can lose or misalign reports.
            _reader = Task.Run(ReadLoopAsync);
        }

        public static Channel? Open(string path)
        {
            var handle = CreateFile(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, FileFlagOverlapped, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                return null;
            }
            // No buffering: each output report must reach the receiver as soon as it is written.
            return new Channel(new FileStream(handle, FileAccess.ReadWrite, 0, true));
        }

        private async Task ReadLoopAsync()
        {
            var buffer = new byte[LongReportLength];
            try
            {
                while (!_closing.IsCancellationRequested)
                {
                    var read = await _stream.ReadAsync(buffer, _closing.Token);
                    if (read > 0) _reports.Writer.TryWrite(buffer[..read]);
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
            }
            finally
            {
                _reports.Writer.TryComplete();
            }
        }

        /// <summary>
        /// Pings every slot at once (root feature, function 1) and returns the ones that answer within the
        /// discovery window. Empty slots report their error on the short-report collection, which is not
        /// read here, so they simply stay silent.
        /// </summary>
        public async Task<IReadOnlyList<byte>> PingAllAsync(CancellationToken token)
        {
            while (_reports.Reader.TryRead(out _)) { }
            var answered = new SortedSet<byte>();
            using var window = CancellationTokenSource.CreateLinkedTokenSource(token);
            window.CancelAfter(DiscoveryWindow);
            try
            {
                for (byte slot = 1; slot <= MaxSlot; slot++) await _stream.WriteAsync(BuildRequest(slot, 0, 1, [], PingSoftwareId), window.Token);
                while (answered.Count < MaxSlot)
                {
                    var report = await _reports.Reader.ReadAsync(window.Token);
                    if (report.Length >= 7 && report[0] == LongReportId && report[1] is >= 1 and <= MaxSlot
                        && report[2] == 0 && report[3] == ((1 << 4) | PingSoftwareId))
                        answered.Add(report[1]);
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is IOException or System.Threading.Channels.ChannelClosedException)
            {
            }
            return answered.ToArray();
        }

        private static byte[] BuildRequest(byte slot, byte featureIndex, byte function, byte[] parameters, byte softwareId)
        {
            var request = new byte[LongReportLength];
            request[0] = LongReportId;
            request[1] = slot;
            request[2] = featureIndex;
            request[3] = (byte)((function << 4) | softwareId);
            parameters.CopyTo(request, 4);
            return request;
        }

        /// <summary>Sends a long HID++ request and waits for its answer, skipping unrelated notifications.</summary>
        public async Task<byte[]?> RequestAsync(byte slot, byte featureIndex, byte function, byte[] parameters, CancellationToken token)
        {
            while (_reports.Reader.TryRead(out _)) { }
            // A new software ID per request keeps a late answer to an earlier request from being taken for this one.
            _softwareId = (byte)(_softwareId % 15 + 1);
            var request = BuildRequest(slot, featureIndex, function, parameters, _softwareId);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(ResponseTimeout);
            try
            {
                await _stream.WriteAsync(request, timeout.Token);
                while (true)
                {
                    var report = await _reports.Reader.ReadAsync(timeout.Token);
                    if (report.Length < 7 || report[1] != slot) continue;
                    // 0x8F: HID++ 1.0 error from the receiver (for example, no device in the slot); 0xFF: HID++ 2.0 error.
                    if (report[2] == 0x8F || (report[2] == 0xFF && report[3] == featureIndex)) return null;
                    if (report[0] == LongReportId && report[2] == featureIndex && report[3] == request[3]) return report;
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex) when (ex is IOException or System.Threading.Channels.ChannelClosedException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            _closing.Cancel();
            _stream.Dispose();
            try { _reader.Wait(TimeSpan.FromSeconds(1)); } catch (AggregateException) { }
        }
    }

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
}
