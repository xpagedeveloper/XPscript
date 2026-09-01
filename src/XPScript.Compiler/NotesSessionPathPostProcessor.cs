namespace XPScript.Compiler;

internal static class NotesSessionPathPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    private static string? TryResolveDefaultRuntimeDirectory()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\HCL\Notes\Installer");
                var raw = key?.GetValue("PROGDIR") as string;
                if (string.IsNullOrWhiteSpace(raw)) return null;

                var expanded = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));
                var path = Path.GetFullPath(expanded);
                if (!Directory.Exists(path)) return null;
                if (!File.Exists(Path.Combine(path, "nnotes.dll"))) return null;
                return path;
            }

            if (OperatingSystem.IsMacOS())
            {
                const string path = "/Applications/HCL Notes.app/Contents/MacOS";
                if (!Directory.Exists(path)) return null;
                if (!File.Exists(Path.Combine(path, "libnotes.dylib")) &&
                    !File.Exists(Path.Combine(path, "libnotes64.dylib"))) return null;
                return path;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
""",
            """
    private static string? TryResolveDefaultRuntimeDirectory()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var notesPath = TryResolveWindowsRuntimeDirectory(@"Software\HCL\Notes\Installer");
                if (notesPath is not null) return notesPath;
                return TryResolveWindowsRuntimeDirectory(@"Software\HCL\Domino\Installer");
            }

            if (OperatingSystem.IsMacOS())
            {
                const string path = "/Applications/HCL Notes.app/Contents/MacOS";
                if (!Directory.Exists(path)) return null;
                if (!File.Exists(Path.Combine(path, "libnotes.dylib")) &&
                    !File.Exists(Path.Combine(path, "libnotes64.dylib"))) return null;
                return path;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryResolveWindowsRuntimeDirectory(string registryPath)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath);
        var raw = key?.GetValue("PROGDIR") as string;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var expanded = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));
        var path = Path.GetFullPath(expanded);
        if (!Directory.Exists(path)) return null;
        if (!File.Exists(Path.Combine(path, "nnotes.dll"))) return null;
        return path;
    }
""",
            "windows-notes-domino-runtime-order");

        source = ReplaceRequired(
            source,
            """
            RuntimeDirectory = resolvedRuntimeDirectory;
            NotesIni = string.IsNullOrWhiteSpace(notesIni) ? "" : Path.GetFullPath(notesIni);
            Api = new XPScriptNotesNativeApi(RuntimeDirectory);
""",
            """
            RuntimeDirectory = resolvedRuntimeDirectory;
            NotesIni = string.IsNullOrWhiteSpace(notesIni) ? "" : Path.GetFullPath(notesIni);
            var resolvedIniPath = ResolveNotesIniPath(RuntimeDirectory, NotesIni);
            IniDir = resolvedIniPath.Length == 0 ? "" : (Path.GetDirectoryName(resolvedIniPath) ?? "");
            DataDir = resolvedIniPath.Length == 0 ? "" : ReadNotesIniDataDirectory(resolvedIniPath, IniDir);
            Api = new XPScriptNotesNativeApi(RuntimeDirectory);
""",
            "session-path-values");

        source = ReplaceRequired(
            source,
            """
    public string RuntimeDirectory { get; }
    public string NotesIni { get; }
""",
            """
    public string RuntimeDirectory { get; }
    public string ProgramDir => RuntimeDirectory;
    public string NotesIni { get; }
    public string IniDir { get; }
    public string DataDir { get; }
""",
            "session-path-properties");

        source = ReplaceRequired(
            source,
            """
    private static string ExtractCommonUsername(string canonical)
""",
            """
    private static string ResolveNotesIniPath(string runtimeDirectory, string explicitNotesIni)
    {
        if (explicitNotesIni.Length > 0 && File.Exists(explicitNotesIni)) return explicitNotesIni;

        var environmentIni = Environment.GetEnvironmentVariable("NOTESINI");
        if (!string.IsNullOrWhiteSpace(environmentIni))
        {
            try
            {
                var path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(environmentIni.Trim().Trim('"')));
                if (File.Exists(path)) return path;
            }
            catch { }
        }

        foreach (var candidate in EnumerateNotesIniCandidates(runtimeDirectory))
        {
            try
            {
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }
            catch { }
        }

        return "";
    }

    private static IEnumerable<string> EnumerateNotesIniCandidates(string runtimeDirectory)
    {
        yield return Path.Combine(runtimeDirectory, "notes.ini");
        yield return Path.Combine(Environment.CurrentDirectory, "notes.ini");

        if (!OperatingSystem.IsWindows()) yield break;

        foreach (var registryPath in new[] { @"Software\HCL\Notes\Installer", @"Software\HCL\Domino\Installer" })
        {
            string? dataDirectory = null;
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath);
                dataDirectory = key?.GetValue("DATADIR") as string;
            }
            catch { }

            if (string.IsNullOrWhiteSpace(dataDirectory)) continue;
            string? expanded = null;
            try { expanded = Environment.ExpandEnvironmentVariables(dataDirectory.Trim().Trim('"')); }
            catch { }
            if (!string.IsNullOrWhiteSpace(expanded)) yield return Path.Combine(expanded, "notes.ini");
        }
    }

    private static string ReadNotesIniDataDirectory(string iniPath, string iniDirectory)
    {
        try
        {
            foreach (var raw in File.ReadLines(iniPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal)) continue;
                const string key = "Directory=";
                if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;

                var value = Environment.ExpandEnvironmentVariables(line[key.Length..].Trim().Trim('"'));
                if (value.Length == 0) return "";
                if (!Path.IsPathRooted(value) && iniDirectory.Length > 0)
                    value = Path.Combine(iniDirectory, value);
                return Path.GetFullPath(value);
            }
        }
        catch { }
        return "";
    }

    private static string ExtractCommonUsername(string canonical)
""",
            "session-ini-helpers");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (source.Contains(oldValue, StringComparison.Ordinal))
            return source.Replace(oldValue, newValue, StringComparison.Ordinal);
        if (source.Contains(newValue, StringComparison.Ordinal))
            return source;
        throw new CompilerException("Unable to apply NotesSession path patch (" + stage + ").");
    }
}
