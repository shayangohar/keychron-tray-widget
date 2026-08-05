using Microsoft.Win32;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal enum KeyboardConnection
{
    Wireless,
    Wired,
    Bluetooth,
    Disconnected,
    Error,
}

internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan PollPeriod = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan HoverRefreshPeriod = TimeSpan.FromSeconds(15);

    private readonly NotifyIcon _notifyIcon;
    private readonly System.Threading.Timer _pollTimer;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly SynchronizationContext _uiContext;
    private readonly SemaphoreSlim _pollGate = new(1, 1);
    private Icon? _currentIcon;
    private DateTime _lastHoverPoll = DateTime.MinValue;
    private HidBatteryReport? _lastBattery;
    private AnalogProfileReport? _lastAnalogProfile;
    private bool _disposed;

    internal TrayApplicationContext()
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "K8 HE: checking",
        };

        var menu = new ContextMenuStrip();
        var refreshItem = new ToolStripMenuItem("Refresh now");
        refreshItem.Click += (_, _) => _ = PollAsync();

        _autostartItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = Autostart.IsEnabled(),
        };
        _autostartItem.CheckedChanged += (_, _) => Autostart.SetEnabled(_autostartItem.Checked);

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();

        menu.Items.Add(refreshItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.MouseMove += OnMouseMove;

        _pollTimer = new System.Threading.Timer(_ => _ = PollAsync(), null, Timeout.Infinite, Timeout.Infinite);

        if (!KeychronHid.TryInitialize(out var error))
        {
            Apply(KeyboardConnection.Error, null, error ?? "HIDAPI could not start.");
            return;
        }

        _pollTimer.Change(TimeSpan.Zero, PollPeriod);
    }

    private void OnMouseMove(object? sender, MouseEventArgs args)
    {
        var now = DateTime.UtcNow;
        if (now - _lastHoverPoll < HoverRefreshPeriod)
        {
            return;
        }

        _lastHoverPoll = now;
        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        if (_disposed || !await _pollGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var result = await Task.Run(KeychronHid.Read);
            if (_disposed)
            {
                return;
            }

            var connection = GetConnection(result);
            var detail = FormatDetail(connection, result);

            _uiContext.Post(_ =>
            {
                if (result.Battery.HasValue)
                {
                    _lastBattery = result.Battery;
                }

                if (result.AnalogProfile.HasValue)
                {
                    _lastAnalogProfile = result.AnalogProfile;
                }

                Apply(connection, result.Battery, detail);
            }, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            if (!_disposed)
            {
                _uiContext.Post(_ => Apply(KeyboardConnection.Error, null, ex.Message), null);
            }
        }
        finally
        {
            _pollGate.Release();
        }
    }

    private static KeyboardConnection GetConnection(HidReadResult result)
    {
        if (result.Battery is { } battery)
        {
            return battery.Transport switch
            {
                KeychronTransport.Usb => KeyboardConnection.Wired,
                KeychronTransport.Wireless24G => KeyboardConnection.Wireless,
                KeychronTransport.Bluetooth => KeyboardConnection.Bluetooth,
                _ when result.ReceiverUsed => KeyboardConnection.Wireless,
                _ when result.WiredPresent => KeyboardConnection.Wired,
                _ => KeyboardConnection.Wireless,
            };
        }

        return result.ReceiverUsed
            ? KeyboardConnection.Wireless
            : result.WiredPresent
                ? KeyboardConnection.Wired
                : KeyboardConnection.Disconnected;
    }

    private string FormatDetail(KeyboardConnection connection, HidReadResult result)
    {
        if (result.Battery is not { } battery)
        {
            return _lastBattery is { } last
                ? $"Not detected - last seen {last.Percentage}%{FormatProfile(_lastAnalogProfile)}"
                : "Not detected";
        }

        var detail = $"{battery.Percentage}% - {TransportName(connection)}";
        if (battery.VoltageMillivolts is { } voltage)
        {
            detail += $" - {voltage} mV";
        }

        var charging = battery.Charging switch
        {
            KeychronChargingState.Charging => "Charging",
            KeychronChargingState.Full => "Full",
            KeychronChargingState.NotCharging when connection == KeyboardConnection.Wired => "Not charging",
            _ => null,
        };
        if (charging is not null)
        {
            detail += $" - {charging}";
        }

        detail += FormatProfile(result.AnalogProfile);
        return detail;
    }

    private static string TransportName(KeyboardConnection connection) => connection switch
    {
        KeyboardConnection.Wired => "Wired",
        KeyboardConnection.Bluetooth => "Bluetooth",
        _ => "2.4 GHz",
    };

    private static string FormatProfile(AnalogProfileReport? profile) => profile is { } value
        ? $" - HE profile {value.Index + 1}/{value.Count}"
        : string.Empty;

    private void Apply(KeyboardConnection connection, HidBatteryReport? battery, string detail)
    {
        var tooltip = $"K8 HE: {detail}";
        _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];

        var newIcon = TrayIcon.Create(connection, battery?.Percentage ?? _lastBattery?.Percentage);
        var oldIcon = _currentIcon;
        _currentIcon = newIcon;
        _notifyIcon.Icon = newIcon;
        oldIcon?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _pollTimer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _currentIcon?.Dispose();
            KeychronHid.Shutdown();
        }

        base.Dispose(disposing);
    }
}

internal static class Autostart
{
    private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "KeychronK8BatteryTray";

    internal static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return string.Equals(value?.Trim('"'), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

internal static class TrayIcon
{
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    internal static Icon Create(KeyboardConnection connection, int? battery)
    {
        using var bitmap = new Bitmap(16, 16);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var bodyBrush = new SolidBrush(Color.White);
        using var outlinePen = new Pen(Color.Black, 1);
        graphics.FillRectangle(bodyBrush, 2, 4, 12, 8);
        graphics.DrawRectangle(outlinePen, 2, 4, 12, 8);
        graphics.FillRectangle(Brushes.Black, 14, 7, 2, 2);

        var level = Math.Clamp(battery ?? 0, 0, 100);
        var width = level * 10 / 100;
        var fill = connection == KeyboardConnection.Error || connection == KeyboardConnection.Disconnected
            ? Brushes.Gray
            : level <= 20
                ? Brushes.Red
                : level <= 40
                    ? Brushes.Goldenrod
                    : Brushes.ForestGreen;
        graphics.FillRectangle(fill, 3, 5, width, 6);

        if (connection == KeyboardConnection.Wired)
        {
            graphics.FillRectangle(Brushes.DodgerBlue, 5, 12, 6, 2);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var source = Icon.FromHandle(handle);
            return (Icon)source.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
