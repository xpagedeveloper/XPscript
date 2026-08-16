using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace XPScript.Compiler;

internal static class CompilerPathSecurity
{
    public static string ResolveProjectLocalFile(string sourceDirectory, string declaredPath, string kind)
    {
        if (string.IsNullOrWhiteSpace(declaredPath))
            throw new CompilerException(kind + " path cannot be empty.");

        var root = Path.GetFullPath(sourceDirectory);
        var portable = declaredPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(portable))
            throw new CompilerException(kind + " path must be relative to and remain inside the XPScript source directory: " + declaredPath);

        var resolved = Path.GetFullPath(Path.Combine(root, portable));
        EnsureLexicallyContained(root, resolved, kind, declaredPath);
        EnsureNoLinkEscape(root, resolved, kind, declaredPath);
        return resolved;
    }

    public static string ResolveApplicationLocalNativeFile(string sourceDirectory, string declaredPath)
    {
        if (string.IsNullOrWhiteSpace(declaredPath))
            throw new CompilerException("Application-local native dependency path cannot be empty.");

        var root = Path.GetFullPath(sourceDirectory);
        var portable = declaredPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(portable))
            throw new CompilerException("Application-local native dependency paths must be relative to the XPScript source directory: " + declaredPath);

        var resolved = Path.GetFullPath(Path.Combine(root, portable));
        EnsureLexicallyContained(root, resolved, "Application-local native dependency", declaredPath);
        EnsureNoLinkEscape(root, resolved, "Application-local native dependency", declaredPath);
        return resolved;
    }

    public static string CreateOwnedTemporaryDirectory(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Temporary directory prefix must not be empty.", nameof(prefix));

        var logicalRoot = Path.Combine(Path.GetTempPath(), "XPScript");
        Directory.CreateDirectory(logicalRoot);
        var physicalRoot = CanonicalizeExistingDirectory(logicalRoot);
        var directory = Path.Combine(physicalRoot, prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        HardenTemporaryDirectory(directory);
        return directory;
    }

    public static void HardenTemporaryDirectory(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            HardenWindowsDirectoryAcl(path);
            return;
        }

        try
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }
        catch (PlatformNotSupportedException) { }
        catch (UnauthorizedAccessException)
        {
            throw new CompilerException("Unable to secure compiler temporary workspace permissions.");
        }
    }

    public static void HardenTemporaryFile(string path)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException) { }
        catch (UnauthorizedAccessException)
        {
            throw new CompilerException("Unable to secure compiler temporary file permissions.");
        }
    }

    public static void DeleteOwnedTemporaryDirectory(string path)
    {
        var full = CanonicalizeExistingDirectory(Path.GetFullPath(path));
        var logicalTempRoot = Path.Combine(Path.GetTempPath(), "XPScript");
        if (!Directory.Exists(logicalTempRoot)) return;
        var tempRoot = CanonicalizeExistingDirectory(logicalTempRoot);
        EnsureLexicallyContained(tempRoot, full, "Compiler temporary workspace", full);

        if (!Directory.Exists(full)) return;

        var rootInfo = new DirectoryInfo(full);
        if (IsLinkOrReparsePoint(rootInfo))
            throw new CompilerException("Refusing to recursively clean a compiler temporary workspace that is itself a symbolic link or reparse point.");

        DeleteDirectoryWithoutFollowingLinks(rootInfo);
    }

    private static string CanonicalizeExistingDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows() || !Directory.Exists(full))
            return full;

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root))
            return full;

        var current = root;
        var relative = Path.GetRelativePath(root, full);
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var info = new DirectoryInfo(current);
            if (!info.Exists || !IsLinkOrReparsePoint(info))
                continue;

            try
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
                if (!string.IsNullOrWhiteSpace(target))
                    current = Path.GetFullPath(target);
            }
            catch (IOException)
            {
                throw new CompilerException("Unable to resolve compiler temporary directory path.");
            }
        }

        return Path.GetFullPath(current);
    }

    [SupportedOSPlatform("windows")]
    private static void HardenWindowsDirectoryAcl(string path)
    {
        var fullPath = Path.GetFullPath(path);
        string sid;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            sid = identity.User?.Value ?? "";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException)
        {
            throw new CompilerException("Unable to determine the current Windows security identifier for compiler temporary workspace ACLs.");
        }

        if (string.IsNullOrWhiteSpace(sid))
            throw new CompilerException("Unable to determine the current Windows security identifier for compiler temporary workspace ACLs.");

        RunIcacls(fullPath, "/inheritance:r");
        RunIcacls(fullPath, "/grant:r", "*" + sid + ":(OI)(CI)F");
    }

    private static void RunIcacls(string path, params string[] arguments)
    {
        var start = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "icacls.exe"),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(path);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(start)
                ?? throw new CompilerException("Unable to start icacls.exe while securing compiler temporary workspace ACLs.");
            _ = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new CompilerException("Unable to secure compiler temporary workspace ACLs with icacls.exe (exit code " + process.ExitCode + ").");
        }
        catch (CompilerException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            throw new CompilerException("Unable to secure compiler temporary workspace ACLs.");
        }
    }

    private static void DeleteDirectoryWithoutFollowingLinks(DirectoryInfo directory)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            if (IsLinkOrReparsePoint(entry))
            {
                if (entry is DirectoryInfo linkedDirectory)
                    linkedDirectory.Delete(recursive: false);
                else
                    entry.Delete();
                continue;
            }

            if (entry is DirectoryInfo childDirectory)
                DeleteDirectoryWithoutFollowingLinks(childDirectory);
            else
                entry.Delete();
        }

        directory.Delete(recursive: false);
    }

    private static bool IsLinkOrReparsePoint(FileSystemInfo info) =>
        info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0;

    private static void EnsureLexicallyContained(string root, string candidate, string kind, string declaredPath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (candidate.Equals(root, comparison))
            throw new CompilerException(kind + " path must identify a file inside the XPScript source directory: " + declaredPath);

        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, comparison))
            throw new CompilerException(kind + " path must remain inside the XPScript source directory: " + declaredPath);
    }

    private static void EnsureNoLinkEscape(string root, string candidate, string kind, string declaredPath)
    {
        var relative = Path.GetRelativePath(root, candidate);
        var current = root;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var info = TryGetFileSystemInfoIncludingBrokenLink(current, kind, declaredPath);
            if (info is null || !IsLinkOrReparsePoint(info))
                continue;

            string? resolvedTarget;
            try
            {
                resolvedTarget = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
            }
            catch (IOException)
            {
                throw new CompilerException(kind + " path contains an unresolved symbolic link or reparse point: " + declaredPath);
            }

            if (string.IsNullOrWhiteSpace(resolvedTarget))
                throw new CompilerException(kind + " path contains a symbolic link or reparse point that cannot be safely resolved: " + declaredPath);

            var finalTarget = Path.GetFullPath(resolvedTarget);
            var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
            if (!finalTarget.Equals(root, comparison) && !finalTarget.StartsWith(rootPrefix, comparison))
                throw new CompilerException(kind + " path resolves through a symbolic link or reparse point outside the XPScript source directory: " + declaredPath);
        }
    }

    private static FileSystemInfo? TryGetFileSystemInfoIncludingBrokenLink(string path, string kind, string declaredPath)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            if (directory.Exists || directory.LinkTarget is not null || (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                return directory;
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException)
        {
            throw new CompilerException(kind + " path contains an unreadable symbolic link or reparse point: " + declaredPath);
        }

        try
        {
            var file = new FileInfo(path);
            if (file.Exists || file.LinkTarget is not null || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                return file;
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException)
        {
            throw new CompilerException(kind + " path contains an unreadable symbolic link or reparse point: " + declaredPath);
        }

        return null;
    }
}
