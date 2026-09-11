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
    public bool IsEncrypted => Exists && Snapshots().Any(x => x.IsEncrypted);
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
        EnsurePath();
        if (UseExtendedBackend)
        {
            using var archive = OpenExtendedArchive();
            _ = SnapshotExtendedEntries(archive.Value);
            return;
        }
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        ValidateZip(archive.Entries);
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

    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true)
    {
        EnsureWritable();
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
        EnsureWritable();
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Update);
        var name = Normalize(XPScriptRuntime.CStr(entryName));
        var matches = archive.Entries.Where(x => x.FullName.Equals(name, StringComparison.OrdinalIgnoreCase) || x.FullName.StartsWith(name.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var entry in matches) entry.Delete();
        return matches.Count > 0;
    }

    public bool Rename(object? entryName, object? newName)
    {
        EnsureWritable();
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
        var target = XPScriptFileSystemRuntime.ResolvePath(targetPath);
        var snapshot = GetEntry(name) ?? throw new XPScriptRuntimeException(53, "Archive entry was not found.");
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
        if (UseExtendedBackend)
        {
            ExtractAllExtended(root);
            return;
        }

        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        ValidateZip(archive.Entries);
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
        EnsurePath();
        if (!System.IO.File.Exists(_path!)) throw new XPScriptRuntimeException(53, "Archive file was not found.");
        return PackBytes(System.IO.File.ReadAllBytes(_path!));
    }

    private bool UseExtendedBackend => ExtendedSupport && (!Format.Equals("ZIP", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(Password));

    private void Add(string name, byte[] bytes, DateTime modified)
    {
        EnsureWritable();
        if (!System.IO.File.Exists(_path!)) Create("zip");
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
        if (!System.IO.File.Exists(_path!)) Create("zip");
        using var archive = OpenZip(System.IO.Compression.ZipArchiveMode.Update);
        if (archive.GetEntry(name) is null) archive.CreateEntry(name, System.IO.Compression.CompressionLevel.NoCompression);
    }

    private System.IO.Compression.CompressionLevel ResolveCompressionLevel() =>
        CompressionLevel <= 0 ? System.IO.Compression.CompressionLevel.NoCompression
        : CompressionLevel <= 3 ? System.IO.Compression.CompressionLevel.Fastest
        : CompressionLevel >= 9 ? System.IO.Compression.CompressionLevel.SmallestSize
        : System.IO.Compression.CompressionLevel.Optimal;

    private byte[] ReadBytesRaw(object? entryName)
    {
        var wanted = Normalize(XPScriptRuntime.CStr(entryName));
        if (UseExtendedBackend)
        {
            using var archive = OpenExtendedArchive();
            foreach (var entry in EnumerateExtendedEntries(archive.Value))
            {
                var key = Normalize(GetStringProperty(entry, "Key"));
                if (!key.Equals(wanted, StringComparison.OrdinalIgnoreCase)) continue;
                ValidateExtendedEntry(entry);
                if (GetBoolProperty(entry, "IsDirectory")) throw new XPScriptRuntimeException(5, "Archive entry is a directory.");
                using var input = OpenExtendedEntryStream(entry);
                using var output = new System.IO.MemoryStream();
                CopyLimited(input, output, GetLongProperty(entry, "Size"));
                return output.ToArray();
            }
            throw new XPScriptRuntimeException(53, "Archive entry was not found.");
        }

        using var zip = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        ValidateZip(zip.Entries);
        var zipEntry = zip.GetEntry(wanted) ?? throw new XPScriptRuntimeException(53, "Archive entry was not found.");
        using var zipInput = zipEntry.Open();
        using var zipOutput = new System.IO.MemoryStream();
        CopyLimited(zipInput, zipOutput, zipEntry.Length);
        return zipOutput.ToArray();
    }

    private List<XPScriptArchiveEntry> Snapshots()
    {
        if (!Exists) return [];
        if (UseExtendedBackend)
        {
            using var archive = OpenExtendedArchive();
            return SnapshotExtendedEntries(archive.Value);
        }
        if (!Format.Equals("ZIP", StringComparison.OrdinalIgnoreCase)) EnsureExtendedEnabled();
        using var zip = OpenZip(System.IO.Compression.ZipArchiveMode.Read);
        ValidateZip(zip.Entries);
        return zip.Entries.Select(x => new XPScriptArchiveEntry(x)).ToList();
    }

    private System.IO.Compression.ZipArchive OpenZip(System.IO.Compression.ZipArchiveMode mode)
    {
        EnsurePath();
        if (!Format.Equals("ZIP", StringComparison.OrdinalIgnoreCase)) EnsureExtendedEnabled();
        if (!System.IO.File.Exists(_path!)) throw new XPScriptRuntimeException(53, "Archive file was not found.");
        if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Password-protected ZIP requires extended archive support.");
        var access = mode == System.IO.Compression.ZipArchiveMode.Read ? System.IO.FileAccess.Read : System.IO.FileAccess.ReadWrite;
        var stream = new System.IO.FileStream(_path!, System.IO.FileMode.Open, access, System.IO.FileShare.None);
        try { return new System.IO.Compression.ZipArchive(stream, mode, false); }
        catch { stream.Dispose(); throw; }
    }

    private void EnsurePath()
    {
        if (string.IsNullOrWhiteSpace(_path)) throw new XPScriptRuntimeException(5, "Archive requires a file path.");
    }

    private void EnsureExtendedEnabled()
    {
        EnsurePath();
        if (!ExtendedSupport)
            throw new XPScriptRuntimeException(5, "Archive supports ZIP only. Create the Archive with extendedSupport=True to enable additional formats.");
    }

    private void EnsureWritable()
    {
        EnsurePath();
        if (!Format.Equals("ZIP", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, ExtendedSupport
                ? "This archive format is currently read-only in the XPScript extended backend."
                : "Archive supports ZIP only. Create the Archive with extendedSupport=True to enable additional formats.");
        if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Archive passwords are not implemented for ZIP writing yet.");
    }

    private void ValidateZip(IEnumerable<System.IO.Compression.ZipArchiveEntry> entries)
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

    private List<XPScriptArchiveEntry> SnapshotExtendedEntries(object archive)
    {
        var result = new List<XPScriptArchiveEntry>();
        long total = 0;
        foreach (var entry in EnumerateExtendedEntries(archive))
        {
            if (result.Count >= MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
            ValidateExtendedEntry(entry);
            var snapshot = XPScriptArchiveEntry.FromExtended(entry);
            total = checked(total + snapshot.Size);
            if (total > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
            result.Add(snapshot);
        }
        return result;
    }

    private void ValidateExtendedEntry(object entry)
    {
        _ = Normalize(GetStringProperty(entry, "Key"));
        var linkTarget = GetNullableStringProperty(entry, "LinkTarget");
        if (!string.IsNullOrEmpty(linkTarget)) throw new XPScriptRuntimeException(5, "Archive symbolic links are not allowed.");
        var size = GetLongProperty(entry, "Size");
        var compressed = GetLongProperty(entry, "CompressedSize");
        if (size < 0 || size > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
        if (compressed > 0 && size > 0 && (double)size / compressed > MaxCompressionRatio)
            throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxCompressionRatio.");
    }

    private void ExtractAllExtended(string root)
    {
        using var archive = OpenExtendedArchive();
        long totalWritten = 0;
        var count = 0;
        foreach (var entry in EnumerateExtendedEntries(archive.Value))
        {
            if (++count > MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
            ValidateExtendedEntry(entry);
            var name = Normalize(GetStringProperty(entry, "Key"));
            var target = SafePath(root, name);
            if (GetBoolProperty(entry, "IsDirectory"))
            {
                System.IO.Directory.CreateDirectory(target);
                continue;
            }
            EnsureNoReparseParents(root, target);
            var parent = System.IO.Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
            using var input = OpenExtendedEntryStream(entry);
            using var output = new System.IO.FileStream(target, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
            totalWritten = checked(totalWritten + CopyLimited(input, output, GetLongProperty(entry, "Size")));
            if (totalWritten > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
        }
    }

    private ExtendedArchiveHandle OpenExtendedArchive()
    {
        EnsureExtendedEnabled();
        if (!System.IO.File.Exists(_path!)) throw new XPScriptRuntimeException(53, "Archive file was not found.");
        try
        {
            var assembly = System.Reflection.Assembly.Load("SharpCompress");
            var optionsType = assembly.GetType("SharpCompress.Readers.ReaderOptions", throwOnError: true)!;
            var options = Activator.CreateInstance(optionsType)!;
            if (!string.IsNullOrEmpty(Password)) optionsType.GetProperty("Password")?.SetValue(options, Password);
            optionsType.GetProperty("ExtensionHint")?.SetValue(options, Format.ToLowerInvariant());
            var factoryType = assembly.GetType("SharpCompress.Archives.ArchiveFactory", throwOnError: true)!;
            var method = factoryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "OpenArchive" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(string) && m.GetParameters()[1].ParameterType == optionsType)
                ?? throw new MissingMethodException("SharpCompress ArchiveFactory.OpenArchive(string, ReaderOptions) was not found.");
            var archive = method.Invoke(null, [_path!, options]) ?? throw new XPScriptRuntimeException(5, "SharpCompress failed to open the archive.");
            return new ExtendedArchiveHandle(archive);
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new XPScriptRuntimeException(5, "Unable to open extended archive: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "Unable to load SharpCompress extended archive support: " + ex.Message);
        }
    }

    private static IEnumerable<object> EnumerateExtendedEntries(object archive)
    {
        var entries = archive.GetType().GetProperty("Entries")?.GetValue(archive) as System.Collections.IEnumerable
            ?? throw new XPScriptRuntimeException(5, "SharpCompress archive entries are unavailable.");
        foreach (var entry in entries) if (entry is not null) yield return entry;
    }

    private static System.IO.Stream OpenExtendedEntryStream(object entry)
    {
        try
        {
            return (System.IO.Stream)(entry.GetType().GetMethod("OpenEntryStream", Type.EmptyTypes)?.Invoke(entry, null)
                ?? throw new MissingMethodException("SharpCompress entry OpenEntryStream() was not found."));
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new XPScriptRuntimeException(5, "Unable to read archive entry: " + (ex.InnerException?.Message ?? ex.Message));
        }
    }

    private long CopyLimited(System.IO.Stream input, System.IO.Stream output, long declaredSize)
    {
        const int bufferSize = 128 * 1024;
        var buffer = new byte[bufferSize];
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

    private static byte[] ToRawBytes(object? value)
    {
        if (value is byte[] raw) return raw;
        if (value is LSArray array)
        {
            if (!array.IsAllocated) return [];
            if (array.Rank != 1) throw new XPScriptRuntimeException(13, "Archive.AddBytes requires a one-dimensional Byte array.");
            var result = new byte[array.UBound() - array.LBound() + 1];
            var offset = 0;
            for (var i = array.LBound(); i <= array.UBound(); i++) result[offset++] = Convert.ToByte(array.Get(i), System.Globalization.CultureInfo.InvariantCulture);
            return result;
        }
        throw new XPScriptRuntimeException(13, "Archive.AddBytes requires a Byte array.");
    }

    private static string GetStringProperty(object value, string property) =>
        value.GetType().GetProperty(property)?.GetValue(value)?.ToString() ?? "";

    private static string? GetNullableStringProperty(object value, string property) =>
        value.GetType().GetProperty(property)?.GetValue(value)?.ToString();

    private static long GetLongProperty(object value, string property)
    {
        var raw = value.GetType().GetProperty(property)?.GetValue(value);
        return raw is null ? 0L : Convert.ToInt64(raw, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool GetBoolProperty(object value, string property)
    {
        var raw = value.GetType().GetProperty(property)?.GetValue(value);
        return raw is not null && Convert.ToBoolean(raw, System.Globalization.CultureInfo.InvariantCulture);
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

    private sealed class ExtendedArchiveHandle : IDisposable
    {
        public object Value { get; }
        public ExtendedArchiveHandle(object value) => Value = value;
        public void Dispose()
        {
            if (Value is IDisposable disposable) disposable.Dispose();
        }
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
    public bool IsEncrypted { get; }
    public string CRC { get; }

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
        IsEncrypted = false;
        CRC = "";
    }

    private XPScriptArchiveEntry(string fullName, long size, long compressedSize, DateTime created, DateTime modified, bool isDirectory, bool isEncrypted, string crc)
    {
        FullName = fullName;
        IsDirectory = isDirectory;
        Name = System.IO.Path.GetFileName(fullName.TrimEnd('/'));
        Extension = isDirectory ? "" : System.IO.Path.GetExtension(Name);
        Size = isDirectory ? 0 : size;
        CompressedSize = isDirectory ? 0 : compressedSize;
        CompressionRatio = CompressedSize <= 0 ? 0d : (double)Size / CompressedSize;
        Created = created;
        Modified = modified;
        IsEncrypted = isEncrypted;
        CRC = crc;
    }

    public static XPScriptArchiveEntry FromExtended(object entry)
    {
        var type = entry.GetType();
        object? Get(string name) => type.GetProperty(name)?.GetValue(entry);
        var fullName = Get("Key")?.ToString() ?? "";
        var size = Convert.ToInt64(Get("Size") ?? 0L, System.Globalization.CultureInfo.InvariantCulture);
        var compressed = Convert.ToInt64(Get("CompressedSize") ?? 0L, System.Globalization.CultureInfo.InvariantCulture);
        var isDirectory = Convert.ToBoolean(Get("IsDirectory") ?? false, System.Globalization.CultureInfo.InvariantCulture);
        var isEncrypted = Convert.ToBoolean(Get("IsEncrypted") ?? false, System.Globalization.CultureInfo.InvariantCulture);
        var created = Get("CreatedTime") is DateTime createdValue ? createdValue : DateTime.MinValue;
        var modified = Get("LastModifiedTime") is DateTime modifiedValue ? modifiedValue : created;
        var crc = Get("Crc")?.ToString() ?? "";
        return new XPScriptArchiveEntry(fullName, size, compressed, created, modified, isDirectory, isEncrypted, crc);
    }
}
""";
}
