using System.Runtime.InteropServices;

internal enum KeychronTransport
{
    Unknown,
    Usb,
    Bluetooth,
    Wireless24G,
}

internal enum KeychronChargingState
{
    Unknown,
    NotCharging,
    Charging,
    Full,
}

internal readonly record struct HidBatteryReport(
    int Percentage,
    KeychronChargingState Charging,
    KeychronTransport Transport);

internal readonly record struct AnalogProfileReport(int Index, int Count)
{
    internal string Name => Index switch
    {
        0 => "Default",
        1 => "Gaming",
        2 => "Gamepad",
        _ => $"Profile {Index + 1}",
    };
}

internal readonly record struct HidReadResult(
    bool WiredPresent,
    HidBatteryReport? Battery,
    AnalogProfileReport? AnalogProfile,
    bool ReceiverUsed);

internal static class KeychronHid
{
    private const ushort VendorId = 0x3434;
    private const ushort WiredProductId = 0x0E80;
    private const ushort ReceiverProductId = 0xD030;
    private const ushort RawUsagePage = 0xFF60;
    private const ushort RawUsage = 0x0061;
    private const byte BatteryCommand = 0xA4;
    private const byte AnalogCommand = 0xA9;
    private const byte GetProfilesInfoCommand = 0x10;
    private const int ReportLength = 33;

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

    private readonly record struct DeviceInfo(string Path);
    private readonly record struct HidResponse(byte[] Data, int Length);
    private readonly record struct DeviceReadResult(
        HidBatteryReport? Battery,
        AnalogProfileReport? AnalogProfile);

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

        var wired = FindDevice(WiredProductId);
        var wiredRead = wired is not null ? ReadDevice(wired.Value) : default;

        // Prefer the direct keyboard endpoint. It works in Cable mode and also
        // reports the current transport when the USB cable only supplies power.
        if (wiredRead.Battery.HasValue)
        {
            return new(wired is not null, wiredRead.Battery, wiredRead.AnalogProfile, false);
        }

