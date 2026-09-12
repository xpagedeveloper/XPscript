namespace XPScript.Compiler;

internal static class ArchiveCompressedTarRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptExtendedArchiveV3
{
    private readonly XPScriptExtendedArchiveV2 _inner;
    private readonly List<CompressedTarEntry> _entries = [];
    private bool _compressedTarMode;
    private string _compression = "";
    private bool _suppressAutoSave;

    public XPScriptExtendedArchiveV3(object? path = null)
    {
        _inner = new XPScriptExtendedArchiveV2(path);
    }

    public string Path => _inner.Path;
    public string Format => _compressedTarMode ? "TAR." + _compression.ToUpperInvariant() : DetectCompressedTarFormat(Path) ?? _inner.Format;
    public bool Exists => _inner.Exists;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => IsCompressedTarPath(Path) ? false : _inner.IsReadOnly;
    public string Password { get => _inner.Password; set => _inner.Password = value ?? ""; }
    public int CompressionLevel { get => _inner.CompressionLevel; set => _inner.CompressionLevel = value; }
    public long MaxExtractSize { get => _inner.MaxExtractSize; set => _inner.MaxExtractSize = value; }
    public int MaxEntries { get => _inner.MaxEntries; set => _inner.MaxEntries = value; }
    public double MaxCompressionRatio { get => _inner.MaxCompressionRatio; set => _inner.MaxCompressionRatio = value; }

    public bool IsEncrypted => _inner.IsEncrypted;
    public long FileCount => _inner.FileCount;
    public long FolderCount => _inner.FolderCount;
    public long CompressedSize => _inner.CompressedSize;
    public long UncompressedSize => _inner.UncompressedSize;
    public LSArray Entries => _inner.Entries;

    public void Open() => _inner.Open();
    public void Close() => _inner.Close();

    public void Create(object? format = null)
    {
        var requested = format is null ? DetectCompressedTarFormat(Path) ?? _inner.Format : XPScriptRuntime.CStr(format).Trim().TrimStart('.').ToUpperInvariant();
        if (TryNormalizeCompressedTar(requested, out var compression))
        {
            EnsurePath();
            if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Password-protected compressed TAR writing is not supported.");
            _compressedTarMode = true;
            _compression = compression;
            _entries.Clear();
            SaveCompressedTar();
            return;
        }

        _compressedTarMode = false;
        _compression = "";
        _entries.Clear();
        _inner.Create(format);
    }

    public void Save()
    {
        if (_compressedTarMode) SaveCompressedTar();
        else _inner.Save();
    }

