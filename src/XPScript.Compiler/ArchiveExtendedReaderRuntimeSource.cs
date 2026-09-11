namespace XPScript.Compiler;

internal static class ArchiveExtendedReaderRuntimeSource
{
    public const string Code = """
internal static class XPScriptArchiveExtendedReader
{
    public static List<XPScriptArchiveEntry> Snapshots(string path, string format, string password, int maxEntries, long maxExtractSize, double maxCompressionRatio)
    {
        using var reader = Open(path, format, password);
        var result = new List<XPScriptArchiveEntry>();
        long total = 0;
        while (MoveNext(reader.Value))
        {
            if (result.Count >= maxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
            var entry = CurrentEntry(reader.Value);
            Validate(entry, maxExtractSize, maxCompressionRatio);
            var snapshot = XPScriptArchiveEntry.FromExtended(entry);
            total = checked(total + snapshot.Size);
            if (total > maxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
            result.Add(snapshot);
        }
        return result;
    }

    public static byte[] ReadEntry(string path, string format, string password, string wanted, long maxExtractSize, double maxCompressionRatio)
    {
        using var reader = Open(path, format, password);
        while (MoveNext(reader.Value))
        {
            var entry = CurrentEntry(reader.Value);
            Validate(entry, maxExtractSize, maxCompressionRatio);
            var key = Normalize(GetString(entry, "Key"));
            if (!key.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            if (GetBool(entry, "IsDirectory")) throw new XPScriptRuntimeException(5, "Archive entry is a directory.");
            using var input = OpenEntryStream(reader.Value);
            using var output = new System.IO.MemoryStream();
            CopyLimited(input, output, GetLong(entry, "Size"), maxExtractSize);
            return output.ToArray();
        }
        throw new XPScriptRuntimeException(53, "Archive entry was not found.");
    }

    public static void ExtractAll(string path, string format, string password, string root, int maxEntries, long maxExtractSize, double maxCompressionRatio)
    {
        using var reader = Open(path, format, password);
        var count = 0;
        long totalWritten = 0;
        while (MoveNext(reader.Value))
        {
            if (++count > maxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
            var entry = CurrentEntry(reader.Value);
            Validate(entry, maxExtractSize, maxCompressionRatio);
            var name = Normalize(GetString(entry, "Key"));
            var target = SafePath(root, name);
            if (GetBool(entry, "IsDirectory"))
            {
                System.IO.Directory.CreateDirectory(target);
                continue;
            }
            EnsureNoReparseParents(root, target);
            var parent = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
            using var input = OpenEntryStream(reader.Value);
            using var output = new System.IO.FileStream(target, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
            totalWritten = checked(totalWritten + CopyLimited(input, output, GetLong(entry, "Size"), maxExtractSize));
            if (totalWritten > maxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
        }
    }

    private static ReaderHandle Open(string path, string format, string password)
    {
        try
        {
            var assembly = System.Reflection.Assembly.Load("SharpCompress");
            var optionsType = assembly.GetType("SharpCompress.Readers.ReaderOptions", throwOnError: true)!;
            var options = Activator.CreateInstance(optionsType)!;
            if (!string.IsNullOrEmpty(password)) optionsType.GetProperty("Password")?.SetValue(options, password);
            optionsType.GetProperty("ExtensionHint")?.SetValue(options, format.ToLowerInvariant());
            var factoryType = assembly.GetType("SharpCompress.Readers.ReaderFactory", throwOnError: true)!;
            var method = factoryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "OpenReader" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[1].ParameterType == optionsType)
                ?? throw new MissingMethodException("SharpCompress ReaderFactory.OpenReader(string, ReaderOptions) was not found.");
            var reader = method.Invoke(null, [path, options]) ?? throw new XPScriptRuntimeException(5, "SharpCompress failed to open the archive reader.");
            return new ReaderHandle(reader);
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new XPScriptRuntimeException(5, "Unable to open extended archive reader: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "Unable to load SharpCompress reader support: " + ex.Message);
        }
    }

    private static bool MoveNext(object reader) => Convert.ToBoolean(reader.GetType().GetMethod("MoveToNextEntry", Type.EmptyTypes)?.Invoke(reader, null) ?? false, System.Globalization.CultureInfo.InvariantCulture);

    private static object CurrentEntry(object reader) => reader.GetType().GetProperty("Entry")?.GetValue(reader) ?? throw new XPScriptRuntimeException(5, "SharpCompress reader entry is unavailable.");

    private static System.IO.Stream OpenEntryStream(object reader)
    {
        try
        {
            return (System.IO.Stream)(reader.GetType().GetMethod("OpenEntryStream", Type.EmptyTypes)?.Invoke(reader, null)
                ?? throw new MissingMethodException("SharpCompress reader OpenEntryStream() was not found."));
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new XPScriptRuntimeException(5, "Unable to read archive entry: " + (ex.InnerException?.Message ?? ex.Message));
        }
    }

    private static void Validate(object entry, long maxExtractSize, double maxCompressionRatio)
    {
        _ = Normalize(GetString(entry, "Key"));
        var linkTarget = entry.GetType().GetProperty("LinkTarget")?.GetValue(entry)?.ToString();
        if (!string.IsNullOrEmpty(linkTarget)) throw new XPScriptRuntimeException(5, "Archive symbolic links are not allowed.");
        var size = GetLong(entry, "Size");
        var compressed = GetLong(entry, "CompressedSize");
        if (size < 0 || size > maxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
        if (compressed > 0 && size > 0 && (double)size / compressed > maxCompressionRatio)
            throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxCompressionRatio.");
    }

    private static long CopyLimited(System.IO.Stream input, System.IO.Stream output, long declaredSize, long maxExtractSize)
    {
        var buffer = new byte[128 * 1024];
        long written = 0;
        while (true)
        {
            var read = input.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;
            written = checked(written + read);
            if (written > maxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
            output.Write(buffer, 0, read);
        }
        if (declaredSize >= 0 && written > declaredSize + 1024 * 1024)
            throw new XPScriptRuntimeException(5, "Archive entry expanded beyond its declared size.");
        return written;
    }

    private static string Normalize(string value)
    {
        var name = (value ?? "").Replace('\\', '/').Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Archive entry name must not be empty.");
        if (name.StartsWith("/", StringComparison.Ordinal) || name.StartsWith("//", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z]:"))
            throw new XPScriptRuntimeException(5, "Absolute archive paths are not allowed.");
        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(x => x == "..")) throw new XPScriptRuntimeException(5, "Archive path traversal is not allowed.");
        var result = string.Join('/', parts.Where(x => x != "."));
        if (name.EndsWith("/", StringComparison.Ordinal)) result += "/";
        return result;
    }

    private static string SafePath(string root, string name)
    {
        var rootFull = System.IO.Path.GetFullPath(root);
        var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(rootFull, name.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        var prefix = rootFull.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!target.StartsWith(prefix, comparison)) throw new XPScriptRuntimeException(5, "Archive extraction path escapes the target directory.");
        return target;
    }

    private static void EnsureNoReparseParents(string root, string target)
    {
        var rootFull = System.IO.Path.GetFullPath(root);
        var current = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(target));
        while (!string.IsNullOrEmpty(current) && !string.Equals(current, rootFull, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if (System.IO.Directory.Exists(current) && (System.IO.File.GetAttributes(current) & System.IO.FileAttributes.ReparsePoint) != 0)
                throw new XPScriptRuntimeException(5, "Archive extraction through a symbolic link or reparse point is not allowed.");
            current = System.IO.Path.GetDirectoryName(current);
        }
    }

    private static string GetString(object value, string property) => value.GetType().GetProperty(property)?.GetValue(value)?.ToString() ?? "";
    private static long GetLong(object value, string property) => Convert.ToInt64(value.GetType().GetProperty(property)?.GetValue(value) ?? 0L, System.Globalization.CultureInfo.InvariantCulture);
    private static bool GetBool(object value, string property) => Convert.ToBoolean(value.GetType().GetProperty(property)?.GetValue(value) ?? false, System.Globalization.CultureInfo.InvariantCulture);

    private sealed class ReaderHandle : IDisposable
    {
        public object Value { get; }
        public ReaderHandle(object value) => Value = value;
        public void Dispose()
        {
            if (Value is IDisposable disposable) disposable.Dispose();
        }
    }
}
""";
}
