namespace XPScript.Compiler;

internal static class ArchiveExtendedReaderRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptExtendedArchive
{
    private readonly XPScriptArchive _inner;

    public XPScriptExtendedArchive(object? path = null)
    {
        _inner = new XPScriptArchive(path, true);
    }

    public string Path => _inner.Path;
    public string Format => _inner.Format;
    public bool Exists => _inner.Exists;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => _inner.IsReadOnly;
    public string Password { get => _inner.Password; set => _inner.Password = value ?? ""; }
    public int CompressionLevel { get => _inner.CompressionLevel; set => _inner.CompressionLevel = value; }
    public long MaxExtractSize { get => _inner.MaxExtractSize; set => _inner.MaxExtractSize = value; }
    public int MaxEntries { get => _inner.MaxEntries; set => _inner.MaxEntries = value; }
    public double MaxCompressionRatio { get => _inner.MaxCompressionRatio; set => _inner.MaxCompressionRatio = value; }

    public bool IsEncrypted => Snapshots().Any(x => x.IsEncrypted);
    public long FileCount => Snapshots().LongCount(x => !x.IsDirectory);
    public long FolderCount => Snapshots().LongCount(x => x.IsDirectory);
    public long CompressedSize => Snapshots().Sum(x => x.CompressedSize);
    public long UncompressedSize => Snapshots().Sum(x => x.Size);
    public LSArray Entries => Pack(Snapshots().Cast<object?>());

    public void Open()
    {
        try { _inner.Open(); }
        catch (XPScriptRuntimeException) { _ = ReaderSnapshots(); }
    }

