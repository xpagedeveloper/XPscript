namespace XPScript.Compiler;

internal static class ArchiveExtendedMemoryRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptExtendedMemoryArchive
{
    private readonly byte[] _data;

    public XPScriptExtendedMemoryArchive(object? bytes)
    {
        _data = ToRawBytes(bytes);
        if (_data.Length == 0) throw new XPScriptRuntimeException(5, "Extended in-memory Archive requires archive data.");
    }

    public string Path => "";
    public string Format => DetectFormat();
    public bool Exists => _data.Length > 0;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => true;
    public string Password { get; set; } = "";
    public int CompressionLevel { get; set; } = 5;
    public long MaxExtractSize { get; set; } = 2L * 1024 * 1024 * 1024;
    public int MaxEntries { get; set; } = 10000;
    public double MaxCompressionRatio { get; set; } = 1000d;

    public bool IsEncrypted => Snapshots().Any(x => x.IsEncrypted);
    public long FileCount => Snapshots().LongCount(x => !x.IsDirectory);
    public long FolderCount => Snapshots().LongCount(x => x.IsDirectory);
    public long CompressedSize => Snapshots().Sum(x => x.CompressedSize);
    public long UncompressedSize => Snapshots().Sum(x => x.Size);
    public LSArray Entries => Pack(Snapshots().Cast<object?>());

