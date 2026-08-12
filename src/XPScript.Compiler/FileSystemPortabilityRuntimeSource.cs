namespace XPScript.Compiler;

internal static class FileSystemPortabilityRuntimeSource
{
    public const string Code = """
internal static class XPScriptFileSystemRuntime
{
    public static Encoding LegacyEncoding { get; } = Encoding.Latin1;

    public static string ResolvePath(object? value)
    {
        var path = XPScriptRuntime.CStr(value);
        if (string.IsNullOrWhiteSpace(path))
            throw new XPScriptRuntimeException(5, "File path must not be empty.");
        try { return Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new XPScriptRuntimeException(5, "Invalid file path.");
        }
    }

    public static string NewLine => Environment.NewLine;

    public static FileStream OpenInputStream(string path) => new(path, new FileStreamOptions
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.ReadWrite
    });

    public static FileStream OpenOutputStream(string path, bool append) => new(path, new FileStreamOptions
    {
        Mode = append ? FileMode.Append : FileMode.Create,
        Access = FileAccess.Write,
        Share = FileShare.Read
    });

    public static FileStream OpenBinaryStream(string path) => new(path, new FileStreamOptions
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
        if (!OperatingSystem.IsWindows() && IsDotHidden(path)) attributes |= FileAttributes.Hidden;
        return (int)attributes;
    }

    public static void SetFileAttr(object? value, int attributesValue)
    {
        var path = RequireExistingPath(value);
        var attributes = (FileAttributes)attributesValue;
        if (!OperatingSystem.IsWindows() && attributes.HasFlag(FileAttributes.Hidden) && !IsDotHidden(path))
            throw new XPScriptRuntimeException(5,
                "Hidden files on Linux/macOS use a leading '.' in the file name. SetFileAttr does not rename files; use Name to rename the file instead.");
        try { File.SetAttributes(path, attributes); }
        catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentException)
        {
            throw new XPScriptRuntimeException(5, "File attributes are not supported for this path or platform.");
        }
    }

    public static void CopyFile(object? sourceValue, object? destinationValue)
    {
        var source = RequireExistingFile(sourceValue);
        var destination = ResolvePath(destinationValue);
        EnsureDifferentPaths(source, destination, "FileCopy");
        RejectLinkedPath(source, "FileCopy", "source");
        RejectLinkedPath(destination, "FileCopy", "destination");

        var parent = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
            throw new DirectoryNotFoundException("Destination directory does not exist.");

        var stage = Path.Combine(parent, ".xps-copy-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            // Revalidate immediately before opening the source. Once open, copying uses the
            // handle rather than reopening the source pathname, reducing path-swap races.
            RejectLinkedPath(source, "FileCopy", "source");
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                input.CopyTo(output);

            if (!OperatingSystem.IsWindows())
            {
                try { File.SetUnixFileMode(stage, File.GetUnixFileMode(source)); }
                catch (PlatformNotSupportedException) { }
                catch (UnauthorizedAccessException) { }
            }

            // Revalidate the destination immediately before publication. Moving the staged
            // regular file avoids writing through an existing destination symlink.
            RejectLinkedPath(destination, "FileCopy", "destination");
            File.Move(stage, destination, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(stage)) File.Delete(stage); } catch { }
        }
    }

    public static void DeleteFile(object? value)
    {
        var path = ResolvePath(value);
        if (!File.Exists(path)) return;
        RejectLinkedPath(path, "Kill", "target");
        try
        {
            RejectLinkedPath(path, "Kill", "target");
            File.Delete(path);
        }
        catch (IOException ex)
        {
            var suffix = OperatingSystem.IsWindows()
                ? " Windows normally prevents deleting a file while an open handle does not grant FileShare.Delete."
                : " Unix-like systems commonly allow unlinking an open file while existing handles keep the inode alive; exact behavior remains filesystem dependent.";
            throw new IOException("Unable to delete file." + suffix, ex);
        }
    }

    public static void MoveFile(object? sourceValue, object? destinationValue)
    {
        var source = RequireExistingFile(sourceValue);
        var destination = ResolvePath(destinationValue);
        EnsureDifferentPaths(source, destination, "Name");
        RejectLinkedPath(source, "Name", "source");
        RejectLinkedPath(destination, "Name", "destination");
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(parent) && !Directory.Exists(parent))
            throw new DirectoryNotFoundException("Destination directory does not exist.");
        try
        {
            RejectLinkedPath(source, "Name", "source");
            RejectLinkedPath(destination, "Name", "destination");
            File.Move(source, destination, overwrite: true);
        }
        catch (IOException ex)
        {
            throw new IOException(
                "Unable to move or rename file. Cross-filesystem moves may not be atomic or supported by the underlying filesystem.", ex);
        }
    }

    public static void MakeDirectory(object? value) => Directory.CreateDirectory(ResolvePath(value));

    public static void RemoveDirectory(object? value)
    {
        var path = ResolvePath(value);
        RejectLinkedPath(path, "RmDir", "target");
        RejectLinkedPath(path, "RmDir", "target");
        Directory.Delete(path, recursive: false);
    }

    public static void ChangeDirectory(object? value)
    {
        var path = ResolvePath(value);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Directory does not exist.");
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
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Directory does not exist.");
        return Directory.EnumerateFileSystemEntries(directory, mask)
            .Select(Path.GetFileName).Where(x => x is not null).Cast<string>();
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
        if (!File.Exists(path)) throw new FileNotFoundException("File not found.");
        return path;
    }

    private static string RequireExistingPath(object? value)
    {
        var path = ResolvePath(value);
        if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("Path not found.");
        return path;
    }

    private static void RejectLinkedPath(string path, string operation, string role)
    {
        try
        {
            var file = new FileInfo(path);
            if (file.LinkTarget is not null || (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0))
                throw new XPScriptRuntimeException(5, operation + " refuses a symbolic-link or reparse-point " + role + ".");
            var directory = new DirectoryInfo(path);
            if (directory.LinkTarget is not null || (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0))
                throw new XPScriptRuntimeException(5, operation + " refuses a symbolic-link or reparse-point " + role + ".");
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new XPScriptRuntimeException(5, operation + " could not safely inspect the " + role + " path.");
        }
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