    public void Close() => _inner.Close();
    public void Save() => _inner.Save();
    public void Create(object? format = null) => _inner.Create(format);
    public void AddFile(object? sourcePath, object? archivePath = null) => _inner.AddFile(sourcePath, archivePath);
    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true) => _inner.AddFolder(sourcePath, archivePath, recursive);
    public void AddText(object? archivePath, object? text) => _inner.AddText(archivePath, text);
    public void AddBytes(object? archivePath, object? bytes) => _inner.AddBytes(archivePath, bytes);
    public bool Remove(object? entryName) => _inner.Remove(entryName);
    public bool Rename(object? entryName, object? newName) => _inner.Rename(entryName, newName);

    public bool Contains(object? entryName)
    {
        var wanted = Normalize(XPScriptRuntime.CStr(entryName));
        return Snapshots().Any(x => x.FullName.Equals(wanted, StringComparison.OrdinalIgnoreCase));
    }

    public XPScriptArchiveEntry? GetEntry(object? entryName)
    {
        var wanted = Normalize(XPScriptRuntime.CStr(entryName));
        return Snapshots().FirstOrDefault(x => x.FullName.Equals(wanted, StringComparison.OrdinalIgnoreCase));
    }

    public LSArray Files() => Pack(Snapshots().Where(x => !x.IsDirectory).Cast<object?>());
    public LSArray Folders() => Pack(Snapshots().Where(x => x.IsDirectory).Cast<object?>());

    public LSArray Find(object? pattern)
    {
        var expression = XPScriptRuntime.CStr(pattern).Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(expression)) expression = "*";
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(expression).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Pack(Snapshots().Where(x => System.Text.RegularExpressions.Regex.IsMatch(x.FullName, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant)).Cast<object?>());
    }

    public string ReadText(object? entryName) => System.Text.Encoding.UTF8.GetString(ReadBytesRaw(entryName));
    public LSArray ReadBytes(object? entryName) => PackBytes(ReadBytesRaw(entryName));

    public void Extract(object? entryName, object? targetPath)
    {
        try
        {
            _inner.Extract(entryName, targetPath);
            return;
        }
        catch (XPScriptRuntimeException)
        {
        }

        var name = Normalize(XPScriptRuntime.CStr(entryName));
        var snapshot = GetEntry(name) ?? throw new XPScriptRuntimeException(53, "Archive entry was not found.");
        var target = XPScriptFileSystemRuntime.ResolvePath(targetPath);
        if (snapshot.IsDirectory)
        {
            System.IO.Directory.CreateDirectory(target);
            return;
        }
        if (System.IO.Directory.Exists(target)) target = System.IO.Path.Combine(target, snapshot.Name);
        var parent = System.IO.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
        System.IO.File.WriteAllBytes(target, ReaderBytes(name));
    }

    public void ExtractFolder(object? folderName, object? targetDirectory)
    {
        var folder = Normalize(XPScriptRuntime.CStr(folderName)).TrimEnd('/') + "/";
        var root = XPScriptFileSystemRuntime.ResolvePath(targetDirectory);
        System.IO.Directory.CreateDirectory(root);
        var matches = Snapshots().Where(x => x.FullName.StartsWith(folder, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) throw new XPScriptRuntimeException(53, "Archive folder was not found.");
        foreach (var entry in matches)
        {
            var relative = entry.FullName[folder.Length..];
            if (relative.Length == 0) continue;
            var target = SafePath(root, Normalize(relative));
            if (entry.IsDirectory)
            {
                System.IO.Directory.CreateDirectory(target);
                continue;
            }
            EnsureNoReparseParents(root, target);
            var parent = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
            System.IO.File.WriteAllBytes(target, ReadBytesRaw(entry.FullName));
        }
    }

    public void ExtractAll(object? targetDirectory)
    {
        try
        {
            _inner.ExtractAll(targetDirectory);
            return;
        }
        catch (XPScriptRuntimeException)
        {
        }

        var root = XPScriptFileSystemRuntime.ResolvePath(targetDirectory);
        System.IO.Directory.CreateDirectory(root);
        XPScriptArchiveExtendedReader.ExtractAll(Path, Format, Password, root, MaxEntries, MaxExtractSize, MaxCompressionRatio);
    }

    public LSArray ToBytes() => _inner.ToBytes();

    private List<XPScriptArchiveEntry> Snapshots()
    {
        try
        {
            var values = _inner.Entries;
            var result = new List<XPScriptArchiveEntry>();
            if (!values.IsAllocated) return result;
            for (var i = values.LBound(); i <= values.UBound(); i++)
                if (values.Get(i) is XPScriptArchiveEntry entry) result.Add(entry);
            return result;
        }
        catch (XPScriptRuntimeException)
        {
            return ReaderSnapshots();
        }
    }

    private List<XPScriptArchiveEntry> ReaderSnapshots()
    {
        if (!Exists) return [];
        return XPScriptArchiveExtendedReader.Snapshots(Path, Format, Password, MaxEntries, MaxExtractSize, MaxCompressionRatio);
    }

    private byte[] ReadBytesRaw(object? entryName)
    {
        try
        {
            var values = _inner.ReadBytes(entryName);
            if (!values.IsAllocated) return [];
            var result = new byte[values.UBound() - values.LBound() + 1];
            var offset = 0;
            for (var i = values.LBound(); i <= values.UBound(); i++) result[offset++] = Convert.ToByte(values.Get(i), System.Globalization.CultureInfo.InvariantCulture);
            return result;
        }
        catch (XPScriptRuntimeException)
        {
            return ReaderBytes(Normalize(XPScriptRuntime.CStr(entryName)));
        }
    }

    private byte[] ReaderBytes(string entryName) =>
        XPScriptArchiveExtendedReader.ReadEntry(Path, Format, Password, entryName, MaxExtractSize, MaxCompressionRatio);

    private static LSArray Pack(IEnumerable<object?> values)
    {
        var items = values.ToList();
        if (items.Count == 0) return new LSArray("Variant", true);
        var result = new LSArray("Variant", true, [0], [items.Count - 1]);
        for (var i = 0; i < items.Count; i++) result.Set(items[i], i);
        return result;
    }

    private static LSArray PackBytes(byte[] bytes)
    {
        if (bytes.Length == 0) return new LSArray("Byte", true);
        var result = new LSArray("Byte", true, [0], [bytes.Length - 1]);
        for (var i = 0; i < bytes.Length; i++) result.Set(bytes[i], i);
        return result;
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
}

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
            var entry = WrapEntry(CurrentEntry(reader.Value), path);
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
            var entry = WrapEntry(CurrentEntry(reader.Value), path);
            Validate(entry, maxExtractSize, maxCompressionRatio);
            if (!entry.Key.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            if (entry.IsDirectory) throw new XPScriptRuntimeException(5, "Archive entry is a directory.");
            using var input = OpenEntryStream(reader.Value);
            using var output = new System.IO.MemoryStream();
            CopyLimited(input, output, entry.Size, maxExtractSize);
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
            var entry = WrapEntry(CurrentEntry(reader.Value), path);
            Validate(entry, maxExtractSize, maxCompressionRatio);
            var target = SafePath(root, entry.Key);
            if (entry.IsDirectory)
            {
                System.IO.Directory.CreateDirectory(target);
                continue;
            }
            EnsureNoReparseParents(root, target);
            var parent = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
            using var input = OpenEntryStream(reader.Value);
            using var output = new System.IO.FileStream(target, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
            totalWritten = checked(totalWritten + CopyLimited(input, output, entry.Size, maxExtractSize));
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

    private static ReaderEntryProxy WrapEntry(object entry, string path)
    {
        var key = GetString(entry, "Key");
        if (string.IsNullOrWhiteSpace(key)) key = FallbackEntryName(path);
        else key = Normalize(key);
        return new ReaderEntryProxy(
            key,
            GetLong(entry, "Size"),
            GetLong(entry, "CompressedSize"),
            GetDate(entry, "CreatedTime"),
            GetDate(entry, "LastModifiedTime"),
            GetBool(entry, "IsDirectory"),
            GetBool(entry, "IsEncrypted"),
            GetLong(entry, "Crc"),
            entry.GetType().GetProperty("LinkTarget")?.GetValue(entry)?.ToString());
    }

    private static string FallbackEntryName(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        var tarAliases = new[] { ".tgz", ".tbz", ".tbz2", ".txz", ".tlz", ".tzst" };
        foreach (var extension in tarAliases)
        {
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
            name = name[..^extension.Length] + ".tar";
            return Normalize(string.IsNullOrWhiteSpace(name) ? "content.tar" : name);
        }

        var singleStreamExtensions = new[] { ".gzip", ".gz", ".bzip2", ".bz2", ".xz", ".lzip", ".lz", ".zstd", ".zst", ".lzw", ".z" };
        foreach (var extension in singleStreamExtensions)
        {
            if (!name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) continue;
            name = name[..^extension.Length];
            break;
        }
        if (string.IsNullOrWhiteSpace(name)) name = "content";
        return Normalize(name);
    }

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

    private static void Validate(ReaderEntryProxy entry, long maxExtractSize, double maxCompressionRatio)
    {
        _ = Normalize(entry.Key);
        if (!string.IsNullOrEmpty(entry.LinkTarget)) throw new XPScriptRuntimeException(5, "Archive symbolic links are not allowed.");
        if (entry.Size < 0 || entry.Size > maxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
        if (entry.CompressedSize > 0 && entry.Size > 0 && (double)entry.Size / entry.CompressedSize > maxCompressionRatio)
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
    private static DateTime? GetDate(object value, string property)
    {
        var raw = value.GetType().GetProperty(property)?.GetValue(value);
        return raw is DateTime date ? date : null;
    }

    private sealed class ReaderEntryProxy
    {
        public string Key { get; }
        public long Size { get; }
        public long CompressedSize { get; }
        public DateTime? CreatedTime { get; }
        public DateTime? LastModifiedTime { get; }
        public bool IsDirectory { get; }
        public bool IsEncrypted { get; }
        public long Crc { get; }
        public string? LinkTarget { get; }

        public ReaderEntryProxy(string key, long size, long compressedSize, DateTime? createdTime, DateTime? lastModifiedTime, bool isDirectory, bool isEncrypted, long crc, string? linkTarget)
        {
            Key = key;
            Size = size;
            CompressedSize = compressedSize;
            CreatedTime = createdTime;
            LastModifiedTime = lastModifiedTime;
            IsDirectory = isDirectory;
            IsEncrypted = isEncrypted;
            Crc = crc;
            LinkTarget = linkTarget;
        }
    }

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