        var receiver = FindDevice(ReceiverProductId);
        var receiverRead = receiver is not null ? ReadDevice(receiver.Value) : default;
        return new(
            wired is not null,
            receiverRead.Battery,
            wiredRead.AnalogProfile ?? receiverRead.AnalogProfile,
            receiverRead.Battery.HasValue || receiverRead.AnalogProfile.HasValue);
    }

    internal static AnalogProfileReport? ReadProfile()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("HIDAPI is not initialized.");
        }

        var wired = FindDevice(WiredProductId);
        var profile = wired is not null ? ReadProfile(wired.Value) : null;
        if (profile.HasValue)
        {
            return profile;
        }

        var receiver = FindDevice(ReceiverProductId);
        return receiver is not null ? ReadProfile(receiver.Value) : null;
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
        var basic = ParseBatteryResponse(new byte[] { 0xA4, 0x5B }, 2);
        Assert(basic.HasValue && basic.Value.Percentage == 91, "short battery response");

        var detailed = ParseBatteryResponse(new byte[] { 0x00, 0xA4, 0x5C, 0xB0, 0x0F, 0x01, 0x01, 0x02 }, 8);
        Assert(
            detailed.HasValue &&
            detailed.Value.Percentage == 92 &&
            detailed.Value.Charging == KeychronChargingState.Charging &&
            detailed.Value.Transport == KeychronTransport.Usb,
            "detailed battery response");

        var otherModel = ParseBatteryResponse(new byte[] { 0xA4, 0x5C, 0xB0, 0x0F, 0x01, 0x04, 0x07 }, 7);
        Assert(
            otherModel.HasValue &&
            otherModel.Value.Charging == KeychronChargingState.Charging &&
            otherModel.Value.Transport == KeychronTransport.Wireless24G,
            "other model battery response");

        var profile = ParseAnalogProfileResponse(new byte[] { 0x00, 0xA9, 0x10, 0x02, 0x03 }, 5);
        Assert(profile.HasValue && profile.Value.Index == 2 && profile.Value.Count == 3 && profile.Value.Name == "Gamepad", "analog profile response");

        Assert(ParseBatteryResponse(new byte[] { 0xA4, 0x65 }, 2) is null, "invalid percentage");
        Assert(ParseBatteryResponse(new byte[] { 0xA3, 0x5B }, 2) is null, "invalid battery command");
        Assert(ParseAnalogProfileResponse(new byte[] { 0xA9, 0x11, 0x02, 0x03 }, 4) is null, "invalid analog command");
        Console.WriteLine("HID protocol parser self-test passed.");
    }

    private static DeviceReadResult ReadDevice(DeviceInfo deviceInfo)
    {
        var device = hid_open_path(deviceInfo.Path);
        if (device == IntPtr.Zero)
        {
            return default;
        }

        try
        {
            var battery = ReadBattery(device);
            var profile = ReadAnalogProfile(device);
            return new(battery, profile);
        }
        finally
        {
            hid_close(device);
        }
    }

    private static AnalogProfileReport? ReadProfile(DeviceInfo deviceInfo)
    {
        var device = hid_open_path(deviceInfo.Path);
        if (device == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var response = SendRequest(device, AnalogCommand, GetProfilesInfoCommand);
            return response.HasValue ? ParseAnalogProfileResponse(response.Value.Data, response.Value.Length) : null;
        }
        finally
        {
            hid_close(device);
        }
    }

    private static HidBatteryReport? ReadBattery(IntPtr device)
    {
        var response = SendRequest(device, BatteryCommand);
        return response.HasValue ? ParseBatteryResponse(response.Value.Data, response.Value.Length) : null;
    }

    private static AnalogProfileReport? ReadAnalogProfile(IntPtr device)
    {
        var response = SendRequest(device, AnalogCommand, GetProfilesInfoCommand);
        return response.HasValue ? ParseAnalogProfileResponse(response.Value.Data, response.Value.Length) : null;
    }

    private static HidResponse? SendRequest(IntPtr device, byte command, byte subcommand = 0)
    {
        var request = new byte[ReportLength];
        request[1] = command;
        if (subcommand != 0)
        {
            request[2] = subcommand;
        }

        if (hid_write(device, request, (UIntPtr)request.Length) < 0)
        {
            return null;
        }

        var response = new byte[ReportLength];
        var length = hid_read_timeout(device, response, (UIntPtr)response.Length, 1000);
        return length > 0 ? new HidResponse(response, length) : null;
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
                        return new DeviceInfo(path);
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

    private static HidBatteryReport? ParseBatteryResponse(byte[] response, int length)
    {
        var offset = FindCommand(response, length, BatteryCommand);
        if (offset < 0 || length <= offset + 1)
        {
            return null;
        }

        var percentage = response[offset + 1];
        if (percentage > 100)
        {
            return null;
        }

        // A non-zero model byte marks the extended report. Older firmware only
        // returns the percentage and leaves the remaining bytes absent.
        if (length > offset + 6 && response[offset + 6] != 0)
        {
            return new(
                percentage,
                ParseChargingState(response[offset + 4]),
                ParseTransport(response[offset + 5]));
        }

        return new(percentage, KeychronChargingState.Unknown, KeychronTransport.Unknown);
    }

    private static AnalogProfileReport? ParseAnalogProfileResponse(byte[] response, int length)
    {
        var offset = FindCommand(response, length, AnalogCommand);
        if (offset < 0 || length <= offset + 3 || response[offset + 1] != GetProfilesInfoCommand)
        {
            return null;
        }

        var index = response[offset + 2];
        var count = response[offset + 3];
        return count > 0 && index < count ? new AnalogProfileReport(index, count) : null;
    }

    private static int FindCommand(byte[] response, int length, byte command)
    {
        if (length >= 2 && response[0] == 0 && response[1] == command)
        {
            return 1;
        }

        return length >= 1 && response[0] == command ? 0 : -1;
    }

    private static KeychronChargingState ParseChargingState(byte value) => value switch
    {
        0 => KeychronChargingState.NotCharging,
        1 => KeychronChargingState.Charging,
        2 => KeychronChargingState.Full,
        _ => KeychronChargingState.Unknown,
    };

    private static KeychronTransport ParseTransport(byte value) => value switch
    {
        1 => KeychronTransport.Usb,
        2 => KeychronTransport.Bluetooth,
        4 => KeychronTransport.Wireless24G,
        _ => KeychronTransport.Unknown,
    };

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Self-test failed: {name}.");
        }
    }
}
