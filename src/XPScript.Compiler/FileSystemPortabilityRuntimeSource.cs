namespace XPScript.Compiler;

internal static class FileSystemPortabilityRuntimeSource
{
    public const string Code = """
internal static class XPScriptFileSystemRuntime
{
    // XPScript's implicit legacy byte/text encoding is defined explicitly so
    // file behavior is reproducible across Windows, Linux and macOS.
    // Use Charset "utf-8" (or another explicit charset) when Unicode text is intended.
    public static Encoding LegacyEncoding { get; } = Encoding.Latin1;

    public static string ResolvePath(object? value)
    {
        var path = XPScriptRuntime.CStr(value);
        if (string.IsNullOrWhiteSpace(path))
            throw new XPScriptRuntimeException(5, "File path must not be empty.");

        try
        {
            // Do not rewrite directory separators or resolve symbolic links here.
            // Path.GetFullPath applies the target OS filesystem syntax and leaves
            // symlink/reparse-point resolution to the operating system.
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new XPScriptRuntimeException(5, "Invalid file path '" + path + "': " + ex.Message);
        }
    }

    public static string NewLine => Environment.NewLine;

    public static bool IsSymbolicLink(object? value)
    {
        var path = ResolvePath(value);
        var info = File.Exists(path) ? (FileSystemInfo)new FileInfo(path) : new DirectoryInfo(path);
        if (!info.Exists) return false;
        return info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0;
    }

    public static string FileSystemComparisonName => OperatingSystem.IsWindows() ? "case-insensitive by default" : "filesystem-defined case sensitivity";
}
""";
}
