namespace XPScript.Compiler;

internal static class ArchiveMemoryRuntimeSource
{
    public const string Code = """
internal static class XPScriptArchiveFactory
{
    public static object Create() => new XPScriptMemoryArchive();

    public static object Create(object? value)
    {
        if (IsBytes(value)) return new XPScriptMemoryArchive(value);
        return new XPScriptArchive(value);
    }

    public static object Create(object? value, bool extendedSupport)
    {
        if (extendedSupport) throw new XPScriptRuntimeException(5, "Extended Archive construction must use the extended Archive factory.");
        return Create(value);
    }

    internal static bool IsBytes(object? value) => value is byte[] || value is LSArray;
}

internal static class XPScriptExtendedArchiveFactory
{
    public static object Create(object? value)
    {
        if (XPScriptArchiveFactory.IsBytes(value))
            throw new XPScriptRuntimeException(5, "Extended in-memory archive support is not implemented yet. Use Archive(bytes) for ZIP or a file path with Archive(path, True) for extended formats.");
        var type = typeof(XPScriptArchive).Assembly.GetType("XPScriptExtendedArchive")
            ?? throw new XPScriptRuntimeException(5, "Extended Archive runtime was not emitted by the compiler.");
        return Activator.CreateInstance(type, [value])
            ?? throw new XPScriptRuntimeException(5, "Extended Archive could not be created.");
    }
}

internal sealed class XPScriptMemoryArchive
{
    private readonly System.IO.MemoryStream _data;
    private bool _created;

    public XPScriptMemoryArchive(object? bytes = null)
    {
        byte[] raw = bytes is null ? [] : ToRawBytes(bytes);
        _data = new System.IO.MemoryStream();
        if (raw.Length > 0)
        {
            _data.Write(raw, 0, raw.Length);
            _data.Position = 0;
            _created = true;
        }
    }

    public string Path => "";
    public string Format => "ZIP";
    public bool Exists => _created;
    public bool ExtendedSupport => false;
    public bool IsEncrypted => false;
    public bool IsReadOnly => false;
    public string Password { get; set; } = "";
    public int CompressionLevel { get; set; } = 5;
    public long MaxExtractSize { get; set; } = 2L * 1024 * 1024 * 1024;
    public int MaxEntries { get; set; } = 10000;
    public double MaxCompressionRatio { get; set; } = 1000d;

    public long FileCount => Snapshots().LongCount(x => !x.IsDirectory);
    public long FolderCount => Snapshots().LongCount(x => x.IsDirectory);
    public long CompressedSize => Snapshots().Sum(x => x.CompressedSize);
    public long UncompressedSize => Snapshots().Sum(x => x.Size);
    public LSArray Entries => Pack(Snapshots().Cast<object?>());

    public void Create(object? format = null)
    {
        var requested = format is null ? "ZIP" : XPScriptRuntime.CStr(format).Trim().TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrEmpty(requested)) requested = "ZIP";
        if (!requested.Equals("ZIP", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, "In-memory Archive creation currently supports ZIP only.");
        if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Password-protected in-memory ZIP writing is not implemented yet.");
        _data.SetLength(0);
        _data.Position = 0;
        using (var archive = new System.IO.Compression.ZipArchive(_data, System.IO.Compression.ZipArchiveMode.Create, true)) { }
        _data.Position = 0;
        _created = true;
    }

    public void Open()
    {
        EnsureCreated();
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        Validate(archive.Entries);
    }

    public void Close() { }
    public void Save() { }

    public void AddFile(object? sourcePath, object? archivePath = null)
    {
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.File.Exists(source)) throw new XPScriptRuntimeException(53, "Archive source file was not found.");
        if ((System.IO.File.GetAttributes(source) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Archive.AddFile does not allow symbolic links or reparse points.");
        var name = Normalize(archivePath is null ? System.IO.Path.GetFileName(source) : XPScriptRuntime.CStr(archivePath));
        Add(name, System.IO.File.ReadAllBytes(source), System.IO.File.GetLastWriteTimeUtc(source));
    }

    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true)
    {
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.Directory.Exists(source)) throw new XPScriptRuntimeException(76, "Archive source folder was not found.");
        if ((System.IO.File.GetAttributes(source) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Archive.AddFolder does not allow a symbolic link or reparse point as the source folder.");
        var rootName = archivePath is null
            ? System.IO.Path.GetFileName(source.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
            : XPScriptRuntime.CStr(archivePath);
        rootName = Normalize(rootName).TrimEnd('/');
        AddDirectoryEntry(rootName + "/");
        AddFolderTree(source, source, rootName, recursive);
    }

    private void AddFolderTree(string sourceRoot, string currentDirectory, string archiveRoot, bool recursive)
    {
        foreach (var file in System.IO.Directory.EnumerateFiles(currentDirectory, "*", System.IO.SearchOption.TopDirectoryOnly))
        {
            if ((System.IO.File.GetAttributes(file) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
            var relative = System.IO.Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
            AddFile(file, Normalize(archiveRoot + "/" + relative));
        }
        if (!recursive) return;
        foreach (var directory in System.IO.Directory.EnumerateDirectories(currentDirectory, "*", System.IO.SearchOption.TopDirectoryOnly))
        {
            if ((System.IO.File.GetAttributes(directory) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
            var relative = System.IO.Path.GetRelativePath(sourceRoot, directory).Replace('\\', '/');
            AddDirectoryEntry(Normalize(archiveRoot + "/" + relative + "/"));
            AddFolderTree(sourceRoot, directory, archiveRoot, true);
        }
    }

    public void AddText(object? archivePath, object? text) =>
        Add(Normalize(XPScriptRuntime.CStr(archivePath)), System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(text)), DateTime.UtcNow);

    public void AddBytes(object? archivePath, object? bytes) =>
        Add(Normalize(XPScriptRuntime.CStr(archivePath)), ToRawBytes(bytes), DateTime.UtcNow);

    public bool Remove(object? entryName)
    {
        EnsureCreated();
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Update);
        var name = Normalize(XPScriptRuntime.CStr(entryName));
        var matches = archive.Entries.Where(x => x.FullName.Equals(name, StringComparison.OrdinalIgnoreCase) || x.FullName.StartsWith(name.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var entry in matches) entry.Delete();
        return matches.Count > 0;
    }

    public bool Rename(object? entryName, object? newName)
    {
        EnsureCreated();
        var oldKey = Normalize(XPScriptRuntime.CStr(entryName));
        var newKey = Normalize(XPScriptRuntime.CStr(newName));
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Update);
        var entries = archive.Entries.Where(x => x.FullName.Equals(oldKey, StringComparison.OrdinalIgnoreCase) || x.FullName.StartsWith(oldKey.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)).ToList();
        if (entries.Count == 0) return false;
        foreach (var entry in entries)
        {
            var suffix = entry.FullName.Length == oldKey.Length ? "" : entry.FullName[oldKey.TrimEnd('/').Length..];
            var targetName = Normalize(newKey.TrimEnd('/') + suffix + (entry.FullName.EndsWith("/", StringComparison.Ordinal) && !suffix.EndsWith("/", StringComparison.Ordinal) ? "/" : ""));
            var replacement = archive.CreateEntry(targetName, ResolveCompressionLevel());
            replacement.LastWriteTime = entry.LastWriteTime;
            if (!entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                using var input = entry.Open();
                using var output = replacement.Open();
                input.CopyTo(output);
            }
        }
        foreach (var entry in entries) entry.Delete();
        return true;
    }

    public bool Contains(object? entryName)
    {
        var name = Normalize(XPScriptRuntime.CStr(entryName));
        return Snapshots().Any(x => x.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public XPScriptArchiveEntry? GetEntry(object? entryName)
    {
        var name = Normalize(XPScriptRuntime.CStr(entryName));
        return Snapshots().FirstOrDefault(x => x.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
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
        var root = XPScriptFileSystemRuntime.ResolvePath(targetDirectory);
        System.IO.Directory.CreateDirectory(root);
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        Validate(archive.Entries);
        foreach (var entry in archive.Entries)
        {
            var name = Normalize(entry.FullName);
            var target = SafePath(root, name);
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
            {
                System.IO.Directory.CreateDirectory(target);
                continue;
            }
            EnsureNoReparseParents(root, target);
            var parent = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
            using var input = entry.Open();
            using var output = new System.IO.FileStream(target, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
            CopyLimited(input, output, entry.Length);
        }
    }

    public LSArray ToBytes()
    {
        EnsureCreated();
        return PackBytes(_data.ToArray());
    }

    private void Add(string name, byte[] bytes, DateTime modified)
    {
        EnsureWritable();
        if (!_created) Create("zip");
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Update);
        archive.GetEntry(name)?.Delete();
        var entry = archive.CreateEntry(name, ResolveCompressionLevel());
        entry.LastWriteTime = new DateTimeOffset(modified.ToUniversalTime());
        using var output = entry.Open();
        output.Write(bytes, 0, bytes.Length);
    }

    private void AddDirectoryEntry(string name)
    {
        EnsureWritable();
        if (!_created) Create("zip");
        name = Normalize(name.TrimEnd('/') + "/");
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Update);
        if (archive.GetEntry(name) is null) archive.CreateEntry(name, System.IO.Compression.CompressionLevel.NoCompression);
    }

    private byte[] ReadBytesRaw(object? entryName)
    {
        EnsureCreated();
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        Validate(archive.Entries);
        var entry = archive.GetEntry(Normalize(XPScriptRuntime.CStr(entryName))) ?? throw new XPScriptRuntimeException(53, "Archive entry was not found.");
        using var input = entry.Open();
        using var output = new System.IO.MemoryStream();
        CopyLimited(input, output, entry.Length);
        return output.ToArray();
    }

    private List<XPScriptArchiveEntry> Snapshots()
    {
        if (!_created) return [];
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        Validate(archive.Entries);
        return archive.Entries.Select(x => new XPScriptArchiveEntry(x)).ToList();
    }

    private System.IO.Compression.ZipArchive OpenZip(System.IO.Compression.ZipArchiveMode mode)
    {
        EnsureCreated();
        if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Archive passwords are not implemented for in-memory ZIP yet.");
        _data.Position = 0;
        return new System.IO.Compression.ZipArchive(_data, mode, true);
    }

    private void EnsureCreated()
    {
        if (!_created) throw new XPScriptRuntimeException(5, "In-memory Archive has not been created or loaded.");
    }

    private void EnsureWritable()
    {
        if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Archive passwords are not implemented for in-memory ZIP writing yet.");
    }

    private System.IO.Compression.CompressionLevel ResolveCompressionLevel() =>
        CompressionLevel <= 0 ? System.IO.Compression.CompressionLevel.NoCompression
        : CompressionLevel <= 3 ? System.IO.Compression.CompressionLevel.Fastest
        : CompressionLevel >= 9 ? System.IO.Compression.CompressionLevel.SmallestSize
        : System.IO.Compression.CompressionLevel.Optimal;

    private void Validate(IEnumerable<System.IO.Compression.ZipArchiveEntry> entries)
    {
        var count = 0;
        long total = 0;
        foreach (var entry in entries)
        {
            if (++count > MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
            _ = Normalize(entry.FullName);
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new XPScriptRuntimeException(5, "Archive symbolic links are not allowed.");
            total = checked(total + entry.Length);
            if (total > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
            if (entry.CompressedLength > 0 && entry.Length > 0 && (double)entry.Length / entry.CompressedLength > MaxCompressionRatio)
                throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxCompressionRatio.");
        }
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
        throw new XPScriptRuntimeException(13, "Archive requires a Byte array or file path.");
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
}
""";
}