    public void Open() => _ = Snapshots();
    public void Close() { }
    public void Save() => ThrowReadOnly();
    public void Create(object? format = null) => ThrowReadOnly();
    public void AddFile(object? sourcePath, object? archivePath = null) => ThrowReadOnly();
    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true) => ThrowReadOnly();
    public void AddText(object? archivePath, object? text) => ThrowReadOnly();
    public void AddBytes(object? archivePath, object? bytes) => ThrowReadOnly();
    public bool Remove(object? entryName) { ThrowReadOnly(); return false; }
    public bool Rename(object? entryName, object? newName) { ThrowReadOnly(); return false; }

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
    public LSArray ToBytes() => PackBytes(_data);

    public void Extract(object? entryName, object? targetPath)
    {
        var name = Normalize(XPScriptRuntime.CStr(entryName));
        var snapshot = GetEntry(name) ?? throw new XPScriptRuntimeException(53, "Archive entry was not found.");
        var target = XPScriptFileSystemRuntime.ResolvePath(targetPath);
        if (snapshot.IsDirectory) { System.IO.Directory.CreateDirectory(target); return; }
        if (System.IO.Directory.Exists(target)) target = System.IO.Path.Combine(target, snapshot.Name);
        var parent = System.IO.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
        System.IO.File.WriteAllBytes(target, ReadBytesRaw(name));
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
            if (entry.IsDirectory) { System.IO.Directory.CreateDirectory(target); continue; }
            EnsureNoReparseParents(root, target);
            var parent = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
            System.IO.File.WriteAllBytes(target, ReadBytesRaw(entry.FullName));
        }
    }

    public void ExtractAll(object? targetDirectory)
    {
        var root = XPScriptFileSystemRuntime.ResolvePath(targetDirectory);
        System.IO.Directory.CreateDirectory(root);
        using var reader = OpenReader();
        var count = 0;
        long total = 0;
        while (MoveNext(reader.Value))
        {
            if (++count > MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
            var entry = Proxy(CurrentEntry(reader.Value), count);
            Validate(entry);
            var target = SafePath(root, entry.Key);
            if (entry.IsDirectory) { System.IO.Directory.CreateDirectory(target); continue; }
            EnsureNoReparseParents(root, target);
            var parent = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
            using var input = OpenEntryStream(reader.Value);
            using var output = new System.IO.FileStream(target, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
            total = checked(total + CopyLimited(input, output, entry.Size));
            if (total > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
        }
    }

    private List<XPScriptArchiveEntry> Snapshots()
    {
        using var reader = OpenReader();
        var result = new List<XPScriptArchiveEntry>();
        long total = 0;
        while (MoveNext(reader.Value))
        {
            if (result.Count >= MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
            var proxy = Proxy(CurrentEntry(reader.Value), result.Count + 1);
            Validate(proxy);
            var snapshot = XPScriptArchiveEntry.FromExtended(proxy);
            total = checked(total + snapshot.Size);
            if (total > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
            result.Add(snapshot);
        }
        return result;
    }

    private byte[] ReadBytesRaw(object? entryName)
    {
        var wanted = Normalize(XPScriptRuntime.CStr(entryName));
        using var reader = OpenReader();
        var index = 0;
        while (MoveNext(reader.Value))
        {
            var proxy = Proxy(CurrentEntry(reader.Value), ++index);
            Validate(proxy);
            if (!proxy.Key.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            if (proxy.IsDirectory) throw new XPScriptRuntimeException(5, "Archive entry is a directory.");
            using var input = OpenEntryStream(reader.Value);
            using var output = new System.IO.MemoryStream();
            CopyLimited(input, output, proxy.Size);
            return output.ToArray();
        }
        throw new XPScriptRuntimeException(53, "Archive entry was not found.");
    }

    private string DetectFormat()
    {
        using var reader = OpenReader();
        var type = reader.Value.GetType().GetProperty("Type")?.GetValue(reader.Value)?.ToString();
        return string.IsNullOrWhiteSpace(type) ? "EXTENDED" : type.ToUpperInvariant();
    }

    private ReaderHandle OpenReader()
    {
        try
        {
            var assembly = System.Reflection.Assembly.Load("SharpCompress");
            var optionsType = assembly.GetType("SharpCompress.Readers.ReaderOptions", throwOnError: true)!;
            var options = Activator.CreateInstance(optionsType)!;
            if (!string.IsNullOrEmpty(Password)) optionsType.GetProperty("Password")?.SetValue(options, Password);
            var factoryType = assembly.GetType("SharpCompress.Readers.ReaderFactory", throwOnError: true)!;
            var method = factoryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "OpenReader" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(System.IO.Stream))
                ?? throw new MissingMethodException("SharpCompress ReaderFactory.OpenReader(Stream, ReaderOptions) was not found.");
            var stream = new System.IO.MemoryStream(_data, writable: false);
            try
            {
                var reader = method.Invoke(null, [stream, options]) ?? throw new XPScriptRuntimeException(5, "SharpCompress failed to open the in-memory archive reader.");
                return new ReaderHandle(reader, stream);
            }
            catch { stream.Dispose(); throw; }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new XPScriptRuntimeException(5, "Unable to open extended in-memory archive: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "Unable to load extended in-memory archive support: " + ex.Message);
        }
    }

    private static bool MoveNext(object reader) => Convert.ToBoolean(reader.GetType().GetMethod("MoveToNextEntry", Type.EmptyTypes)?.Invoke(reader, null) ?? false, System.Globalization.CultureInfo.InvariantCulture);
    private static object CurrentEntry(object reader) => reader.GetType().GetProperty("Entry")?.GetValue(reader) ?? throw new XPScriptRuntimeException(5, "SharpCompress reader entry is unavailable.");
    private static System.IO.Stream OpenEntryStream(object reader) => (System.IO.Stream)(reader.GetType().GetMethod("OpenEntryStream", Type.EmptyTypes)?.Invoke(reader, null) ?? throw new XPScriptRuntimeException(5, "SharpCompress entry stream is unavailable."));

    private static MemoryEntryProxy Proxy(object entry, int index)
    {
        var type = entry.GetType();
        object? Get(string name) => type.GetProperty(name)?.GetValue(entry);
        var key = Get("Key")?.ToString();
        if (string.IsNullOrWhiteSpace(key)) key = "entry-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        key = Normalize(key);
        var linkTarget = Get("LinkTarget")?.ToString();
        if (!string.IsNullOrEmpty(linkTarget)) throw new XPScriptRuntimeException(5, "Archive symbolic links are not allowed.");
        return new MemoryEntryProxy(
            key,
            Convert.ToInt64(Get("Size") ?? 0L, System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToInt64(Get("CompressedSize") ?? 0L, System.Globalization.CultureInfo.InvariantCulture),
            Get("CreatedTime") is DateTime created ? created : DateTime.MinValue,
            Get("LastModifiedTime") is DateTime modified ? modified : DateTime.MinValue,
            Convert.ToBoolean(Get("IsDirectory") ?? false, System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToBoolean(Get("IsEncrypted") ?? false, System.Globalization.CultureInfo.InvariantCulture),
            Get("Crc")?.ToString() ?? "");
    }

    private void Validate(MemoryEntryProxy entry)
    {
        if (entry.Size < 0 || entry.Size > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
        if (entry.CompressedSize > 0 && entry.Size > 0 && (double)entry.Size / entry.CompressedSize > MaxCompressionRatio)
            throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxCompressionRatio.");
    }

    private long CopyLimited(System.IO.Stream input, System.IO.Stream output, long declaredSize)
    {
        var buffer = new byte[128 * 1024];
        long written = 0;
        while (true)
        {
            var read = input.Read(buffer, 0, buffer.Length);
            if (read <= 0) break;
            written = checked(written + read);
            if (written > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
            output.Write(buffer, 0, read);
        }
        if (declaredSize >= 0 && written > declaredSize + 1024 * 1024)
            throw new XPScriptRuntimeException(5, "Archive entry expanded beyond its declared size.");
        return written;
    }

    private static void ThrowReadOnly() => throw new XPScriptRuntimeException(5, "Extended in-memory archives are read-only. Use Archive() for writable in-memory ZIP archives.");

    private static byte[] ToRawBytes(object? value)
    {
        if (value is byte[] raw) return raw;
        if (value is LSArray array)
        {
            if (!array.IsAllocated) return [];
            if (array.Rank != 1) throw new XPScriptRuntimeException(13, "Archive requires a one-dimensional Byte array.");
            var result = new byte[array.UBound() - array.LBound() + 1];
            var offset = 0;
            for (var i = array.LBound(); i <= array.UBound(); i++) result[offset++] = Convert.ToByte(array.Get(i), System.Globalization.CultureInfo.InvariantCulture);
            return result;
        }
        throw new XPScriptRuntimeException(13, "Archive requires a Byte array.");
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

    private sealed class ReaderHandle : IDisposable
    {
        public object Value { get; }
        private readonly System.IO.Stream _stream;
        public ReaderHandle(object value, System.IO.Stream stream) { Value = value; _stream = stream; }
        public void Dispose() { if (Value is IDisposable disposable) disposable.Dispose(); _stream.Dispose(); }
    }

    private sealed class MemoryEntryProxy
    {
        public string Key { get; }
        public long Size { get; }
        public long CompressedSize { get; }
        public DateTime CreatedTime { get; }
        public DateTime LastModifiedTime { get; }
        public bool IsDirectory { get; }
        public bool IsEncrypted { get; }
        public string Crc { get; }
        public MemoryEntryProxy(string key, long size, long compressedSize, DateTime created, DateTime modified, bool isDirectory, bool isEncrypted, string crc)
        { Key = key; Size = size; CompressedSize = compressedSize; CreatedTime = created; LastModifiedTime = modified; IsDirectory = isDirectory; IsEncrypted = isEncrypted; Crc = crc; }
    }
}
""";
}
