namespace XPScript.Compiler;

internal static class CrossPlatformRuntimeSource
{
    public const string Code = """
internal static class XPCrossPlatformRuntime
{
    public static string Platform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "MacOS";
        if (OperatingSystem.IsFreeBSD()) return "FreeBSD";
        return "Unknown";
    }

    public static int Shell(object? command, object? windowStyle = null)
    {
        var raw = XPScriptRuntime.CStr(command).Trim();
        if (raw.Length == 0)
            throw new XPScriptRuntimeException(5, "Shell requires a program or script name.");

        var parsed = SplitCommand(raw);
        try
        {
            var start = BuildStartInfo(parsed.FileName, parsed.Arguments, windowStyle);
            _ = System.Diagnostics.Process.Start(start)
                ?? throw new FileNotFoundException("Could not start the requested program or script.");
            return 33;
        }
        catch (Exception ex)
        {
            throw LSExtendedErrorRuntime.Normalize(ex);
        }
    }

    private static System.Diagnostics.ProcessStartInfo BuildStartInfo(string fileName, string arguments, object? windowStyle)
    {
        fileName = ResolveRequestedProgram(fileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var info = new System.Diagnostics.ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = false
        };

        if (OperatingSystem.IsWindows())
        {
            if (extension is ".cmd" or ".bat")
            {
                var batchArguments = ParseArguments(arguments);
                ValidateBatchFileName(fileName);
                foreach (var argument in batchArguments) ValidateBatchArgument(argument);

                info.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                info.ArgumentList.Add("/d");
                info.ArgumentList.Add("/c");
                info.ArgumentList.Add("call");
                info.ArgumentList.Add(fileName);
                foreach (var argument in batchArguments) info.ArgumentList.Add(argument);
            }
            else if (extension == ".ps1")
            {
                info.FileName = ResolveWindowsPowerShell();
                info.ArgumentList.Add("-NoLogo");
                info.ArgumentList.Add("-NoProfile");
                info.ArgumentList.Add("-File");
                info.ArgumentList.Add(fileName);
                AddArguments(info, arguments);
            }
            else
            {
                info.FileName = fileName;
                AddArguments(info, arguments);
            }

            if (windowStyle is not null)
            {
                info.WindowStyle = XPScriptRuntime.CInt(windowStyle) switch
                {
                    0 => System.Diagnostics.ProcessWindowStyle.Hidden,
                    2 => System.Diagnostics.ProcessWindowStyle.Minimized,
                    3 => System.Diagnostics.ProcessWindowStyle.Maximized,
                    _ => System.Diagnostics.ProcessWindowStyle.Normal
                };
            }
            return info;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
        {
            if (extension == ".ps1")
            {
                info.FileName = ResolveUnixPowerShell();
                info.ArgumentList.Add("-NoLogo");
                info.ArgumentList.Add("-NoProfile");
                info.ArgumentList.Add("-File");
                info.ArgumentList.Add(fileName);
                AddArguments(info, arguments);
                return info;
            }

            if (extension is ".sh" or ".bash")
            {
                info.FileName = "/bin/sh";
                info.ArgumentList.Add(fileName);
                AddArguments(info, arguments);
                return info;
            }

            info.FileName = fileName;
            AddArguments(info, arguments);
            return info;
        }

        throw new PlatformNotSupportedException("Shell is not implemented for platform: " + Platform());
    }

    private static string ResolveRequestedProgram(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new XPScriptRuntimeException(5, "Shell requires a program or script name.");

        try
        {
            if (Path.IsPathRooted(fileName) || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
            {
                var explicitPath = Path.GetFullPath(fileName);
                if (!File.Exists(explicitPath))
                    throw new XPScriptRuntimeException(53, "Requested program or script was not found.");
                return explicitPath;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new XPScriptRuntimeException(5, "Requested program or script path is invalid.");
        }

        var resolved = ResolveFromAbsolutePath(fileName, includeWindowsExtensions: OperatingSystem.IsWindows());
        if (resolved is null)
            throw new XPScriptRuntimeException(53, "Requested program or script was not found in absolute PATH locations.");
        return resolved;
    }

    private static string ResolveWindowsPowerShell()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            var pwsh = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwsh)) return pwsh;
        }

        var fromPath = ResolveFromAbsolutePath("pwsh.exe", includeWindowsExtensions: false);
        if (fromPath is not null) return fromPath;

        var windowsPowerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(windowsPowerShell)) return windowsPowerShell;
        throw new XPScriptRuntimeException(53, "PowerShell executable was not found.");
    }

    private static string ResolveUnixPowerShell()
    {
        foreach (var candidate in new[] { "/usr/bin/pwsh", "/usr/local/bin/pwsh", "/opt/homebrew/bin/pwsh" })
            if (File.Exists(candidate)) return candidate;

        return ResolveFromAbsolutePath("pwsh", includeWindowsExtensions: false)
            ?? throw new XPScriptRuntimeException(53, "PowerShell executable was not found.");
    }

    private static string? ResolveFromAbsolutePath(string executableName, bool includeWindowsExtensions)
    {
        var names = CandidateExecutableNames(executableName, includeWindowsExtensions);
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var rawDirectory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var directory = rawDirectory.Trim().Trim('"');
                if (!Path.IsPathRooted(directory)) continue;
                directory = Path.GetFullPath(directory);
                foreach (var name in names)
                {
                    var candidate = Path.Combine(directory, name);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }
        }
        return null;
    }

    private static IReadOnlyList<string> CandidateExecutableNames(string executableName, bool includeWindowsExtensions)
    {
        if (!includeWindowsExtensions || Path.HasExtension(executableName)) return [executableName];

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.StartsWith('.', StringComparison.Ordinal) && x.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\0']) < 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return extensions.Length == 0
            ? [executableName + ".exe", executableName + ".com", executableName + ".cmd", executableName + ".bat"]
            : extensions.Select(x => executableName + x).ToArray();
    }

    private static void AddArguments(System.Diagnostics.ProcessStartInfo info, string rawArguments)
    {
        foreach (var argument in ParseArguments(rawArguments))
            info.ArgumentList.Add(argument);
    }

    private static IReadOnlyList<string> ParseArguments(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var tokenStarted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                tokenStarted = true;
                if (i > 0 && text[i - 1] == '\\')
                {
                    if (current.Length > 0) current.Length--;
                    current.Append('"');
                }
                else inQuotes = !inQuotes;
                continue;
            }
            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (tokenStarted)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }
                continue;
            }
            tokenStarted = true;
            current.Append(c);
        }
        if (inQuotes) throw new XPScriptRuntimeException(5, "Unterminated quoted Shell argument.");
        if (tokenStarted) result.Add(current.ToString());
        return result;
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        if (command[0] == '"')
        {
            var close = command.IndexOf('"', 1);
            if (close < 0) throw new XPScriptRuntimeException(5, "Unterminated executable quote in Shell command.");
            return (command[1..close], command[(close + 1)..].TrimStart());
        }
        var space = command.IndexOf(' ');
        return space < 0 ? (command, "") : (command[..space], command[(space + 1)..].TrimStart());
    }

    private static void ValidateBatchFileName(string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0', '"']) >= 0)
            throw new XPScriptRuntimeException(5, "Batch script path contains unsupported command-shell characters.");
    }

    private static void ValidateBatchArgument(string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0', '"', '&', '|', '<', '>', '^', '%', '!']) >= 0)
            throw new XPScriptRuntimeException(5, "Batch script arguments may not contain command-shell metacharacters. Use a directly executable program or PowerShell script for structured arguments.");
    }
}
""";
}
