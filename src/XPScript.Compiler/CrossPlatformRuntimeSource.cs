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
                ?? throw new FileNotFoundException("Could not start program or script: " + parsed.FileName);
            return 33;
        }
        catch (Exception ex)
        {
            throw LSExtendedErrorRuntime.Normalize(ex);
        }
    }

    private static System.Diagnostics.ProcessStartInfo BuildStartInfo(string fileName, string arguments, object? windowStyle)
    {
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
                info.FileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
                info.ArgumentList.Add("/d");
                info.ArgumentList.Add("/s");
                info.ArgumentList.Add("/c");
                info.ArgumentList.Add(QuoteWindowsCommand(fileName, arguments));
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
                info.FileName = "pwsh";
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

            // Executable binaries and scripts with an executable bit/shebang are started directly.
            info.FileName = fileName;
            AddArguments(info, arguments);
            return info;
        }

        throw new PlatformNotSupportedException("Shell is not implemented for platform: " + Platform());
    }

    private static string ResolveWindowsPowerShell()
    {
        // Prefer cross-platform PowerShell when installed, otherwise use Windows PowerShell.
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), "pwsh.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return "powershell.exe";
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
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
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
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(c);
        }
        if (inQuotes) throw new XPScriptRuntimeException(5, "Unterminated quoted Shell argument.");
        if (current.Length > 0) result.Add(current.ToString());
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

    private static string QuoteWindowsCommand(string fileName, string arguments)
    {
        var command = "\"" + fileName.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        if (!string.IsNullOrWhiteSpace(arguments)) command += " " + arguments;
        return command;
    }
}
""";
}
