using System.Diagnostics;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Cli;

internal static class ServiceCommand
{
    private static readonly Regex ServiceNamePattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$", RegexOptions.CultureInvariant);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("service requires a subcommand. Supported: install.");

        return args[0].ToLowerInvariant() switch
        {
            "install" => await InstallAsync(args[1..]).ConfigureAwait(false),
            _ => throw new ArgumentException("Unknown service subcommand: " + args[0])
        };
    }

    private static async Task<int> InstallAsync(string[] args)
    {
        if (args.Length == 0)
            throw new ArgumentException("service install requires a compiled service executable.");

        var executable = Path.GetFullPath(args[0]);
        if (!File.Exists(executable))
            throw new FileNotFoundException("Compiled service executable was not found.", executable);

        string? name = null;
        string? displayName = null;
        var startMode = "manual";

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--name":
                    name = RequireValue(args, ref i);
                    break;
                case "--display-name":
                    displayName = RequireValue(args, ref i);
                    break;
                case "--start":
                    startMode = RequireValue(args, ref i).Trim().ToLowerInvariant();
                    break;
                default:
                    throw new ArgumentException("Unknown service install argument: " + args[i]);
            }
        }

        ValidateName(name);
        ValidateDisplayName(displayName);
        if (startMode is not ("auto" or "manual" or "disabled"))
            throw new ArgumentException("--start must be auto, manual or disabled.");

        if (OperatingSystem.IsWindows())
            return await InstallWindowsAsync(executable, name!, displayName!, startMode).ConfigureAwait(false);
        if (OperatingSystem.IsLinux())
            return await InstallSystemdAsync(executable, name!, displayName!, startMode).ConfigureAwait(false);
        if (OperatingSystem.IsMacOS())
            return await InstallLaunchdAsync(executable, name!, displayName!, startMode).ConfigureAwait(false);

        throw new PlatformNotSupportedException("Service installation is supported on Windows, Linux and macOS.");
    }

    private static async Task<int> InstallWindowsAsync(string executable, string name, string displayName, string startMode)
    {
        var query = await RunProcessAsync("sc.exe", ["query", name]).ConfigureAwait(false);
        if (query.ExitCode == 0)
            throw new InvalidOperationException("A Windows service named '" + name + "' already exists.");

        var startValue = startMode switch
        {
            "auto" => "auto",
            "manual" => "demand",
            _ => "disabled"
        };

        var create = await RunProcessAsync("sc.exe",
        [
            "create", name,
            "binPath=", QuoteForSc(executable),
            "DisplayName=", displayName,
            "start=", startValue
        ]).ConfigureAwait(false);

        EnsureSuccess(create, "Unable to install Windows service '" + name + "'.");
        Console.WriteLine("Installed service: " + name);
        Console.WriteLine("Display name: " + displayName);
        Console.WriteLine("Startup: " + startMode);
        return 0;
    }

    private static async Task<int> InstallSystemdAsync(string executable, string name, string displayName, string startMode)
    {
        var unitName = name + ".service";
        var existing = await RunProcessAsync("systemctl", ["cat", unitName]).ConfigureAwait(false);
        if (existing.ExitCode == 0)
            throw new InvalidOperationException("A systemd service named '" + name + "' already exists.");

        var unitPath = Path.Combine("/etc/systemd/system", unitName);
        if (File.Exists(unitPath))
            throw new InvalidOperationException("A systemd service named '" + name + "' already exists.");

        var disabledPolicy = startMode == "disabled" ? "RefuseManualStart=yes\n" : string.Empty;
        var escapedExecutable = EscapeSystemdArgument(executable);
        var unit = $"""
[Unit]
Description={EscapeSystemdDescription(displayName)}
{disabledPolicy}
[Service]
Type=simple
ExecStart={escapedExecutable}
Restart=no

[Install]
WantedBy=multi-user.target
""";

        await File.WriteAllTextAsync(unitPath, unit, new UTF8Encoding(false)).ConfigureAwait(false);
        EnsureSuccess(await RunProcessAsync("systemctl", ["daemon-reload"]).ConfigureAwait(false), "systemctl daemon-reload failed.");

        if (startMode == "auto")
            EnsureSuccess(await RunProcessAsync("systemctl", ["enable", unitName]).ConfigureAwait(false), "Unable to enable service '" + name + "'.");
        else
            EnsureSuccess(await RunProcessAsync("systemctl", ["disable", unitName]).ConfigureAwait(false), "Unable to disable automatic startup for service '" + name + "'.");

        Console.WriteLine("Installed service: " + name);
        Console.WriteLine("Display name: " + displayName);
        Console.WriteLine("Startup: " + startMode);
        return 0;
    }

    private static async Task<int> InstallLaunchdAsync(string executable, string name, string displayName, string startMode)
    {
        var domainTarget = "system/" + name;
        var existing = await RunProcessAsync("launchctl", ["print", domainTarget]).ConfigureAwait(false);
        if (existing.ExitCode == 0)
            throw new InvalidOperationException("A launchd service named '" + name + "' already exists.");

        var plistPath = Path.Combine("/Library/LaunchDaemons", name + ".plist");
        if (File.Exists(plistPath))
            throw new InvalidOperationException("A launchd service named '" + name + "' already exists.");

        var runAtLoad = startMode == "auto" ? "true" : "false";
        var disabled = startMode == "disabled" ? "true" : "false";
        var plist = $"""
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>{EscapeXml(name)}</string>
    <key>ProgramArguments</key>
    <array>
        <string>{EscapeXml(executable)}</string>
    </array>
    <key>RunAtLoad</key>
    <{runAtLoad}/>
    <key>Disabled</key>
    <{disabled}/>
    <key>XPscriptDisplayName</key>
    <string>{EscapeXml(displayName)}</string>
</dict>
</plist>
""";

        await File.WriteAllTextAsync(plistPath, plist, new UTF8Encoding(false)).ConfigureAwait(false);
        if (startMode != "disabled")
            EnsureSuccess(await RunProcessAsync("launchctl", ["bootstrap", "system", plistPath]).ConfigureAwait(false), "Unable to register launchd service '" + name + "'.");

        Console.WriteLine("Installed service: " + name);
        Console.WriteLine("Display name: " + displayName);
        Console.WriteLine("Startup: " + startMode);
        return 0;
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("service install requires --name.");
        if (!ServiceNamePattern.IsMatch(name))
            throw new ArgumentException("--name must start with a letter or digit and contain only letters, digits, dot, underscore or hyphen. Maximum length is 128 characters.");
    }

    private static void ValidateDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("service install requires --display-name.");
        if (displayName.Length > 256 || displayName.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException("--display-name must be 1 to 256 characters and cannot contain line breaks or NUL.");
    }

    private static string RequireValue(string[] values, ref int index)
    {
        if (++index >= values.Length) throw new ArgumentException(values[index - 1] + " requires a value.");
        return values[index];
    }

    private static string QuoteForSc(string path) => "\"" + path.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string EscapeSystemdArgument(string value) =>
        "\"" + value.Replace("%", "%%", StringComparison.Ordinal).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string EscapeSystemdDescription(string value) => value.Replace("%", "%%", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static void EnsureSuccess(ProcessResult result, string message)
    {
        if (result.ExitCode == 0) return;
        var detail = string.IsNullOrWhiteSpace(result.Stderr) ? result.Stdout : result.Stderr;
        detail = detail.Replace('\r', ' ').Replace('\n', ' ').Trim();
        throw new InvalidOperationException(detail.Length == 0 ? message : message + " " + detail);
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start " + fileName + ".");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    private sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);
}
