namespace XPScript.Compiler;

internal static class FileSystemPortabilityRuntimeSource
{
    public const string Code = """
internal static class XPScriptFileSystemRuntime
{
    public static Encoding LegacyEncoding { get; } = Encoding.Latin1;
    private static string _scriptDirectory = Environment.CurrentDirectory;

    public static void SetScriptDirectory(string directory)
    {
        if (!string.IsNullOrWhiteSpace(directory))
            _scriptDirectory = Path.GetFullPath(directory);
    }

    private const int DarwinOpenReadWrite = 0x0002;
    private const int DarwinOpenReadOnly = 0x0000;
    private const int DarwinOpenNoFollow = 0x00000100;
    private const int DarwinOpenCloseOnExec = 0x01000000;
    private const int LinuxOpenReadOnly = 0x0000;
    private const int LinuxOpenNoFollow = 0x00020000;
    private const int LinuxOpenCloseOnExec = 0x00080000;
    private const uint WindowsGenericRead = 0x80000000;
    private const uint WindowsFileShareRead = 0x00000001;
    private const uint WindowsOpenExisting = 3;
    private const uint WindowsFileAttributeNormal = 0x00000080;
    private const uint WindowsFileFlagOpenReparsePoint = 0x00200000;
    private const uint WindowsFileAttributeDirectory = 0x00000010;
    private const uint WindowsFileAttributeReparsePoint = 0x00000400;

    [System.Runtime.InteropServices.DllImport("libSystem.B.dylib", EntryPoint = "open", SetLastError = true)]
    private static extern int DarwinOpenExisting(string path, int flags);

    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int LinuxOpenExisting(string path, int flags);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        Microsoft.Win32.SafeHandles.SafeFileHandle file,
        out WindowsByHandleFileInformation information);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct WindowsByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    public static string ResolvePath(object? value)
    {
        var path = XPScriptRuntime.CStr(value);
        if (string.IsNullOrWhiteSpace(path))
            throw new XPScriptRuntimeException(5, "File path must not be empty.");
        try { return Path.GetFullPath(path, _scriptDirectory); }
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

    public static FileStream OpenBinaryStream(string path)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.ReadWrite
            });
        }

        // .NET implements Unix FileShare with a whole-file flock even for FileShare.ReadWrite.
        // Darwin documents flock and lockf/fcntl record locks as interacting locking interfaces;
        // keeping that implicit flock on the same process causes an otherwise valid byte-range
        // lockf(F_TLOCK) to fail with EAGAIN. Binary/Random semantics already permit ReadWrite
        // sharing and require explicit Lock/Unlock coordination, so use a native descriptor after
        // any required creation has completed and its temporary managed FileStream is closed.
        //
        // Do not P/Invoke open(..., O_CREAT, mode) here: open is variadic when O_CREAT is present,
        // and Darwin ARM64 vararg ABI handling can corrupt the mode argument. CreateNew is used only
        // to bootstrap a missing file atomically; an existing file is never truncated or rewritten.
        if (!File.Exists(path))
        {
            try
            {
                using var bootstrap = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Another process won the create race. Continue with the existing file.
            }
        }

        var flags = DarwinOpenReadWrite | DarwinOpenCloseOnExec;
        var fd = DarwinOpenExisting(path, flags);
        if (fd < 0)
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastPInvokeError();
            throw new IOException("Unable to open Binary/Random file on macOS. errno=" + error.ToString(CultureInfo.InvariantCulture));
        }

        var handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(new IntPtr(fd), ownsHandle: true);
        try
        {
            return new FileStream(handle, FileAccess.ReadWrite);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

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

    public static void CopyFile(object? sourceValue, object? destinationValue, bool overwrite = true)
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
            // The security boundary is the open itself. Unix uses O_NOFOLLOW. Windows opens
            // the reparse point rather than its target and validates the resulting handle.
            // After this point both bytes and Unix mode are read from the already-open handle.
            using (var input = OpenFileCopySource(source))
            {
                UnixFileMode? unixMode = null;
                if (!OperatingSystem.IsWindows())
                {
                    try { unixMode = File.GetUnixFileMode(input.SafeFileHandle); }
                    catch (PlatformNotSupportedException) { }
                    catch (UnauthorizedAccessException) { }
                }

                using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    input.CopyTo(output);

                if (unixMode.HasValue)
                {
                    try { File.SetUnixFileMode(stage, unixMode.Value); }
                    catch (PlatformNotSupportedException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }

            // Revalidate the destination immediately before publication. Moving the staged
            // regular file avoids writing through an existing destination symlink.
            RejectLinkedPath(destination, "FileCopy", "destination");
            File.Move(stage, destination, overwrite: overwrite);
        }
        finally
        {
            try { if (File.Exists(stage)) File.Delete(stage); } catch { }
        }
    }

    private static FileStream OpenFileCopySource(string source)
    {
        if (OperatingSystem.IsWindows())
        {
            var handle = CreateFileW(
                source,
                WindowsGenericRead,
                WindowsFileShareRead,
                IntPtr.Zero,
                WindowsOpenExisting,
                WindowsFileAttributeNormal | WindowsFileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                throw new IOException("Unable to safely open FileCopy source.");
            }

            try
            {
                if (!GetFileInformationByHandle(handle, out var information))
                    throw new IOException("Unable to safely inspect FileCopy source handle.");
                if ((information.FileAttributes & WindowsFileAttributeReparsePoint) != 0)
                    throw new XPScriptRuntimeException(5, "FileCopy refuses a symbolic-link or reparse-point source.");
                if ((information.FileAttributes & WindowsFileAttributeDirectory) != 0)
                    throw new XPScriptRuntimeException(5, "FileCopy source must be a regular file.");
                return new FileStream(handle, FileAccess.Read);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        var fd = OperatingSystem.IsMacOS()
            ? DarwinOpenExisting(source, DarwinOpenReadOnly | DarwinOpenNoFollow | DarwinOpenCloseOnExec)
            : LinuxOpenExisting(source, LinuxOpenReadOnly | LinuxOpenNoFollow | LinuxOpenCloseOnExec);
        if (fd < 0)
            throw new XPScriptRuntimeException(5, "FileCopy source could not be safely opened without following symbolic links.");

        var unixHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(new IntPtr(fd), ownsHandle: true);
        try
        {
            return new FileStream(unixHandle, FileAccess.Read);
        }
        catch
        {
            unixHandle.Dispose();
            throw;
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

    public static void MoveFile(object? sourceValue, object? destinationValue, bool overwrite = true)
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
            File.Move(source, destination, overwrite: overwrite);
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
        var directory = string.IsNullOrEmpty(directoryPart) ? _scriptDirectory : ResolvePath(directoryPart);
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
