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

        var project = AndroidProjectMetadata.LoadForSource(source);
        Console.WriteLine("Android project: target=" + project.Target + ", applicationType=" + project.ApplicationType);

        string deviceMode = "auto";
        string? requestedSerial = null;
        string? requestedAvd = null;
        string? requestedPlatform = null;
        var debug = false;
        for (var i = 1; i < args.Length; i++)
        {
            if (args[i] == "--device" && i + 1 < args.Length)
            {
                var deviceValue = args[++i];
                if (deviceValue.Equals("auto", StringComparison.OrdinalIgnoreCase) ||
                    deviceValue.Equals("emulator", StringComparison.OrdinalIgnoreCase) ||
                    deviceValue.Equals("physical", StringComparison.OrdinalIgnoreCase))
                    deviceMode = deviceValue.ToLowerInvariant();
                else
                    requestedSerial = deviceValue;
            }
            else if (args[i] == "--serial" && i + 1 < args.Length) requestedSerial = args[++i];
            else if (args[i] == "--avd" && i + 1 < args.Length) requestedAvd = args[++i];
            else if (args[i] == "--platform" && i + 1 < args.Length) requestedPlatform = args[++i].ToLowerInvariant();
            else if (args[i] == "--debug") debug = true;
            else throw new ArgumentException("Unknown android run argument: " + args[i]);
        }

        if (deviceMode is not ("auto" or "emulator" or "physical"))
            throw new ArgumentException("Invalid Android device selector '" + deviceMode + "'.");
        if (requestedAvd is not null && deviceMode != "emulator")
            throw new ArgumentException("--avd can only be used with --device emulator.");
        if (requestedSerial is not null && deviceMode == "emulator")
            throw new ArgumentException("--serial cannot be combined with --device emulator.");

        var adb = ResolveAdb();
        var serial = await RequireReadyDeviceAsync(adb, deviceMode, requestedSerial, requestedAvd);
        var abiResult = await ExecuteAsync(adb, ["-s", serial, "shell", "getprop", "ro.product.cpu.abi"]);
        if (abiResult.ExitCode != 0) throw new InvalidOperationException("Unable to detect Android device ABI: " + abiResult.Error.Trim());
        var abi = abiResult.Output.Trim().ToLowerInvariant();
        var detectedPlatform = abi switch
        {
            "arm64-v8a" => "android-arm64",
            "x86_64" => "android-x64",
            _ => throw new InvalidOperationException("Unsupported Android ABI '" + abi + "'. Supported ABIs are arm64-v8a and x86_64.")
        };
        var platform = requestedPlatform ?? detectedPlatform;
        if (platform is not ("android-arm64" or "android-x64"))
            throw new ArgumentException("Unsupported Android --platform '" + platform + "'. Supported platforms are android-arm64 and android-x64.");
        if (requestedPlatform is not null && requestedPlatform != detectedPlatform)
            throw new InvalidOperationException("Android --platform '" + requestedPlatform + "' does not match device ABI '" + abi + "' (" + detectedPlatform + ").");

        ValidateAndroidBuildEnvironment(deviceMode == "emulator");

        var outputDirectory = Path.Combine(Path.GetTempPath(), "xpscript-android");
        Directory.CreateDirectory(outputDirectory);
        var apk = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(source) + "-" + platform + ".apk");
        Console.WriteLine("Building " + platform + " APK...");
        var compilerArgs = new List<string> { source, "-o", apk, "--platform", platform, "--runtime=false" };
        if (debug) compilerArgs.Add("--debug");
        var compileResult = await XPScript.Compiler.XPScriptCompilerCommandLine.CompileAsync(compilerArgs.ToArray());
        if (compileResult != 0) return compileResult;
        if (!File.Exists(apk)) throw new InvalidOperationException("Android compilation completed without producing the expected APK: " + apk);

        var install = await ExecuteAsync(adb, ["-s", serial, "install", "-r", apk]);
        var installDiagnostics = install.Error + Environment.NewLine + install.Output;
        if (install.ExitCode != 0 &&
            installDiagnostics.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE", StringComparison.OrdinalIgnoreCase))
        {
            var uninstall = await ExecuteAsync(adb, ["-s", serial, "shell", "pm", "uninstall", "--user", "0", "com.xpscript.debugapp"]);
            if (uninstall.ExitCode == 0 || uninstall.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
                install = await ExecuteAsync(adb, ["-s", serial, "install", "-r", apk]);
        }

        if (install.ExitCode != 0 || !install.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("adb install failed for " + serial + ": " + (install.Error + Environment.NewLine + install.Output).Trim());

        await ExecuteAsync(adb, ["-s", serial, "logcat", "-c"]);
        var launch = await ExecuteAsync(adb, ["-s", serial, "shell", "monkey", "-p", "com.xpscript.debugapp", "-c", "android.intent.category.LAUNCHER", "1"]);
        if (launch.ExitCode != 0 || launch.Output.Contains("No activities found", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Android application launch failed on " + serial + ": " + (launch.Error + Environment.NewLine + launch.Output).Trim());

        if (project.ApplicationType == AndroidProjectMetadata.UiApplicationType)
        {
            await Task.Delay(750);
            var process = await ExecuteAsync(adb, ["-s", serial, "shell", "pidof", "com.xpscript.debugapp"]);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(process.Output))
            {
                var logs = await ExecuteAsync(adb, ["-s", serial, "logcat", "-d", "-t", "200"]);
                throw new InvalidOperationException(
                    "Android UI application was launched but is not running on " + serial + "." +
                    Environment.NewLine + logs.Output.Trim());
            }
            Console.WriteLine("Android UI application launched on " + serial + ". Use 'xpscript android logs --device " + serial + "' for runtime diagnostics.");
            return 0;
        }

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
        var serial = await RequireReadyDeviceAsync(adb, "auto", requestedSerial, null);
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
        var serial = await RequireReadyDeviceAsync(adb, "auto", requestedSerial, null);
        return await StreamLogsAsync(adb, serial);
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
        var serial = await RequireReadyDeviceAsync(adb, "auto", requestedSerial, null);
        var result = await ExecuteAsync(adb, ["-s", serial, "install", "-r", apk]);
        if (result.ExitCode != 0 || !result.Output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("adb install failed for " + serial + ": " + (result.Error + Environment.NewLine + result.Output).Trim());

        Console.WriteLine("Installed " + Path.GetFileName(apk) + " on " + serial + ".");
        return 0;
    }

    private static async Task<string> RequireReadyDeviceAsync(string adb, string deviceMode, string? requestedSerial, string? requestedAvd)
    {
        if (deviceMode == "emulator")
        {
            var emulator = ResolveAndroidTool("emulator");
            var avdResult = await ExecuteAsync(emulator, ["-list-avds"]);
            if (avdResult.ExitCode != 0)
                throw new InvalidOperationException("Unable to list Android AVDs: " + avdResult.Error.Trim());

            var avds = avdResult.Output.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (avds.Length == 0)
                throw new InvalidOperationException("No Android Virtual Device is configured.");

            var avd = requestedAvd;
            if (string.IsNullOrWhiteSpace(avd))
            {
                if (avds.Length > 1)
                    throw new InvalidOperationException("Multiple AVDs are configured. Select one with --avd NAME.");
                avd = avds[0];
            }
            if (!avds.Contains(avd, StringComparer.Ordinal))
                throw new InvalidOperationException("Android AVD '" + avd + "' was not found.");

            var current = await ExecuteAsync(adb, ["devices"]);
            var running = current.Output.Replace("\r\n", "\n").Split('\n')
                .Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(ParseDevice)
                .FirstOrDefault(device => device.State.Equals("device", StringComparison.OrdinalIgnoreCase)
                    && device.Serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(running.Serial))
                return running.Serial;

            Console.WriteLine("Starting Android emulator '" + avd + "'...");
            var start = new ProcessStartInfo { FileName = emulator, UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("-avd");
            start.ArgumentList.Add(avd);
            start.ArgumentList.Add("-no-boot-anim");
            _ = Process.Start(start) ?? throw new InvalidOperationException("Unable to start Android emulator '" + avd + "'.");

            var deadline = DateTime.UtcNow.AddMinutes(3);
            while (DateTime.UtcNow < deadline)
            {
                var devicesResult = await ExecuteAsync(adb, ["devices"]);
                if (devicesResult.ExitCode == 0)
                {
                    var candidate = devicesResult.Output.Replace("\r\n", "\n").Split('\n')
                        .Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).Select(ParseDevice)
                        .FirstOrDefault(device => device.State.Equals("device", StringComparison.OrdinalIgnoreCase)
                            && device.Serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(candidate.Serial))
                    {
                        var boot = await ExecuteAsync(adb, ["-s", candidate.Serial, "shell", "getprop", "sys.boot_completed"]);
                        if (boot.ExitCode == 0 && boot.Output.Trim() == "1")
                            return candidate.Serial;
                    }
                }
                await Task.Delay(1000);
            }
            throw new TimeoutException("Android emulator '" + avd + "' did not finish booting within 180 seconds.");
        }

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
            throw new InvalidOperationException("Multiple Android targets are ready. Select one with --device SERIAL or --serial SERIAL.");
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

    private const string AndroidCompileApiLevel = "36";

    private static void ValidateAndroidBuildEnvironment(bool emulatorRequired)
    {
        _ = ResolveAdb();
        if (emulatorRequired)
            _ = ResolveAndroidTool("emulator");

        var sdkRoot = ResolveAndroidSdkRoot();
        var platformJar = Path.Combine(sdkRoot, "platforms", "android-" + AndroidCompileApiLevel, "android.jar");
        if (!File.Exists(platformJar))
        {
            throw new InvalidOperationException(
                "Android SDK platform API " + AndroidCompileApiLevel + " is required to compile Android applications, " +
                "but android.jar was not found at '" + platformJar + "'. " +
                "Install Android SDK Platform " + AndroidCompileApiLevel + " or configure ANDROID_SDK_ROOT/ANDROID_HOME to the SDK containing it.");
        }

        var buildToolsRoot = Path.Combine(sdkRoot, "build-tools");
        var hasBuildTools = Directory.Exists(buildToolsRoot) &&
            Directory.EnumerateDirectories(buildToolsRoot).Any(directory =>
                File.Exists(Path.Combine(directory, OperatingSystem.IsWindows() ? "aapt2.exe" : "aapt2")));
        if (!hasBuildTools)
        {
            throw new InvalidOperationException(
                "Android SDK Build Tools are required to compile Android applications, but no usable build-tools installation was found under '" +
                buildToolsRoot + "'. Install Android SDK Build Tools and retry.");
        }
    }

    private static string ResolveAndroidSdkRoot()
    {
        foreach (var value in new[]
        {
            Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
            Environment.GetEnvironmentVariable("ANDROID_HOME"),
            OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk")
                : null
        }.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var root = Path.GetFullPath(value!);
            if (Directory.Exists(Path.Combine(root, "platforms")) ||
                Directory.Exists(Path.Combine(root, "build-tools")))
                return root;
        }

        throw new InvalidOperationException(
            "Android SDK was not found. Set ANDROID_SDK_ROOT or ANDROID_HOME to the Android SDK root.");
    }

    private static string ResolveAndroidTool(string name)
    {
        var executable = OperatingSystem.IsWindows() ? name + ".exe" : name;
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
            OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk") : null
        }.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var candidate = Path.Combine(root!, "emulator", executable);
            if (File.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException(name + " was not found. Install Android SDK tools or set ANDROID_SDK_ROOT/ANDROID_HOME.");
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
  xpscript android run <source.xps> [--platform RID] [--device auto|emulator|physical|SERIAL] [--serial SERIAL] [--avd NAME] [--debug]

Commands:
  devices  List Android devices/emulators visible to adb, including unauthorized/offline state.
  install  Install or update an APK on exactly one ready device/emulator.
  launch   Launch the XPScript Android debug application.
  logs     Print XPScript-tagged Android log output.
  run      Select a target, detect its ABI, build, install, launch and print XPScript Android logs.
           --device selects auto, emulator or physical.
           --serial selects one exact adb target.
           --avd names an emulator AVD when --device emulator is used.
""");
    }
}
