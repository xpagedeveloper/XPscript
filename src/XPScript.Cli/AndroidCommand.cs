using System.Diagnostics;

namespace XPScript.Cli;

internal static class AndroidCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            WriteHelp();
            return 0;
        }

        return args[0].ToLowerInvariant() switch
        {
            "devices" => await DevicesAsync(),
            "install" => await InstallAsync(args[1..]),
            _ => throw new ArgumentException("Unknown android command: " + args[0])
        };
    }

    private static async Task<int> InstallAsync(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("android install requires an APK path.");
        var apk = Path.GetFullPath(args[0]);
        if (!File.Exists(apk)) throw new FileNotFoundException("Android APK was not found.", apk);

        string? requestedSerial = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--device" && i + 1 < args.Length) requestedSerial = args[++i];
            else throw new ArgumentException("Unknown android install argument: " + args[i]);
        }

        var adb = ResolveAdb();
        var serial = await RequireReadyDeviceAsync(adb, requestedSerial);
        var result = await ExecuteAsync(adb, ["-s", serial, "install", "-r", apk]);
        if (result.ExitCode != 0 || !result.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("adb install failed for " + serial + ": " + (result.Error + Environment.NewLine + result.Output).Trim());

        Console.WriteLine("Installed " + Path.GetFileName(apk) + " on " + serial + ".");
        return 0;
    }

    private static async Task<string> RequireReadyDeviceAsync(string adb, string? requestedSerial)
    {
        var result = await ExecuteAsync(adb, ["devices"]);
        if (result.ExitCode != 0) throw new InvalidOperationException("adb devices failed: " + result.Error.Trim());
        var devices = result.Output.Replace("\r\n", "\n").Split('\n').Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseDevice).ToArray();

        if (requestedSerial is not null)
        {
            var selected = devices.FirstOrDefault(device => device.Serial.Equals(requestedSerial, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(selected.Serial)) throw new InvalidOperationException("Android device '" + requestedSerial + "' was not found.");
            if (!selected.State.Equals("device", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Android device '" + requestedSerial + "' is " + selected.State + ".");
            return selected.Serial;
        }

        var ready = devices.Where(device => device.State.Equals("device", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (ready.Length == 0)
        {
            var blocked = devices.FirstOrDefault(device => device.State is "unauthorized" or "offline");
            if (!string.IsNullOrEmpty(blocked.Serial)) throw new InvalidOperationException("Android device '" + blocked.Serial + "' is " + blocked.State + ".");
            throw new InvalidOperationException("No ready Android device or emulator was detected.");
        }
        if (ready.Length > 1)
            throw new InvalidOperationException("Multiple Android devices/emulators are ready. Select one with --device SERIAL.");
        return ready[0].Serial;
    }

    private static async Task<int> DevicesAsync()
    {
        var adb = ResolveAdb();
        var result = await ExecuteAsync(adb, ["devices", "-l"]);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("adb devices failed: " + result.Error.Trim());

        var devices = result.Output.Replace("\r\n", "\n").Split('\n')
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseDevice)
            .ToArray();

        if (devices.Length == 0)
        {
            Console.WriteLine("No Android devices or emulators detected.");
            return 0;
        }

        foreach (var device in devices)
            Console.WriteLine($"{device.Serial}\t{device.State}\t{device.Description}");

        if (devices.Any(device => device.State.Equals("unauthorized", StringComparison.OrdinalIgnoreCase)))
            Console.Error.WriteLine("warning: authorize USB debugging on the unauthorized Android device.");
        if (devices.Any(device => device.State.Equals("offline", StringComparison.OrdinalIgnoreCase)))
            Console.Error.WriteLine("warning: an Android device is offline.");

        return 0;
    }

    private static (string Serial, string State, string Description) ParseDevice(string line)
    {
        var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 2)
            return (line.Trim(), "unknown", string.Empty);
        return (fields[0], fields[1], string.Join(' ', fields.Skip(2)));
    }

    private static string ResolveAdb()
    {
        var executable = OperatingSystem.IsWindows() ? "adb.exe" : "adb";
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), executable);
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var root in new[]
        {
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
                : null
        }.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var candidate = Path.Combine(root!, "platform-tools", executable);
            if (File.Exists(candidate)) return candidate;
        }

        throw new InvalidOperationException("adb was not found. Install Android Platform Tools or set ANDROID_SDK_ROOT/ANDROID_HOME.");
    }

    private static async Task<(int ExitCode, string Output, string Error)> ExecuteAsync(string fileName, IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start adb.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdout, await stderr);
    }

    private static void WriteHelp()
    {
        Console.WriteLine("""
Usage:
  xpscript android devices
  xpscript android install <app.apk> [--device SERIAL]

Commands:
  devices  List Android devices/emulators visible to adb, including unauthorized/offline state.
  install  Install or update an APK on exactly one ready device/emulator.
""");
    }
}