    public void AddFile(object? sourcePath, object? archivePath = null)
    {
        if (!EnsureCompressedTarMutation())
        {
            _inner.AddFile(sourcePath, archivePath);
            return;
        }
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.File.Exists(source)) throw new XPScriptRuntimeException(53, "Archive source file was not found.");
        if ((System.IO.File.GetAttributes(source) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Archive.AddFile does not allow symbolic links or reparse points.");
        var name = Normalize(archivePath is null ? System.IO.Path.GetFileName(source) : XPScriptRuntime.CStr(archivePath));
        Upsert(new CompressedTarEntry(name, System.IO.File.ReadAllBytes(source), System.IO.File.GetLastWriteTimeUtc(source), false));
        AutoSave();
    }

    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true)
    {
        if (!EnsureCompressedTarMutation())
        {
            _inner.AddFolder(sourcePath, archivePath, recursive);
            return;
        }
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.Directory.Exists(source)) throw new XPScriptRuntimeException(76, "Archive source folder was not found.");
        if ((System.IO.File.GetAttributes(source) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Archive.AddFolder does not allow a symbolic link or reparse point as the source folder.");
        var rootName = archivePath is null
            ? System.IO.Path.GetFileName(source.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar))
            : XPScriptRuntime.CStr(archivePath);
        rootName = Normalize(rootName).TrimEnd('/');
        _suppressAutoSave = true;
        try
        {
            Upsert(new CompressedTarEntry(rootName + "/", null, DateTime.UtcNow, true));
            AddFolderTree(source, source, rootName, recursive);
        }
        finally
        {
            _suppressAutoSave = false;
            SaveCompressedTar();
        }
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
            Upsert(new CompressedTarEntry(Normalize(archiveRoot + "/" + relative + "/"), null, DateTime.UtcNow, true));
            AddFolderTree(sourceRoot, directory, archiveRoot, true);
        }
    }

    public void AddText(object? archivePath, object? text)
    {
        if (!EnsureCompressedTarMutation()) { _inner.AddText(archivePath, text); return; }
        Upsert(new CompressedTarEntry(Normalize(XPScriptRuntime.CStr(archivePath)), System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(text)), DateTime.UtcNow, false));
        AutoSave();
    }

    public void AddBytes(object? archivePath, object? bytes)
    {
        if (!EnsureCompressedTarMutation()) { _inner.AddBytes(archivePath, bytes); return; }
        Upsert(new CompressedTarEntry(Normalize(XPScriptRuntime.CStr(archivePath)), ToRawBytes(bytes), DateTime.UtcNow, false));
        AutoSave();
    }

    public bool Remove(object? entryName)
    {
        if (!EnsureCompressedTarMutation()) return _inner.Remove(entryName);
        var name = Normalize(XPScriptRuntime.CStr(entryName));
        var prefix = name.TrimEnd('/') + "/";
        var removed = _entries.RemoveAll(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (removed > 0) SaveCompressedTar();
        return removed > 0;
    }

    public bool Rename(object? entryName, object? newName)
    {
        if (!EnsureCompressedTarMutation()) return _inner.Rename(entryName, newName);
        var oldKey = Normalize(XPScriptRuntime.CStr(entryName));
        var newKey = Normalize(XPScriptRuntime.CStr(newName));
        var oldPrefix = oldKey.TrimEnd('/') + "/";
        var matches = _entries.Where(x => x.Name.Equals(oldKey, StringComparison.OrdinalIgnoreCase) || x.Name.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) return false;
        foreach (var item in matches)
        {
            var suffix = item.Name.Length == oldKey.Length ? "" : item.Name[oldKey.TrimEnd('/').Length..];
            item.Name = Normalize(newKey.TrimEnd('/') + suffix + (item.IsDirectory && !suffix.EndsWith("/", StringComparison.Ordinal) ? "/" : ""));
        }
        SaveCompressedTar();
        return true;
    }

    public bool Contains(object? entryName) => _inner.Contains(entryName);
    public XPScriptArchiveEntry? GetEntry(object? entryName) => _inner.GetEntry(entryName);
    public LSArray Files() => _inner.Files();
    public LSArray Folders() => _inner.Folders();
    public LSArray Find(object? pattern) => _inner.Find(pattern);
    public string ReadText(object? entryName) => _inner.ReadText(entryName);
    public LSArray ReadBytes(object? entryName) => _inner.ReadBytes(entryName);
    public void Extract(object? entryName, object? targetPath) => _inner.Extract(entryName, targetPath);
    public void ExtractFolder(object? folderName, object? targetDirectory) => _inner.ExtractFolder(folderName, targetDirectory);
    public void ExtractAll(object? targetDirectory) => _inner.ExtractAll(targetDirectory);
    public LSArray ToBytes() => _inner.ToBytes();

    private bool EnsureCompressedTarMutation()
    {
        if (_compressedTarMode) return true;
        var detected = DetectCompressedTarFormat(Path);
        if (detected is null) return false;
        if (!string.IsNullOrEmpty(Password) || _inner.IsEncrypted)
            throw new XPScriptRuntimeException(5, "Encrypted compressed TAR archives cannot be rebuilt safely yet.");
        if (!TryNormalizeCompressedTar(detected, out var compression)) return false;
        _compressedTarMode = true;
        _compression = compression;
        _entries.Clear();
        if (!Exists) return true;
        var values = _inner.Entries;
        if (values.IsAllocated)
        {
            for (var i = values.LBound(); i <= values.UBound(); i++)
            {
                if (values.Get(i) is not XPScriptArchiveEntry entry) continue;
                if (entry.IsEncrypted) throw new XPScriptRuntimeException(5, "Encrypted compressed TAR archives cannot be rebuilt safely yet.");
                if (entry.IsDirectory) _entries.Add(new CompressedTarEntry(Normalize(entry.FullName.TrimEnd('/') + "/"), null, entry.Modified, true));
                else _entries.Add(new CompressedTarEntry(Normalize(entry.FullName), ToRawBytes(_inner.ReadBytes(entry.FullName)), entry.Modified, false));
            }
        }
        return true;
    }

    private void Upsert(CompressedTarEntry entry)
    {
        if (_entries.Count >= MaxEntries && !_entries.Any(x => x.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
        if (entry.Bytes is not null && entry.Bytes.LongLength > MaxExtractSize)
            throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
        _entries.RemoveAll(x => x.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase));
        _entries.Add(entry);
    }

    private void AutoSave()
    {
        if (!_suppressAutoSave) SaveCompressedTar();
    }

    private void SaveCompressedTar()
    {
        EnsurePath();
        if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Password-protected compressed TAR writing is not supported.");
        long total = 0;
        foreach (var entry in _entries)
        {
            if (entry.Bytes is null) continue;
            total = checked(total + entry.Bytes.LongLength);
            if (total > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
        }
        XPScriptCompressedTarWriter.WriteAtomic(Path, _compression, _entries, CompressionLevel);
    }

    private void EnsurePath()
    {
        if (string.IsNullOrWhiteSpace(Path)) throw new XPScriptRuntimeException(5, "Compressed TAR creation requires a file path.");
    }

    private static bool IsCompressedTarPath(string path) => DetectCompressedTarFormat(path) is not null;

    private static string? DetectCompressedTarFormat(string path)
    {
        var value = (path ?? "").Replace('\\', '/').ToLowerInvariant();
        if (value.EndsWith(".tar.gz", StringComparison.Ordinal) || value.EndsWith(".tgz", StringComparison.Ordinal)) return "TAR.GZIP";
        if (value.EndsWith(".tar.bz2", StringComparison.Ordinal) || value.EndsWith(".tbz2", StringComparison.Ordinal) || value.EndsWith(".tbz", StringComparison.Ordinal)) return "TAR.BZIP2";
        if (value.EndsWith(".tar.lz", StringComparison.Ordinal)) return "TAR.LZIP";
        return null;
    }

    private static bool TryNormalizeCompressedTar(string format, out string compression)
    {
        var value = (format ?? "").Trim().TrimStart('.').Replace("_", ".").Replace("-", ".").ToUpperInvariant();
        if (value is "TAR.GZIP" or "TAR.GZ" or "TARGZIP" or "TGZ") { compression = "GZip"; return true; }
        if (value is "TAR.BZIP2" or "TAR.BZ2" or "TARBZIP2" or "TBZ2" or "TBZ") { compression = "BZip2"; return true; }
        if (value is "TAR.LZIP" or "TAR.LZ" or "TARLZIP") { compression = "LZip"; return true; }
        compression = "";
        return false;
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

    internal sealed class CompressedTarEntry
    {
        public string Name { get; set; }
        public byte[]? Bytes { get; }
        public DateTime Modified { get; }
        public bool IsDirectory { get; }
        public CompressedTarEntry(string name, byte[]? bytes, DateTime modified, bool isDirectory)
        {
            Name = name; Bytes = bytes; Modified = modified; IsDirectory = isDirectory;
        }
    }
}

internal static class XPScriptCompressedTarWriter
{
    public static void WriteAtomic(string path, string compressionName, IEnumerable<XPScriptExtendedArchiveV3.CompressedTarEntry> entries, int compressionLevel)
    {
        var parent = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Write(temp, compressionName, entries, compressionLevel);
            ReplaceAtomic(temp, path);
        }
        catch
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
            throw;
        }
    }

    private static void Write(string path, string compressionName, IEnumerable<XPScriptExtendedArchiveV3.CompressedTarEntry> entries, int compressionLevel)
    {
        try
        {
            var assembly = System.Reflection.Assembly.Load("SharpCompress");
            var archiveTypeType = assembly.GetType("SharpCompress.Common.ArchiveType", throwOnError: true)!;
            var compressionTypeType = assembly.GetType("SharpCompress.Common.CompressionType", throwOnError: true)!;
            var optionsType = assembly.GetType("SharpCompress.Writers.Tar.TarWriterOptions", throwOnError: true)!;
            var archiveType = Enum.Parse(archiveTypeType, "Tar", true);
            var compressionType = Enum.Parse(compressionTypeType, compressionName, true);
            var options = Activator.CreateInstance(optionsType, [compressionType, true])!;
            optionsType.GetProperty("CompressionLevel")?.SetValue(options, compressionName.Equals("GZip", StringComparison.OrdinalIgnoreCase) ? Math.Clamp(compressionLevel, 0, 9) : compressionLevel);
            var factoryType = assembly.GetType("SharpCompress.Writers.WriterFactory", throwOnError: true)!;
            var openWriter = factoryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "OpenWriter" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(System.IO.Stream))
                ?? throw new MissingMethodException("SharpCompress WriterFactory.OpenWriter(Stream, ArchiveType, IWriterOptions) was not found.");
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None);
            var writer = openWriter.Invoke(null, [stream, archiveType, options]) ?? throw new XPScriptRuntimeException(5, "SharpCompress failed to create the compressed TAR writer.");
            try
            {
                var writerType = writer.GetType();
                var writeFile = writerType.GetMethod("Write", [typeof(string), typeof(System.IO.Stream), typeof(DateTime?)])
                    ?? writerType.GetInterfaces().SelectMany(x => x.GetMethods()).FirstOrDefault(m => m.Name == "Write" && m.GetParameters().Length == 3);
                var writeDirectory = writerType.GetMethod("WriteDirectory", [typeof(string), typeof(DateTime?)])
                    ?? writerType.GetInterfaces().SelectMany(x => x.GetMethods()).FirstOrDefault(m => m.Name == "WriteDirectory" && m.GetParameters().Length == 2);
                if (writeFile is null || writeDirectory is null) throw new MissingMethodException("SharpCompress TAR writer entry methods were not found.");
                foreach (var entry in entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                {
                    if (entry.IsDirectory)
                    {
                        writeDirectory.Invoke(writer, [entry.Name.TrimEnd('/'), entry.Modified]);
                        continue;
                    }
                    using var input = new System.IO.MemoryStream(entry.Bytes ?? [], writable: false);
                    writeFile.Invoke(writer, [entry.Name, input, entry.Modified]);
                }
            }
            finally
            {
                if (writer is IDisposable disposable) disposable.Dispose();
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new XPScriptRuntimeException(5, "Unable to write compressed TAR archive: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "Unable to write compressed TAR archive: " + ex.Message);
        }
    }

    private static void ReplaceAtomic(string temp, string destination)
    {
        if (!System.IO.File.Exists(destination)) { System.IO.File.Move(temp, destination); return; }
        try { System.IO.File.Replace(temp, destination, null); }
        catch (Exception ex) when (ex is PlatformNotSupportedException || ex is System.IO.IOException) { System.IO.File.Move(temp, destination, true); }
    }
}
""";
}
