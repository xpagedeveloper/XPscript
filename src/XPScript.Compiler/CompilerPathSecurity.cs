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

    public static void HardenTemporaryDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) return;

        try
        {
            File.SetUnixFileMode(path,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }
        catch (PlatformNotSupportedException) { }
        catch (UnauthorizedAccessException ex)
        {
            throw new CompilerException("Unable to secure compiler temporary workspace permissions: " + ex.Message);
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
        catch (UnauthorizedAccessException ex)
        {
            throw new CompilerException("Unable to secure compiler temporary file permissions: " + ex.Message);
        }
    }

    public static void DeleteOwnedTemporaryDirectory(string path)
    {
        var full = Path.GetFullPath(path);
        var tempRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "XPScript"));
        EnsureLexicallyContained(tempRoot, full, "Compiler temporary workspace", full);

        if (!Directory.Exists(full)) return;

        var rootInfo = new DirectoryInfo(full);
        if (IsLinkOrReparsePoint(rootInfo))
            throw new CompilerException("Refusing to recursively clean a compiler temporary workspace that is itself a symbolic link or reparse point.");

        DeleteDirectoryWithoutFollowingLinks(rootInfo);
    }

    private static void DeleteDirectoryWithoutFollowingLinks(DirectoryInfo directory)
    {
        foreach (var entry in directory.EnumerateFileSystemInfos())
        {
            try
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
            catch
            {
                throw;
            }
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
            if (!File.Exists(current) && !Directory.Exists(current))
                continue;

            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (!IsLinkOrReparsePoint(info))
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
}
