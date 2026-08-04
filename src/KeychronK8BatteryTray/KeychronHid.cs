using System.Runtime.InteropServices;

internal readonly record struct HidReadResult(bool WiredPresent, int? WirelessBattery);

internal static class KeychronHid
{
    private const ushort VendorId = 0x3434;
    private const ushort WiredProductId = 0x0E80;
    private const ushort ReceiverProductId = 0xD030;
    private const ushort RawUsagePage = 0xFF60;
    private const ushort RawUsage = 0x0061;
    private const byte BatteryCommand = 0xA4;

    private static bool _initialized;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeDeviceInfo
    {
        public IntPtr Path;
        public ushort VendorId;
        public ushort ProductId;
        public IntPtr SerialNumber;
        public ushort ReleaseNumber;
        public IntPtr ManufacturerString;
        public IntPtr ProductString;
        public ushort UsagePage;
        public ushort Usage;
        public int InterfaceNumber;
        public IntPtr Next;
    }

    private readonly record struct DeviceInfo(string Path, ushort UsagePage, ushort Usage);

    [DllImport("hidapi", CallingConvention = CallingConvention.Cdecl)]
    private static extern int hid_init();

    [DllImport("hidapi", CallingConvention = CallingConvention.Cdecl)]
    private static extern int hid_exit();

    [DllImport("hidapi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr hid_enumerate(ushort vendorId, ushort productId);

    [DllImport("hidapi", CallingConvention = CallingConvention.Cdecl)]
    private static extern void hid_free_enumeration(IntPtr devices);

    [DllImport("hidapi", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern IntPtr hid_open_path([MarshalAs(UnmanagedType.LPStr)] string path);

    [DllImport("hidapi", CallingConvention = CallingConvention.Cdecl)]
    private static extern void hid_close(IntPtr device);

    [DllImport("hidapi", CallingConvention = CallingConvention.Cdecl)]
    private static extern int hid_write(
        IntPtr device,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] data,
        UIntPtr length);

    [DllImport("hidapi", CallingConvention = CallingConvention.Cdecl)]
    private static extern int hid_read_timeout(
        IntPtr device,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] data,
        UIntPtr length,
        int milliseconds);

    internal static bool TryInitialize(out string? error)
    {
        try
        {
            var result = hid_init();
            _initialized = result == 0;
            error = _initialized ? null : "HIDAPI could not start.";
            return _initialized;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            error = "hidapi.dll is missing or is not valid.";
            return false;
        }
    }

    internal static HidReadResult Read()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("HIDAPI is not initialized.");
        }

        var wired = FindDevice(WiredProductId) is not null;
        var wirelessBattery = ReadWirelessBattery();
        return new HidReadResult(wired, wirelessBattery);
    }

    internal static void Shutdown()
    {
        if (_initialized)
        {
            hid_exit();
            _initialized = false;
        }
    }

    internal static void RunSelfTest()
    {
        Assert(ParseBatteryResponse(new byte[] { 0xA4, 0x5B }, 2) == 91, "short response");
        Assert(ParseBatteryResponse(new byte[] { 0x00, 0xA4, 0x5C }, 3) == 92, "report-id response");
        Assert(ParseBatteryResponse(new byte[] { 0xA4, 0x65 }, 2) is null, "invalid percentage");
        Assert(ParseBatteryResponse(new byte[] { 0xA3, 0x5B }, 2) is null, "invalid command");
        Console.WriteLine("Battery response parser self-test passed.");
    }

    private static int? ReadWirelessBattery()
    {
        var deviceInfo = FindDevice(ReceiverProductId);
        if (deviceInfo is null)
        {
            return null;
        }

        var device = hid_open_path(deviceInfo.Value.Path);
        if (device == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var request = new byte[33];
            request[1] = BatteryCommand;
            if (hid_write(device, request, (UIntPtr)request.Length) < 0)
            {
                return null;
            }

            var response = new byte[33];
            var length = hid_read_timeout(device, response, (UIntPtr)response.Length, 1000);
            return length > 0 ? ParseBatteryResponse(response, length) : null;
        }
        finally
        {
            hid_close(device);
        }
    }

    private static DeviceInfo? FindDevice(ushort productId)
    {
        var head = hid_enumerate(VendorId, productId);
        if (head == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            for (var current = head; current != IntPtr.Zero;)
            {
                var native = Marshal.PtrToStructure<NativeDeviceInfo>(current);
                if (native.UsagePage == RawUsagePage && native.Usage == RawUsage)
                {
                    var path = Marshal.PtrToStringAnsi(native.Path);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return new DeviceInfo(path, native.UsagePage, native.Usage);
                    }
                }

                current = native.Next;
            }

            return null;
        }
        finally
        {
            hid_free_enumeration(head);
        }
    }

    private static int? ParseBatteryResponse(byte[] response, int length)
    {
        if (length >= 3 && response[0] == 0 && response[1] == BatteryCommand)
        {
            return response[2] <= 100 ? response[2] : null;
        }

        if (length >= 2 && response[0] == BatteryCommand)
        {
            return response[1] <= 100 ? response[1] : null;
        }

        return null;
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Self-test failed: {name}.");
        }
    }
}
