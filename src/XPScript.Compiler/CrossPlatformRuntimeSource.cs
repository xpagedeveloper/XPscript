namespace XPScript.Compiler;

internal static class CrossPlatformRuntimeSource
{
    public const string Code = """
internal static class XPCrossPlatformRuntime
{
    private static IEnumerator<string>? DirEnumerator;

    public static string Platform()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "MacOS";
        if (OperatingSystem.IsFreeBSD()) return "FreeBSD";
        return "Unknown";
    }

    public static bool FileExists(object? path) => File.Exists(XPScriptRuntime.CStr(path));

    public static bool DirExists(object? path) => Directory.Exists(XPScriptRuntime.CStr(path));

    public static bool IsFile(object? path)
    {
        var value = XPScriptRuntime.CStr(path);
        return File.Exists(value) && !Directory.Exists(value);
    }

    public static bool IsDir(object? path) => Directory.Exists(XPScriptRuntime.CStr(path));

    public static bool CopyFile(object? source, object? target, int action = 1) =>
        ApplyFileTransferPolicy(source, target, action, move: false);

    public static bool MoveFile(object? source, object? target, int action = 1) =>
        ApplyFileTransferPolicy(source, target, action, move: true);

    private static bool ApplyFileTransferPolicy(object? sourceValue, object? targetValue, int action, bool move)
    {
        if (action < 1 || action > 3)
            throw new XPScriptRuntimeException(5, "File transfer action must be 1 (fail), 2 (overwrite), or 3 (skip).");

        var source = XPScriptFileSystemRuntime.ResolvePath(sourceValue);
        var target = XPScriptFileSystemRuntime.ResolvePath(targetValue);
        if (!File.Exists(source)) return false;

        if (File.Exists(target) || Directory.Exists(target))
        {
            if (action == 1 || action == 3) return false;
            if (Directory.Exists(target)) return false;
        }

        try
        {
            if (move) XPScriptFileSystemRuntime.MoveFile(source, target, overwrite: action == 2);
            else XPScriptFileSystemRuntime.CopyFile(source, target, overwrite: action == 2);
            return true;
        }
        catch (IOException) when (action != 2 && (File.Exists(target) || Directory.Exists(target)))
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static class PathApi
    {
        public static string Combine(object? left, object? right) =>
            System.IO.Path.Combine(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right));

        public static string FileName(object? path) =>
            System.IO.Path.GetFileName(XPScriptRuntime.CStr(path));

        public static string Extension(object? path) =>
            System.IO.Path.GetExtension(XPScriptRuntime.CStr(path));

        public static string Directory(object? path) =>
            System.IO.Path.GetDirectoryName(XPScriptFileSystemRuntime.ResolvePath(path)) ?? "";

        public static string Absolute(object? path) =>
            XPScriptFileSystemRuntime.ResolvePath(path);

        public static string Relative(object? basePath, object? path) =>
            System.IO.Path.GetRelativePath(XPScriptFileSystemRuntime.ResolvePath(basePath), XPScriptFileSystemRuntime.ResolvePath(path));

        public static bool Exists(object? path)
        {
            var value = XPScriptRuntime.CStr(path);
            return FileExists(value) || DirExists(value);
        }
    }

    public static string Dir(object? pattern = null, int mode = 0, int maxDepth = 3)
    {
        if (pattern is not null)
        {
            if (mode < 0 || mode > 3)
                throw new XPScriptRuntimeException(5, "Dir mode must be 0, 1, 2, or 3.");
            if (maxDepth < 0 || maxDepth > 32)
                throw new XPScriptRuntimeException(5, "Dir maxDepth must be between 0 and 32.");

            var raw = XPScriptRuntime.CStr(pattern);
            if (string.IsNullOrWhiteSpace(raw)) raw = "*";

            var directoryPart = Path.GetDirectoryName(raw);
            var directory = string.IsNullOrEmpty(directoryPart)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(directoryPart);
            var mask = Path.GetFileName(raw);
            if (string.IsNullOrEmpty(mask)) mask = "*";
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("Directory does not exist.");

            IEnumerable<string> entries = mode switch
            {
                0 => Directory.EnumerateFileSystemEntries(directory, mask, SearchOption.TopDirectoryOnly),
                1 => Directory.EnumerateFiles(directory, mask, SearchOption.TopDirectoryOnly),
                2 => Directory.EnumerateDirectories(directory, mask, SearchOption.TopDirectoryOnly),
                3 => EnumerateFilesLimitedDepth(directory, mask, maxDepth),
                _ => throw new XPScriptRuntimeException(5, "Dir mode must be 0, 1, 2, or 3.")
            };

            DirEnumerator?.Dispose();
            DirEnumerator = entries
                .Select(entry => mode == 3 ? Path.GetRelativePath(directory, entry) : Path.GetFileName(entry))
                .Where(name => !string.IsNullOrEmpty(name) && name != "." && name != "..")
                .GetEnumerator();
        }

        if (DirEnumerator is null) return "";
        if (!DirEnumerator.MoveNext())
        {
            DirEnumerator.Dispose();
            DirEnumerator = null;
            return "";
        }
        return DirEnumerator.Current;
    }

    private static IEnumerable<string> EnumerateFilesLimitedDepth(string root, string mask, int maxDepth)
    {
        var pending = new Stack<(string Directory, int Depth)>();
        pending.Push((root, 0));

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var file in Directory.EnumerateFiles(current.Directory, mask, SearchOption.TopDirectoryOnly))
                yield return file;

            if (current.Depth >= maxDepth) continue;

            foreach (var child in Directory.EnumerateDirectories(current.Directory, "*", SearchOption.TopDirectoryOnly))
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(child); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                pending.Push((child, current.Depth + 1));
            }
        }
    }

    public sealed class XPFileInfoValue
    {
        public string Name { get; }
        public string FullPath { get; }
        public string Extension { get; }
        public long Length { get; }
        public DateTime Created { get; }
        public DateTime Modified { get; }
        public DateTime Accessed { get; }
        public bool IsFile { get; }
        public bool IsDirectory { get; }
        public bool IsLink { get; }
        public int Attributes { get; }

        public XPFileInfoValue(string path)
        {
            FullPath = Path.GetFullPath(path);
            FileSystemInfo info;
            if (File.Exists(FullPath)) info = new System.IO.FileInfo(FullPath);
            else if (Directory.Exists(FullPath)) info = new DirectoryInfo(FullPath);
            else throw new FileNotFoundException("Path not found.", FullPath);

            Name = info.Name;
            Extension = info.Extension;
            Created = info.CreationTime;
            Modified = info.LastWriteTime;
            Accessed = info.LastAccessTime;
            IsFile = info is System.IO.FileInfo;
            IsDirectory = info is DirectoryInfo;
            IsLink = info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0;
            Attributes = XPScriptFileSystemRuntime.GetFileAttr(FullPath);
            Length = info is System.IO.FileInfo file ? file.Length : 0L;
        }
    }

    public static XPFileInfoValue FileInfo(object? path) =>
        new(Path.GetFullPath(XPScriptRuntime.CStr(path)));

    public static string FileHash(object? path, object? algorithm = null)
    {
        var file = Path.GetFullPath(XPScriptRuntime.CStr(path));
        var name = algorithm is null ? "SHA256" : XPScriptRuntime.CStr(algorithm).Trim().ToUpperInvariant().Replace("-", "", StringComparison.Ordinal);
        using System.Security.Cryptography.HashAlgorithm hash = name switch
        {
            "SHA256" => System.Security.Cryptography.SHA256.Create(),
            "SHA384" => System.Security.Cryptography.SHA384.Create(),
            "SHA512" => System.Security.Cryptography.SHA512.Create(),
            "SHA1" => System.Security.Cryptography.SHA1.Create(),
            "MD5" => System.Security.Cryptography.MD5.Create(),
            _ => throw new XPScriptRuntimeException(5, "FileHash algorithm must be SHA256, SHA384, SHA512, SHA1, or MD5.")
        };
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(hash.ComputeHash(stream));
    }

    public static bool FileEquals(object? leftValue, object? rightValue)
    {
        var left = Path.GetFullPath(XPScriptRuntime.CStr(leftValue));
        var right = Path.GetFullPath(XPScriptRuntime.CStr(rightValue));
        var leftInfo = new System.IO.FileInfo(left);
        var rightInfo = new System.IO.FileInfo(right);
        if (!leftInfo.Exists || !rightInfo.Exists) return false;
        if (leftInfo.Length != rightInfo.Length) return false;
        if (string.Equals(left, right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return true;

        const int bufferSize = 128 * 1024;
        using var a = new FileStream(left, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
        using var b = new FileStream(right, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
        var ab = new byte[bufferSize];
        var bb = new byte[bufferSize];
        while (true)
        {
            var ac = a.Read(ab, 0, ab.Length);
            var bc = b.Read(bb, 0, bb.Length);
            if (ac != bc) return false;
            if (ac == 0) return true;
            if (!ab.AsSpan(0, ac).SequenceEqual(bb.AsSpan(0, bc))) return false;
        }
    }

    public static LSArray Files(object? pathOrPattern, object? maskValue = null, bool recursive = false, int maxDepth = 3) =>
        ToXPScriptArray("String", EnumeratePaths(pathOrPattern, maskValue, recursive, maxDepth, directories: false).Cast<object?>());

    public static LSArray Directories(object? pathOrPattern, object? maskValue = null, bool recursive = false, int maxDepth = 3) =>
        ToXPScriptArray("String", EnumeratePaths(pathOrPattern, maskValue, recursive, maxDepth, directories: true).Cast<object?>());

    private static IEnumerable<string> EnumeratePaths(object? pathOrPattern, object? maskValue, bool recursive, int maxDepth, bool directories)
    {
        if (maxDepth < 0 || maxDepth > 32)
            throw new XPScriptRuntimeException(5, "Filesystem maxDepth must be between 0 and 32.");

        var raw = XPScriptRuntime.CStr(pathOrPattern);
        if (string.IsNullOrWhiteSpace(raw)) raw = ".";
        string root;
        string mask;
        if (maskValue is not null)
        {
            root = Path.GetFullPath(raw);
            mask = XPScriptRuntime.CStr(maskValue);
            if (string.IsNullOrWhiteSpace(mask)) mask = "*";
        }
        else if (raw.IndexOfAny(['*', '?']) >= 0)
        {
            var directoryPart = Path.GetDirectoryName(raw);
            root = Path.GetFullPath(string.IsNullOrEmpty(directoryPart) ? "." : directoryPart);
            mask = Path.GetFileName(raw);
            if (string.IsNullOrWhiteSpace(mask)) mask = "*";
        }
        else
        {
            root = Path.GetFullPath(raw);
            mask = "*";
        }
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Directory does not exist.");

        foreach (var value in EnumerateLimited(root, mask, recursive ? maxDepth : 0, directories))
            yield return Path.GetFullPath(value);
    }

    private static IEnumerable<string> EnumerateLimited(string root, string mask, int maxDepth, bool directories)
    {
        var pending = new Stack<(string Directory, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            var matches = directories
                ? Directory.EnumerateDirectories(current.Directory, mask, SearchOption.TopDirectoryOnly)
                : Directory.EnumerateFiles(current.Directory, mask, SearchOption.TopDirectoryOnly);
            foreach (var match in matches) yield return match;

            if (current.Depth >= maxDepth) continue;
            foreach (var child in Directory.EnumerateDirectories(current.Directory, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    pending.Push((child, current.Depth + 1));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public static string ReadFile(object? path, object? charset = null)
    {
        var encoding = ResolveTextEncoding(charset);
        using var reader = new StreamReader(Path.GetFullPath(XPScriptRuntime.CStr(path)), encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static void WriteFile(object? path, object? content, object? charset = null) =>
        File.WriteAllText(Path.GetFullPath(XPScriptRuntime.CStr(path)), XPScriptRuntime.CStr(content), ResolveTextEncoding(charset));

    public static void AppendFile(object? path, object? content, object? charset = null) =>
        File.AppendAllText(Path.GetFullPath(XPScriptRuntime.CStr(path)), XPScriptRuntime.CStr(content), ResolveTextEncoding(charset));

    public static LSArray ReadLines(object? path, object? charset = null)
    {
        var values = new List<object?>();
        using var reader = new StreamReader(Path.GetFullPath(XPScriptRuntime.CStr(path)), ResolveTextEncoding(charset), detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = reader.ReadLine()) is not null) values.Add(line);
        return ToXPScriptArray("String", values);
    }

    public static void WriteLines(object? path, object? values, object? charset = null)
    {
        using var writer = new StreamWriter(Path.GetFullPath(XPScriptRuntime.CStr(path)), append: false, ResolveTextEncoding(charset));
        foreach (var value in EnumerateValues(values)) writer.WriteLine(XPScriptRuntime.CStr(value));
    }

    public static LSArray ReadBytes(object? path)
    {
        var bytes = File.ReadAllBytes(Path.GetFullPath(XPScriptRuntime.CStr(path)));
        return ToXPScriptArray("Byte", bytes.Cast<object?>());
    }

    public static void WriteBytes(object? path, object? values) =>
        File.WriteAllBytes(Path.GetFullPath(XPScriptRuntime.CStr(path)), ToBytes(values));

    private static LSArray ToXPScriptArray(string elementType, IEnumerable<object?> values)
    {
        var items = values.ToList();
        if (items.Count == 0) return new LSArray(elementType, true);
        var array = new LSArray(elementType, true, [0], [items.Count - 1]);
        for (var i = 0; i < items.Count; i++) array.Set(items[i], i);
        return array;
    }

    private static IEnumerable<object?> EnumerateValues(object? values)
    {
        if (values is LSArray array)
        {
            if (!array.IsAllocated) yield break;
            var lower = array.LBound();
            var upper = array.UBound();
            for (var i = lower; i <= upper; i++) yield return array.Get(new object?[] { i });
            yield break;
        }
        if (values is System.Collections.IEnumerable enumerable && values is not string)
        {
            foreach (var value in enumerable) yield return value;
            yield break;
        }
        throw new XPScriptRuntimeException(13, "Expected an array or enumerable value.");
    }

    private static byte[] ToBytes(object? values)
    {
        if (values is byte[] bytes) return bytes;
        var result = new List<byte>();
        foreach (var value in EnumerateValues(values)) result.Add(XPScriptRuntime.CByte(value));
        return result.ToArray();
    }

    private static Encoding ResolveTextEncoding(object? charset)
    {
        if (charset is null || string.IsNullOrWhiteSpace(XPScriptRuntime.CStr(charset))) return new UTF8Encoding(false);
        var name = XPScriptRuntime.CStr(charset).Trim();
        var normalized = name.Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        if (normalized is "utf8" or "utf-8") return new UTF8Encoding(false);
        if (normalized is "utf16" or "utf-16" or "unicode") return new UnicodeEncoding(false, true);
        if (normalized is "utf-16be" or "unicodefffe") return new UnicodeEncoding(true, true);
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        try { return System.Text.Encoding.GetEncoding(name); }
        catch (ArgumentException) { throw new XPScriptRuntimeException(5, "Unsupported text charset: " + name); }
    }

    public static string StrTemplate(object? template, object? values)
    {
        var text = XPScriptRuntime.CStr(template);
        var output = new System.Text.StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];

            if (current == '\\' && i + 1 < text.Length && (text[i + 1] == '{' || text[i + 1] == '}'))
            {
                output.Append(text[i + 1]);
                i++;
                continue;
            }

            if (current != '{')
            {
                output.Append(current);
                continue;
            }

            var close = text.IndexOf('}', i + 1);
            if (close < 0)
            {
                output.Append(current);
                continue;
            }

            var token = text[(i + 1)..close];
            if (token.Length == 0 || !token.All(char.IsDigit))
            {
                output.Append(text, i, close - i + 1);
                i = close;
                continue;
            }

            if (!int.TryParse(token, out var index))
                throw new XPScriptRuntimeException(9, "StrTemplate placeholder index is invalid.");

            output.Append(XPScriptRuntime.CStr(GetTemplateValue(values, index)));
            i = close;
        }

        return output.ToString();
    }

    private static object? GetTemplateValue(object? values, int index)
    {
        try
        {
            if (values is LSArray array)
            {
                if (!array.IsAllocated || array.Rank != 1)
                    throw new XPScriptRuntimeException(5, "StrTemplate values must be a one-dimensional allocated array or list.");
                return array.Get(index);
            }

            if (values is System.Array clrArray)
            {
                if (clrArray.Rank != 1)
                    throw new XPScriptRuntimeException(5, "StrTemplate values must be a one-dimensional array or list.");
                return clrArray.GetValue(index);
            }

            if (values is System.Collections.IList list)
                return list[index];
        }
        catch (XPScriptRuntimeException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new XPScriptRuntimeException(9, "StrTemplate placeholder index is outside the supplied values array.");
        }

        throw new XPScriptRuntimeException(13, "StrTemplate values must be an array or list.");
    }

    public static int Shell(object? command, object? windowStyle = null)
    {
        var raw = XPScriptRuntime.CStr(command).Trim();
        if (raw.Length == 0)
            throw new XPScriptRuntimeException(5, "Shell requires a program or script name.");

        var parsed = SplitCommand(raw);
        try
        {
            var start = BuildStartInfo(parsed.FileName, ParseArguments(parsed.Arguments), windowStyle);
            using var process = System.Diagnostics.Process.Start(start)
                ?? throw new FileNotFoundException("Could not start the requested program or script.");
            return 33;
        }
        catch (Exception ex)
        {
            throw LSExtendedErrorRuntime.Normalize(ex);
        }
    }

    public static int ShellArgs(object? executable, object? arguments, object? windowStyle = null)
    {
        var fileName = XPScriptRuntime.CStr(executable).Trim();
        if (fileName.Length == 0)
            throw new XPScriptRuntimeException(5, "ShellArgs requires a program name.");

        var structuredArguments = ToStructuredArguments(arguments);
        try
        {
            var start = BuildStartInfo(fileName, structuredArguments, windowStyle);
            using var process = System.Diagnostics.Process.Start(start)
                ?? throw new FileNotFoundException("Could not start the requested program.");
            return 33;
        }
        catch (Exception ex)
        {
            throw LSExtendedErrorRuntime.Normalize(ex);
        }
    }

    private static System.Diagnostics.ProcessStartInfo BuildStartInfo(string fileName, IReadOnlyList<string> arguments, object? windowStyle)
    {
        fileName = ResolveRequestedProgram(fileName);
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
                ValidateBatchFileName(fileName);
                var batchArguments = arguments;
                foreach (var argument in batchArguments) ValidateBatchArgument(argument);

                info.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                info.ArgumentList.Add("/d");
                info.ArgumentList.Add("/s");
                info.ArgumentList.Add("/c");
                info.ArgumentList.Add(fileName);
                foreach (var argument in batchArguments) info.ArgumentList.Add(argument);
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
                info.FileName = ResolveUnixPowerShell();
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

            info.FileName = fileName;
            AddArguments(info, arguments);
            return info;
        }

        throw new PlatformNotSupportedException("Shell is not implemented for platform: " + Platform());
    }

    private static IReadOnlyList<string> ToStructuredArguments(object? arguments)
    {
        if (arguments is null) return [];
        if (arguments is string)
            throw new XPScriptRuntimeException(5, "ShellArgs arguments must be an array or list, not a command string.");

        var result = new List<string>();
        if (arguments is LSArray array)
        {
            if (!array.IsAllocated || array.Rank != 1)
                throw new XPScriptRuntimeException(5, "ShellArgs requires a one-dimensional allocated array or list.");
            var lower = array.LBound();
            var upper = array.UBound();
            for (var index = lower; index <= upper; index++)
            {
                if (result.Count >= 4096)
                    throw new XPScriptRuntimeException(5, "ShellArgs accepts at most 4096 arguments.");
                result.Add(XPScriptRuntime.CStr(array.Get(index)));
            }
            return result;
        }

        if (arguments is not System.Collections.IEnumerable enumerable)
            throw new XPScriptRuntimeException(5, "ShellArgs arguments must be an array or list.");

        foreach (var value in enumerable)
        {
            if (result.Count >= 4096)
                throw new XPScriptRuntimeException(5, "ShellArgs accepts at most 4096 arguments.");
            result.Add(XPScriptRuntime.CStr(value));
        }
        return result;
    }

    private static string ResolveRequestedProgram(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new XPScriptRuntimeException(5, "Shell requires a program or script name.");

        try
        {
            if (Path.IsPathRooted(fileName) || fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
            {
                var explicitPath = Path.GetFullPath(fileName);
                if (!File.Exists(explicitPath))
                    throw new XPScriptRuntimeException(53, "Requested program or script was not found.");
                return explicitPath;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new XPScriptRuntimeException(5, "Requested program or script path is invalid.");
        }

        var resolved = ResolveFromAbsolutePath(fileName, includeWindowsExtensions: OperatingSystem.IsWindows());
        if (resolved is null)
            throw new XPScriptRuntimeException(53, "Requested program or script was not found in absolute PATH locations.");
        return resolved;
    }

    private static string ResolveWindowsPowerShell()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            var pwsh = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwsh)) return pwsh;
        }

        var fromPath = ResolveFromAbsolutePath("pwsh.exe", includeWindowsExtensions: false);
        if (fromPath is not null) return fromPath;

        var windowsPowerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(windowsPowerShell)) return windowsPowerShell;
        throw new XPScriptRuntimeException(53, "PowerShell executable was not found.");
    }

    private static string ResolveUnixPowerShell()
    {
        foreach (var candidate in new[] { "/usr/bin/pwsh", "/usr/local/bin/pwsh", "/opt/homebrew/bin/pwsh" })
            if (File.Exists(candidate)) return candidate;

        return ResolveFromAbsolutePath("pwsh", includeWindowsExtensions: false)
            ?? throw new XPScriptRuntimeException(53, "PowerShell executable was not found.");
    }

    private static string? ResolveFromAbsolutePath(string executableName, bool includeWindowsExtensions)
    {
        var names = CandidateExecutableNames(executableName, includeWindowsExtensions);
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var rawDirectory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var directory = rawDirectory.Trim().Trim('"');
                if (!Path.IsPathRooted(directory)) continue;
                directory = Path.GetFullPath(directory);
                foreach (var name in names)
                {
                    var candidate = Path.Combine(directory, name);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }
        }
        return null;
    }

    private static IReadOnlyList<string> CandidateExecutableNames(string executableName, bool includeWindowsExtensions)
    {
        if (!includeWindowsExtensions || Path.HasExtension(executableName)) return [executableName];

        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.StartsWith('.', StringComparison.Ordinal) && x.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '\0']) < 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return extensions.Length == 0
            ? [executableName + ".exe", executableName + ".com", executableName + ".cmd", executableName + ".bat"]
            : extensions.Select(x => executableName + x).ToArray();
    }

    private static void AddArguments(System.Diagnostics.ProcessStartInfo info, IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);
    }

    private static IReadOnlyList<string> ParseArguments(string text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var tokenStarted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                tokenStarted = true;
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
                if (tokenStarted)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    tokenStarted = false;
                }
                continue;
            }
            current.Append(c);
            tokenStarted = true;
        }
        if (inQuotes) throw new XPScriptRuntimeException(5, "Unterminated quoted Shell argument.");
        if (tokenStarted) result.Add(current.ToString());
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

    private static void ValidateBatchFileName(string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0', '"', '&', '|', '<', '>', '^', '%', '!']) >= 0)
            throw new XPScriptRuntimeException(5, "Batch script path contains unsupported command-shell characters.");
    }

    private static void ValidateBatchArgument(string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0', '"', '&', '|', '<', '>', '^', '%', '!']) >= 0)
            throw new XPScriptRuntimeException(5, "Batch script arguments may not contain command-shell metacharacters. Use a directly executable program or PowerShell script for structured arguments.");
    }
}
""";
}
