using System.Runtime.InteropServices;

namespace BTDeviceBatteryInfo.Services;

/// <summary>Reads the battery property Windows publishes on Classic Bluetooth HFP nodes.</summary>
internal static class BluetoothPnP
{
    private const uint CmGetIdListFilterPresent = 0x00000100;
    private const uint CrSuccess = 0;
    private const uint DevpropTypeByte = 3;
    private const uint DevpropTypeGuid = 15;
    private static readonly Guid DeviceContainerIdFormat = new("8C7ED206-3F8A-4A7C-9A37-0F7A7E7F0F8D");
    private static readonly Guid BluetoothBatteryFormat = new("104EA319-6EE2-4701-BD47-8DDBF425BBE5");

    public static int? TryGetBatteryForContainer(string containerId)
    {
        if (!Guid.TryParse(containerId, out var expectedContainer)) return null;
        if (CM_Get_Device_ID_List_SizeW(out var length, null, CmGetIdListFilterPresent) != CrSuccess || length == 0) return null;

        var buffer = new char[length];
        if (CM_Get_Device_ID_ListW(null, buffer, length, CmGetIdListFilterPresent) != CrSuccess) return null;
        var ids = new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        foreach (var id in ids.Where(id => id.StartsWith("BTHENUM\\", StringComparison.OrdinalIgnoreCase)
                                           || id.StartsWith("BTHLE\\", StringComparison.OrdinalIgnoreCase)))
        {
            if (CM_Locate_DevNodeW(out var devInst, id, 0) != CrSuccess) continue;
            if (!TryGetGuidProperty(devInst, new DEVPROPKEY(DeviceContainerIdFormat, 2), out var actualContainer)
                || actualContainer != expectedContainer) continue;
            if (TryGetByteProperty(devInst, new DEVPROPKEY(BluetoothBatteryFormat, 2), out var battery)
                && battery <= 100) return battery;
        }
        return null;
    }

    private static bool TryGetByteProperty(uint devInst, DEVPROPKEY key, out byte value)
    {
        var buffer = new byte[1];
        var size = 1u;
        if (CM_Get_DevNode_PropertyW(devInst, ref key, out var type, buffer, ref size, 0) != CrSuccess
            || type != DevpropTypeByte || size != 1) { value = 0; return false; }
        value = buffer[0];
        return true;
    }

    private static bool TryGetGuidProperty(uint devInst, DEVPROPKEY key, out Guid value)
    {
        var buffer = new byte[16];
        var size = 16u;
        if (CM_Get_DevNode_PropertyW(devInst, ref key, out var type, buffer, ref size, 0) != CrSuccess
            || type != DevpropTypeGuid || size != 16) { value = Guid.Empty; return false; }
        value = new Guid(buffer);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct DEVPROPKEY(Guid formatId, uint propertyId)
    {
        public readonly Guid fmtid = formatId;
        public readonly uint pid = propertyId;
    }

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_List_SizeW(out uint length, string? filter, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_Device_ID_ListW(string? filter, [Out] char[] buffer, uint length, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_PropertyW(uint devInst, ref DEVPROPKEY key, out uint propertyType,
        [Out] byte[] buffer, ref uint bufferSize, uint flags);
}
