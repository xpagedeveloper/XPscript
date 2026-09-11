namespace XPScript.Compiler;

internal static class ArchiveRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptArchive
{
    private readonly string? _path;

    public XPScriptArchive(object? path = null, bool extendedSupport = false)
    {
        ExtendedSupport = extendedSupport;
        if (path is null) return;
        var text = XPScriptRuntime.CStr(path);
        if (!string.IsNullOrWhiteSpace(text)) _path = XPScriptFileSystemRuntime.ResolvePath(text);
    }

    public string Path => _path ?? "";
    public string Format => string.IsNullOrEmpty(_path) ? "" : System.IO.Path.GetExtension(_path).TrimStart('.').ToUpperInvariant();
    public bool Exists => _path is not null && System.IO.File.Exists(_path);
    public bool ExtendedSupport { get; }
    public bool IsEncrypted => false;
    public bool IsReadOnly => !Format.Equals("ZIP", StringComparison.OrdinalIgnoreCase);
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

    public void Open()
    {
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        Validate(archive.Entries);
    }

    public void Close() { }
    public void Save() { }

    public void Create(object? format = null)
    {
        EnsurePath();
        var requested = format is null ? Format : XPScriptRuntime.CStr(format).Trim().TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrEmpty(requested)) requested = "ZIP";
        if (!requested.Equals("ZIP", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, ExtendedSupport
                ? "Extended archive creation is not implemented yet."
                : "Archive creation supports ZIP only. Create the Archive with extendedSupport=True to enable additional formats.");
        var parent = System.IO.Path.GetDirectoryName(_path!);
        if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
        using var stream = new System.IO.FileStream(_path!, System.IO.FileMode.Create, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, false);
    }

