using System.Threading;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            KeychronHid.RunSelfTest();
            return;
        }

        if (args.Contains("--probe", StringComparer.OrdinalIgnoreCase))
        {
            if (!KeychronHid.TryInitialize(out var error))
            {
                Console.Error.WriteLine(error);
                Environment.ExitCode = 1;
                return;
            }

            try
            {
                var result = KeychronHid.Read();
                var battery = result.Battery;
                var profile = result.AnalogProfile;
                Console.WriteLine(
                    $"WiredPresent={result.WiredPresent}; " +
                    $"Battery={battery?.Percentage.ToString() ?? "none"}; " +
                    $"VoltageMillivolts={battery?.VoltageMillivolts?.ToString() ?? "unknown"}; " +
                    $"Charging={battery?.Charging.ToString() ?? "unknown"}; " +
                    $"Transport={battery?.Transport.ToString() ?? "unknown"}; " +
                    $"AnalogProfile={(profile.HasValue ? $"{profile.Value.Index + 1}/{profile.Value.Count}" : "unknown")}");
            }
            finally
            {
                KeychronHid.Shutdown();
            }

            return;
        }

        using var mutex = new Mutex(true, "Local\\KeychronK8BatteryTray", out var firstInstance);
        if (!firstInstance)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        using var context = new TrayApplicationContext();
        Application.Run(context);
    }
}
