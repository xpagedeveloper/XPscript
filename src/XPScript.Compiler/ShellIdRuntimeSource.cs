namespace XPScript.Compiler;

internal static class ShellIdRuntimeSource
{
    public const string Code = """
internal static class XPShellIdRuntime
{
    public static int ShellId(object? command, object? windowStyle = null)
    {
        var raw = XPScriptRuntime.CStr(command).Trim();
        if (raw.Length == 0)
            throw new XPScriptRuntimeException(5, "Shellid requires a program or script name.");

        var parsed = SplitCommand(raw);
        try
        {
            var info = BuildStartInfo(parsed.FileName, ParseArguments(parsed.Arguments), windowStyle);
            using var process = System.Diagnostics.Process.Start(info)
                ?? throw new FileNotFoundException("Could not start the requested program or script.");

            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
                return process.Id;
            return 33;
        }
        catch (Exception ex)
        {
            throw LSExtendedErrorRuntime.Normalize(ex);
        }
    }

    private static System.Diagnostics.ProcessStartInfo BuildStartInfo(string fileName, IReadOnlyList<string> arguments, object? windowStyle)
    {
        fileName = ResolveProgram(fileName);
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
                info.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                info.ArgumentList.Add("/d");
                info.ArgumentList.Add("/s");
                info.ArgumentList.Add("/c");
                info.ArgumentList.Add(fileName);
                foreach (var argument in arguments) info.ArgumentList.Add(argument);
            }
            else if (extension == ".ps1")
            {
                info.FileName = ResolvePowerShell();
                info.ArgumentList.Add("-NoLogo");
                info.ArgumentList.Add("-NoProfile");
                info.ArgumentList.Add("-File");
                info.ArgumentList.Add(fileName);
                foreach (var argument in arguments) info.ArgumentList.Add(argument);
            }
            else
            {
                info.FileName = fileName;
                foreach (var argument in arguments) info.ArgumentList.Add(argument);
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
                info.FileName = ResolvePowerShell();
                info.ArgumentList.Add("-NoLogo");
                info.ArgumentList.Add("-NoProfile");
                info.ArgumentList.Add("-File");
                info.ArgumentList.Add(fileName);
            }
            else if (extension is ".sh" or ".bash")
            {
                info.FileName = "/bin/sh";
                info.ArgumentList.Add(fileName);
            }
            else
            {
                info.FileName = fileName;
            }
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            return info;
        }

        throw new PlatformNotSupportedException("Shellid is not available on platform: " + XPCrossPlatformRuntime.Platform());
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        if (command[0] == '"')
        {
            var close = command.IndexOf('"', 1);
            if (close < 0) throw new XPScriptRuntimeException(5, "Unterminated executable quote in Shellid command.");
            return (command[1..close], command[(close + 1)..].TrimStart());
        }
        var split = command.IndexOfAny([' ', '\t']);
        return split < 0 ? (command, "") : (command[..split], command[(split + 1)..].TrimStart());
    }

    private static IReadOnlyList<string> ParseArguments(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var result = new List<string>();
        var token = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '"')
            {
                if (quoted && i + 1 < raw.Length && raw[i + 1] == '"')
                {
                    token.Append('"');
                    i++;
                }
                else quoted = !quoted;
                continue;
            }
            if (!quoted && char.IsWhiteSpace(c))
            {
                if (token.Length > 0)
                {
                    result.Add(token.ToString());
                    token.Clear();
                }
                continue;
            }
            token.Append(c);
        }
        if (quoted) throw new XPScriptRuntimeException(5, "Unterminated argument quote in Shellid command.");
        if (token.Length > 0) result.Add(token.ToString());
        return result;
    }

    private static string ResolveProgram(string fileName)
    {
        if (Path.IsPathRooted(fileName) || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
        {
            var full = Path.GetFullPath(fileName);
            if (!File.Exists(full)) throw new FileNotFoundException("Requested program or script was not found.", full);
            return full;
        }

        var names = new List<string> { fileName };
        if (OperatingSystem.IsWindows() && string.IsNullOrEmpty(Path.GetExtension(fileName)))
        {
            var pathext = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            names.AddRange(pathext.Select(ext => fileName + ext.ToLowerInvariant()));
            names.AddRange(pathext.Select(ext => fileName + ext.ToUpperInvariant()));
        }

        foreach (var rawDirectory in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var directory = rawDirectory.Trim().Trim('"');
            if (!Path.IsPathRooted(directory)) continue;
            foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var candidate = Path.Combine(directory, name);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
        }
        throw new FileNotFoundException("Requested program or script was not found in PATH.", fileName);
    }

    private static string ResolvePowerShell()
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"), Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe") }
            : new[] { "/usr/bin/pwsh", "/usr/local/bin/pwsh", "/opt/homebrew/bin/pwsh" };
        foreach (var candidate in candidates)
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) return candidate;
        return ResolveProgram(OperatingSystem.IsWindows() ? "pwsh.exe" : "pwsh");
    }
}
""";
}
