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
            "launch" => await LaunchAsync(args[1..]),
            "logs" => await LogsAsync(args[1..]),
            "run" => await RunScriptAsync(args[1..]),
            _ => throw new ArgumentException("Unknown android command: " + args[0])
        };
    }

    private static async Task<int> RunScriptAsync(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("android run requires an .xps source file.");
        var source = Path.GetFullPath(args[0]);
        if (!File.Exists(source)) throw new FileNotFoundException("XPScript source file was not found.", source);

        string? requestedSerial = null;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--device" && i + 1 < args.Length) requestedSerial = args[++i];
            else throw new ArgumentException("Unknown android run argument: " + args[i]);
        }

        var adb = ResolveAdb();
        var serial = await RequireReadyDeviceAsync(adb, requestedSerial);
        var abiResult = await ExecuteAsync(adb, ["-s", serial, "shell", "getprop", "ro.product.cpu.abi"]);
        if (abiResult.ExitCode != 0) throw new InvalidOperationException("Unable to detect Android device ABI: " + abiResult.Error.Trim());
        var abi = abiResult.Output.Trim().ToLowerInvariant();
        var rid = abi switch
        {
            "arm64-v8a" => "android-arm64",
            "x86_64" => "android-x64",
            _ => throw new InvalidOperationException("Unsupported Android ABI '" + abi + "'. Supported ABIs are arm64-v8a and x86_64.")
        };

        var outputDirectory = Path.Combine(Path.GetTempPath(), "xpscript-android");
        Directory.CreateDirectory(outputDirectory);
        var apk = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(source) + "-" + rid + ".apk");
        Console.WriteLine("Building " + rid + " APK...");
        var compileResult = await XPScript.Compiler.XPScriptCompilerCommandLine.CompileAsync(
            [source, "-o", apk, "--rid", rid, "--runtime=false"]);
        if (compileResult != 0) return compileResult;
        if (!File.Exists(apk)) throw new InvalidOperationException("Android compilation completed without producing the expected APK: " + apk);

        var install = await ExecuteAsync(adb, ["-s", serial, "install", "-r", apk]);
        if (install.ExitCode != 0 || !install.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("adb install failed for " + serial + ": " + (install.Error + Environment.NewLine + install.Output).Trim());

        await ExecuteAsync(adb, ["-s", serial, "logcat", "-c"]);
        var launch = await ExecuteAsync(adb, ["-s", serial, "shell", "monkey", "-p", "com.xpscript.debugapp", "-c", "android.intent.category.LAUNCHER", "1"]);
        if (launch.ExitCode != 0 || launch.Output.Contains("No activities found", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Android application launch failed on " + serial + ": " + (launch.Error + Environment.NewLine + launch.Output).Trim());

        return await WaitForCompletionAsync(adb, serial, TimeSpan.FromSeconds(30));
    }

    private static async Task<int> WaitForCompletionAsync(string adb, string serial, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string lastOutput = string.Empty;
        while (DateTime.UtcNow < deadline)
        {
            var logs = await ExecuteAsync(adb, ["-s", serial, "logcat", "-d", "-s", "XPScript:I", "*:S"]);
            if (logs.ExitCode != 0) throw new InvalidOperationException("adb logcat failed on " + serial + ": " + logs.Error.Trim());
            lastOutput = logs.Output;
            if (lastOutput.Contains("XPSCRIPT-EXIT=0", StringComparison.Ordinal))
            {
                Console.Write(lastOutput);
                return 0;
            }
            if (lastOutput.Contains("XPSCRIPT-EXIT=1", StringComparison.Ordinal))
            {
                Console.Write(lastOutput);
                return 1;
            }
            await Task.Delay(250);
        }
        if (!string.IsNullOrEmpty(lastOutput)) Console.Write(lastOutput);
        throw new TimeoutException("Android application did not report XPSCRIPT-EXIT within " + timeout.TotalSeconds + " seconds on " + serial + ".");
    }

    private static async Task<int> LaunchAsync(string[] args)
    {
        var requestedSerial = ParseOptionalDevice(args, "android launch");
        var adb = ResolveAdb();
        var serial = await RequireReadyDeviceAsync(adb, requestedSerial);
        var result = await ExecuteAsync(adb, ["-s", serial, "shell", "monkey", "-p", "com.xpscript.debugapp", "-c", "android.intent.category.LAUNCHER", "1"]);
        if (result.ExitCode != 0 || result.Output.Contains("No activities found", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Android application launch failed on " + serial + ": " + (result.Error + Environment.NewLine + result.Output).Trim());
        Console.WriteLine("Launched com.xpscript.debugapp on " + serial + ".");
        return 0;
    }

    private static async Task<int> LogsAsync(string[] args)
    {
        var requestedSerial = ParseOptionalDevice(args, "android logs");
        var adb = ResolveAdb();
        var serial = await RequireReadyDeviceAsync(adb, requestedSerial);
        var result = await StreamLogsAsync(adb, serial);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("adb logcat failed on " + serial + ": " + result.Error.Trim());
        return result;
    }

    private static async Task<int> StreamLogsAsync(string adb, string serial)
    {
        var start = new ProcessStartInfo
        {
            FileName = adb,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "-s", serial, "logcat", "-s", "XPScript:I", "*:S" })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start adb logcat.");
        var stderr = process.StandardError.ReadToEndAsync();
        while (await process.StandardOutput.ReadLineAsync() is { } line)
            Console.WriteLine(line);
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException("adb logcat failed on " + serial + ": " + (await stderr).Trim());
        return 0;
    }

    private static string? ParseOptionalDevice(string[] args, string command)
    {
        string? requestedSerial = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--device" && i + 1 < args.Length) requestedSerial = args[++i];
            else throw new ArgumentException("Unknown " + command + " argument: " + args[i]);
        }
        return requestedSerial;
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
        var overridePath = Environment.GetEnvironmentVariable("XPSCRIPT_ADB");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var fullOverride = Path.GetFullPath(overridePath);
            if (!File.Exists(fullOverride)) throw new InvalidOperationException("XPSCRIPT_ADB does not identify an adb executable: " + fullOverride);
            return fullOverride;
        }

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
  xpscript android launch [--device SERIAL]
  xpscript android logs [--device SERIAL]
  xpscript android run <source.xps> [--device SERIAL]

Commands:
  devices  List Android devices/emulators visible to adb, including unauthorized/offline state.
  install  Install or update an APK on exactly one ready device/emulator.
  launch   Launch the XPScript Android debug application.
  logs     Print XPScript-tagged Android log output.
  run      Detect device ABI, build, install, launch and print XPScript Android logs.
""");
    }
}
