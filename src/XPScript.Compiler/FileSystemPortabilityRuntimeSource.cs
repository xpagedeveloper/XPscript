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
            // Do not rewrite directory separators, alter case or resolve symbolic links here.
            // Path.GetFullPath applies the target OS filesystem syntax and leaves
            // case sensitivity and symlink/reparse-point resolution to the filesystem.
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new XPScriptRuntimeException(5, "Invalid file path '" + path + "': " + ex.Message);
        }
    }

    public static string NewLine => Environment.NewLine;

    // FileShare policy is language-defined rather than inherited accidentally from
    // whichever .NET overload happened to be used by a runtime implementation.
    // Input permits other readers/writers. Output/Append permits readers but keeps a
    // single writer. Binary/Random permits multiple read/write handles so explicit
    // XPScript Lock/Unlock can coordinate byte/record regions across processes.
    public static FileStream OpenInputStream(string path) =>
        new(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite
        });

    public static FileStream OpenOutputStream(string path, bool append) =>
        new(path, new FileStreamOptions
        {
            Mode = append ? FileMode.Append : FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.Read
        });

    public static FileStream OpenBinaryStream(string path) =>
        new(path, new FileStreamOptions
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.ReadWrite,
            Share = FileShare.ReadWrite
        });

    public static string FileSharePolicy =>
        "Input=ReadWrite sharing; Output/Append=read sharing with one writer; Binary/Random=ReadWrite sharing with explicit Lock/Unlock coordination";

    public static long FileLen(object? value) => new FileInfo(RequireExistingFile(value)).Length;

    public static DateTime FileDateTime(object? value) => File.GetLastWriteTime(RequireExistingFile(value));

    public static int GetFileAttr(object? value)
    {
        var path = RequireExistingPath(value);
        var attributes = File.GetAttributes(path);

        // Unix-like filesystems conventionally represent hidden files by a leading dot,
        // whereas Windows exposes an explicit Hidden attribute. Synthesize Hidden when
        // reading attributes so portable XPScript code can identify both forms.
        if (!OperatingSystem.IsWindows() && IsDotHidden(path))
            attributes |= FileAttributes.Hidden;

        return (int)attributes;
    }

    public static void SetFileAttr(object? value, int attributesValue)
    {
        var path = RequireExistingPath(value);
        var attributes = (FileAttributes)attributesValue;

        if (!OperatingSystem.IsWindows() && attributes.HasFlag(FileAttributes.Hidden) && !IsDotHidden(path))
            throw new XPScriptRuntimeException(5,
                "Hidden files on Linux/macOS use a leading '.' in the file name. SetFileAttr does not rename files; use Name to rename the file instead.");

        try
        {
            File.SetAttributes(path, attributes);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentException)
        {
            throw new XPScriptRuntimeException(5, "File attributes are not supported for this path/platform: " + ex.Message);
        }
    }

    public static void CopyFile(object? sourceValue, object? destinationValue)
    {
        var source = RequireExistingFile(sourceValue);
        var destination = ResolvePath(destinationValue);
        EnsureDifferentPaths(source, destination, "FileCopy");

        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
            throw new DirectoryNotFoundException("Destination directory does not exist: " + parent);

        File.Copy(source, destination, overwrite: true);

        // File.Copy preserves normal platform metadata where the OS supports it. On Unix,
        // make executable permission preservation explicit when both files are local files.
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(destination, File.GetUnixFileMode(source)); }
            catch (PlatformNotSupportedException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static void DeleteFile(object? value)
    {
        var path = ResolvePath(value);
        if (!File.Exists(path)) return;

        try
        {
            File.Delete(path);
        }
        catch (IOException ex)
        {
            var suffix = OperatingSystem.IsWindows()
                ? " Windows normally prevents deleting a file while an open handle does not grant FileShare.Delete."
                : " Unix-like systems commonly allow unlinking an open file while existing handles keep the inode alive; exact behavior remains filesystem dependent.";
            throw new IOException("Unable to delete file '" + path + "'." + suffix + " " + ex.Message, ex);
        }
    }

    public static void MoveFile(object? sourceValue, object? destinationValue)
    {
        var source = RequireExistingFile(sourceValue);
        var destination = ResolvePath(destinationValue);
        EnsureDifferentPaths(source, destination, "Name");

        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
            throw new DirectoryNotFoundException("Destination directory does not exist: " + parent);

        try
        {
            // Keep this as a real filesystem move/rename. Do not silently convert a failed
            // cross-filesystem rename into copy+delete because that would lose atomicity and
            // may change permissions/ownership/link semantics.
            File.Move(source, destination, overwrite: true);
        }
        catch (IOException ex)
        {
            throw new IOException(
                "Unable to move/rename '" + source + "' to '" + destination +
                "'. Cross-filesystem moves may not be atomic or supported by the underlying filesystem. " + ex.Message, ex);
        }
    }

    public static void MakeDirectory(object? value)
    {
        var path = ResolvePath(value);
        Directory.CreateDirectory(path);
    }

    public static void RemoveDirectory(object? value)
    {
        var path = ResolvePath(value);
        Directory.Delete(path, recursive: false);
    }

    public static void ChangeDirectory(object? value)
    {
        var path = ResolvePath(value);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Directory does not exist: " + path);
        Environment.CurrentDirectory = path;
    }

    public static IEnumerable<string> Enumerate(object? patternValue)
    {
        var raw = XPScriptRuntime.CStr(patternValue);
        if (string.IsNullOrWhiteSpace(raw)) raw = "*";

        var directoryPart = Path.GetDirectoryName(raw);
        var directory = string.IsNullOrEmpty(directoryPart) ? Environment.CurrentDirectory : ResolvePath(directoryPart);
        var mask = Path.GetFileName(raw);
        if (string.IsNullOrEmpty(mask)) mask = "*";

        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Directory does not exist: " + directory);

        // Intentionally do not normalize case. Matching follows the target filesystem/runtime
        // semantics rather than imposing Windows-style case-insensitivity on Unix systems.
        return Directory.EnumerateFileSystemEntries(directory, mask)
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Cast<string>();
    }

    public static bool IsSymbolicLink(object? value)
    {
        var path = ResolvePath(value);
        var info = File.Exists(path) ? (FileSystemInfo)new FileInfo(path) : new DirectoryInfo(path);
        if (!info.Exists) return false;
        return info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0;
    }

    public static bool IsExecutable(object? value)
    {
        var path = RequireExistingFile(value);
        if (OperatingSystem.IsWindows())
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                   extension.Equals(".com", StringComparison.OrdinalIgnoreCase);
        }

        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (PlatformNotSupportedException) { return false; }
    }

    public static string FileSystemComparisonName => OperatingSystem.IsWindows()
        ? "Windows filesystem semantics (normally case-insensitive, case-preserving)"
        : "filesystem-defined case sensitivity";

    private static string RequireExistingFile(object? value)
    {
        var path = ResolvePath(value);
        if (!File.Exists(path)) throw new FileNotFoundException("File not found: " + path, path);
        return path;
    }

    private static string RequireExistingPath(object? value)
    {
        var path = ResolvePath(value);
        if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("Path not found: " + path, path);
        return path;
    }

    private static bool IsDotHidden(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return name.Length > 1 && name[0] == '.' && name is not "." and not "..";
    }

    private static void EnsureDifferentPaths(string source, string destination, string operation)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (source.Equals(destination, comparison))
            throw new XPScriptRuntimeException(5, operation + " source and destination must be different paths.");
    }
}
""";
}
