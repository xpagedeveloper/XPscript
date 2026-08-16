using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace XPScript.Compiler;

internal static class CompilerSecureFileCopy
{
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagSequentialScan = 0x08000000;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const int FileAttributeTagInfo = 9;

    public static void CopyValidatedRegularFile(string sourcePath, string destinationPath, string kind)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);

        RejectLinkedSource(source, kind);

        try
        {
            using var input = OpenReadWithoutFollowingLinks(source, kind);

            // Keep the path-based re-check as a defense-in-depth signal for a pathname that
            // changes after the handle was opened. The actual copy reads only from the already
            // opened no-follow/open-reparse handle.
            RejectLinkedSource(source, kind);

            using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                options: FileOptions.SequentialScan);

            input.CopyTo(output);
            output.Flush(flushToDisk: true);
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            throw new CompilerException(kind + " changed or disappeared before it could be staged.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new CompilerException(kind + " could not be staged because a required directory is unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            throw new CompilerException(kind + " could not be staged because access was denied.");
        }
        catch (IOException)
        {
            throw new CompilerException(kind + " could not be staged safely.");
        }
    }

    private static FileStream OpenReadWithoutFollowingLinks(string sourcePath, string kind)
    {
        if (OperatingSystem.IsWindows())
            return OpenWindowsReadWithoutFollowingReparsePoints(sourcePath, kind);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            return OpenUnixReadWithoutFollowingLinks(sourcePath, kind);

        throw new PlatformNotSupportedException("Secure compiler dependency staging is not implemented for this platform.");
    }

    private static FileStream OpenWindowsReadWithoutFollowingReparsePoints(string sourcePath, string kind)
    {
        var handle = CreateFileW(
            sourcePath,
            GenericRead,
            FileShareRead,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOpenReparsePoint | FileFlagSequentialScan,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new CompilerException(kind + " could not be opened safely for compiler staging.");
        }

        try
        {
            if (!GetFileInformationByHandleEx(
                    handle,
                    FileAttributeTagInfo,
                    out var tagInfo,
                    (uint)Marshal.SizeOf<FileAttributeTagInfoNative>()))
            {
                throw new CompilerException("Unable to safely inspect " + kind + " after opening it for compiler staging.");
            }

            if ((tagInfo.FileAttributes & FileAttributeReparsePoint) != 0)
                throw new CompilerException(kind + " may not be a symbolic link or reparse-point file during compiler staging.");

            if ((tagInfo.FileAttributes & FileAttributeDirectory) != 0)
                throw new CompilerException(kind + " must be a regular file during compiler staging.");

            return new FileStream(handle, FileAccess.Read, bufferSize: 64 * 1024, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static FileStream OpenUnixReadWithoutFollowingLinks(string sourcePath, string kind)
    {
        var noFollow = OperatingSystem.IsLinux() ? 0x00020000 : 0x00000100;
        var fd = open(sourcePath, noFollow);
        if (fd < 0)
            throw new CompilerException(kind + " could not be opened safely without following symbolic links.");

        var handle = new SafeFileHandle(new IntPtr(fd), ownsHandle: true);
        try
        {
            var stream = new FileStream(handle, FileAccess.Read, bufferSize: 64 * 1024, isAsync: false);

            // FileStream.Length forces the opened handle to behave like a seekable regular file.
            // Reject directories and other unsuitable filesystem objects before copying bytes.
            try
            {
                _ = stream.Length;
            }
            catch
            {
                stream.Dispose();
                throw new CompilerException(kind + " must be a regular file during compiler staging.");
            }

            return stream;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static void RejectLinkedSource(string sourcePath, string kind)
    {
        try
        {
            var info = new FileInfo(sourcePath);
            if (!info.Exists)
                throw new CompilerException(kind + " was not found while preparing compiler staging.");

            if (info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new CompilerException(kind + " may not be a symbolic link or reparse-point file during compiler staging.");
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new CompilerException("Unable to safely inspect " + kind + " before compiler staging.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInfoNative
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle hFile,
        int fileInformationClass,
        out FileAttributeTagInfoNative lpFileInformation,
        uint dwBufferSize);

    [DllImport("libc", SetLastError = true, EntryPoint = "open")]
    private static extern int open(string pathname, int flags);
}