    public void AddFile(object? sourcePath, object? archivePath = null)
    {
        EnsureWritable();
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.File.Exists(source)) throw new XPScriptRuntimeException(53, "Archive source file was not found.");
        var name = Normalize(archivePath is null ? System.IO.Path.GetFileName(source) : XPScriptRuntime.CStr(archivePath));
        Add(name, System.IO.File.ReadAllBytes(source), System.IO.File.GetLastWriteTimeUtc(source));
    }

    public void AddText(object? archivePath, object? text) =>
        Add(Normalize(XPScriptRuntime.CStr(archivePath)), System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(text)), DateTime.UtcNow);

    public bool Remove(object? entryName)
    {
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Update);
        var entry = archive.GetEntry(Normalize(XPScriptRuntime.CStr(entryName)));
        if (entry is null) return false;
        entry.Delete();
        return true;
    }

    public bool Contains(object? entryName)
    {
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        return archive.GetEntry(Normalize(XPScriptRuntime.CStr(entryName))) is not null;
    }

    public XPScriptArchiveEntry? GetEntry(object? entryName)
    {
        var name = Normalize(XPScriptRuntime.CStr(entryName));
        return Snapshots().FirstOrDefault(x => x.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public LSArray Files() => Pack(Snapshots().Where(x => !x.IsDirectory).Cast<object?>());
    public LSArray Folders() => Pack(Snapshots().Where(x => x.IsDirectory).Cast<object?>());

    public string ReadText(object? entryName) => System.Text.Encoding.UTF8.GetString(ReadBytesRaw(entryName));
    public LSArray ReadBytes(object? entryName) => PackBytes(ReadBytesRaw(entryName));

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
            var parent = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
            using var input = entry.Open();
            using var output = new System.IO.FileStream(target, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
            input.CopyTo(output);
        }
    }

    public LSArray ToBytes()
    {
        EnsurePath();
        if (!System.IO.File.Exists(_path!)) throw new XPScriptRuntimeException(53, "Archive file was not found.");
        return PackBytes(System.IO.File.ReadAllBytes(_path!));
    }

    private void Add(string name, byte[] bytes, DateTime modified)
    {
        EnsureWritable();
        if (!System.IO.File.Exists(_path!)) Create("zip");
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Update);
        archive.GetEntry(name)?.Delete();
        var level = CompressionLevel <= 0 ? System.IO.Compression.CompressionLevel.NoCompression
            : CompressionLevel <= 3 ? System.IO.Compression.CompressionLevel.Fastest
            : CompressionLevel >= 9 ? System.IO.Compression.CompressionLevel.SmallestSize
            : System.IO.Compression.CompressionLevel.Optimal;
        var entry = archive.CreateEntry(name, level);
        entry.LastWriteTime = new DateTimeOffset(modified.ToUniversalTime());
        using var output = entry.Open();
        output.Write(bytes, 0, bytes.Length);
    }

    private byte[] ReadBytesRaw(object? entryName)
    {
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        Validate(archive.Entries);
        var entry = archive.GetEntry(Normalize(XPScriptRuntime.CStr(entryName))) ?? throw new XPScriptRuntimeException(53, "Archive entry was not found.");
        using var input = entry.Open();
        using var output = new System.IO.MemoryStream();
        input.CopyTo(output);
        if (output.Length > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
        return output.ToArray();
    }

    private List<XPScriptArchiveEntry> Snapshots()
    {
        if (!Exists) return [];
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        Validate(archive.Entries);
        return archive.Entries.Select(x => new XPScriptArchiveEntry(x)).ToList();
    }

    private System.IO.Compression.ZipArchive OpenZip(System.IO.Compression.ZipArchiveMode mode)
    {
        EnsureWritable();
        if (!System.IO.File.Exists(_path!)) throw new XPScriptRuntimeException(53, "Archive file was not found.");
        var access = mode == System.IO.Compression.ZipArchiveMode.Read ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite;
        var stream = new System.IO.FileStream(_path!, System.IO.FileMode.Open, access, System.IO.FileShare.None);
        try { return new System.IO.Compression.ZipArchive(stream, mode, false); }
        catch { stream.Dispose(); throw; }
    }

    private void EnsurePath()
    {
        if (string.IsNullOrWhiteSpace(_path)) throw new XPScriptRuntimeException(5, "Archive requires a file path.");
    }

    private void EnsureWritable()
    {
        EnsurePath();
        if (!Format.Equals("ZIP", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, ExtendedSupport
                ? "Extended archive format support is enabled but the SharpCompress backend is not implemented yet."
                : "Archive supports ZIP only. Create the Archive with extendedSupport=True to enable additional formats.");
        if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Archive passwords are not implemented yet.");
    }

    private void Validate(IEnumerable<System.IO.Compression.ZipArchiveEntry> entries)
    {
        var count = 0;
        long total = 0;
        foreach (var entry in entries)
        {
            if (++count > MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
            _ = Normalize(entry.FullName);
            if (IsSymlink(entry)) throw new XPScriptRuntimeException(5, "Archive symbolic links are not allowed.");
            total = checked(total + entry.Length);
            if (total > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
            if (entry.CompressedLength > 0 && entry.Length > 0 && (double)entry.Length / entry.CompressedLength > MaxCompressionRatio)
                throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxCompressionRatio.");
        }
    }

    private static bool IsSymlink(System.IO.Compression.ZipArchiveEntry entry) => ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;

    private static string Normalize(string value)
    {
        var name = (value ?? "").Replace('\\', '/').Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Archive entry name must not be empty.");
        if (name.StartsWith("/", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z]:"))
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

internal sealed class XPScriptArchiveEntry
{
    public string Name { get; }
    public string FullName { get; }
    public string Extension { get; }
    public long Size { get; }
    public long CompressedSize { get; }
    public double CompressionRatio { get; }
    public DateTime Created { get; }
    public DateTime Modified { get; }
    public bool IsDirectory { get; }
    public bool IsEncrypted => false;
    public string CRC => "";

    public XPScriptArchiveEntry(System.IO.Compression.ZipArchiveEntry entry)
    {
        FullName = entry.FullName;
        IsDirectory = FullName.EndsWith("/", StringComparison.Ordinal);
        Name = System.IO.Path.GetFileName(FullName.TrimEnd('/'));
        Extension = IsDirectory ? "" : System.IO.Path.GetExtension(Name);
        Size = IsDirectory ? 0 : entry.Length;
        CompressedSize = IsDirectory ? 0 : entry.CompressedLength;
        CompressionRatio = CompressedSize <= 0 ? 0d : (double)Size / CompressedSize;
        Modified = entry.LastWriteTime.UtcDateTime;
        Created = Modified;
    }
}
""";
}
