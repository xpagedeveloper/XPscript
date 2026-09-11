namespace XPScript.Compiler;

internal static class ArchiveExtendedMemoryWriterRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptExtendedMemoryArchiveV2
{
    private byte[] _data;
    private XPScriptExtendedMemoryArchive? _reader;
    private readonly List<PendingEntry> _pending = [];
    private string _format = "";
    private bool _createMode;

    public XPScriptExtendedMemoryArchiveV2(object? bytes)
    {
        _data = ToRawBytes(bytes);
        if (_data.Length > 0) _reader = new XPScriptExtendedMemoryArchive(_data);
    }

    public string Path => "";
    public string Format => _createMode ? _format : (_reader?.Format ?? "");
    public bool Exists => _data.Length > 0 || _createMode;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => !_createMode;
    public string Password
    {
        get => _reader?.Password ?? "";
        set
        {
            if (_createMode && !string.IsNullOrEmpty(value))
                throw new XPScriptRuntimeException(5, "Password-protected archive writing is not supported.");
            if (_reader is not null) _reader.Password = value ?? "";
        }
    }
    public int CompressionLevel { get; set; } = 5;
    public long MaxExtractSize { get; set; } = 2L * 1024 * 1024 * 1024;
    public int MaxEntries { get; set; } = 10000;
    public double MaxCompressionRatio { get; set; } = 1000d;

    public bool IsEncrypted => _reader?.IsEncrypted ?? false;
    public long FileCount => _createMode ? _pending.LongCount(x => !x.IsDirectory) : (_reader?.FileCount ?? 0);
    public long FolderCount => _createMode ? _pending.LongCount(x => x.IsDirectory) : (_reader?.FolderCount ?? 0);
    public long CompressedSize => _reader?.CompressedSize ?? 0;
    public long UncompressedSize => _createMode ? _pending.Sum(x => (long)(x.Bytes?.Length ?? 0)) : (_reader?.UncompressedSize ?? 0);
    public LSArray Entries => _reader?.Entries ?? new LSArray("Variant", true);

    public void Open()
    {
        if (_reader is not null) _reader.Open();
    }

    public void Close() => _reader?.Close();

    public void Create(object? format = null)
    {
        var requested = NormalizeFormat(format is null ? "TAR" : XPScriptRuntime.CStr(format));
        _format = requested;
        _createMode = true;
        _pending.Clear();
        _data = [];
        _reader = null;
    }

    public void Save()
    {
        EnsureWritable();
        if (_pending.Count > MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
        var total = _pending.Sum(x => (long)(x.Bytes?.Length ?? 0));
        if (total > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
        _data = XPScriptArchiveExtendedMemoryWriter.Write(_format, _pending, CompressionLevel);
        _reader = new XPScriptExtendedMemoryArchive(_data);
        _reader.MaxEntries = MaxEntries;
        _reader.MaxExtractSize = MaxExtractSize;
        _reader.MaxCompressionRatio = MaxCompressionRatio;
    }

    public void AddFile(object? sourcePath, object? archivePath = null)
    {
        EnsureWritable();
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.File.Exists(source)) throw new XPScriptRuntimeException(53, "Archive source file was not found.");
        var attributes = System.IO.File.GetAttributes(source);
        if ((attributes & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Archive.AddFile does not allow symbolic links or reparse points.");
        var name = Normalize(archivePath is null ? System.IO.Path.GetFileName(source) : XPScriptRuntime.CStr(archivePath));
        SetEntry(name, System.IO.File.ReadAllBytes(source), System.IO.File.GetLastWriteTimeUtc(source), false);
        Save();
    }

    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true)
    {
        EnsureWritable();
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.Directory.Exists(source)) throw new XPScriptRuntimeException(76, "Archive source folder was not found.");
        if ((System.IO.File.GetAttributes(source) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Archive.AddFolder does not allow symbolic links or reparse points.");
        var root = archivePath is null ? System.IO.Path.GetFileName(source.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)) : XPScriptRuntime.CStr(archivePath);
        root = Normalize(root).TrimEnd('/');
        AddFolderTree(source, source, root, recursive);
        Save();
    }

    public void AddText(object? archivePath, object? text)
    {
        EnsureWritable();
        SetEntry(Normalize(XPScriptRuntime.CStr(archivePath)), System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(text)), DateTime.UtcNow, false);
        Save();
    }

    public void AddBytes(object? archivePath, object? bytes)
    {
        EnsureWritable();
        SetEntry(Normalize(XPScriptRuntime.CStr(archivePath)), ToRawBytes(bytes), DateTime.UtcNow, false);
        Save();
    }

    public bool Remove(object? entryName)
    {
        EnsureWritable();
        var wanted = Normalize(XPScriptRuntime.CStr(entryName)).TrimEnd('/');
        var removed = _pending.RemoveAll(x => x.Name.TrimEnd('/').Equals(wanted, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed) Save();
        return removed;
    }

    public bool Rename(object? entryName, object? newName)
    {
        EnsureWritable();
        var wanted = Normalize(XPScriptRuntime.CStr(entryName)).TrimEnd('/');
        var replacement = Normalize(XPScriptRuntime.CStr(newName));
        var item = _pending.FirstOrDefault(x => x.Name.TrimEnd('/').Equals(wanted, StringComparison.OrdinalIgnoreCase));
        if (item is null) return false;
        item.Name = item.IsDirectory ? replacement.TrimEnd('/') + "/" : replacement.TrimEnd('/');
        Save();
        return true;
    }

    public bool Contains(object? entryName) => Reader().Contains(entryName);
    public XPScriptArchiveEntry? GetEntry(object? entryName) => Reader().GetEntry(entryName);
    public LSArray Files() => Reader().Files();
    public LSArray Folders() => Reader().Folders();
    public LSArray Find(object? pattern) => Reader().Find(pattern);
    public string ReadText(object? entryName) => Reader().ReadText(entryName);
    public LSArray ReadBytes(object? entryName) => Reader().ReadBytes(entryName);
    public void Extract(object? entryName, object? targetPath) => Reader().Extract(entryName, targetPath);
    public void ExtractFolder(object? folderName, object? targetDirectory) => Reader().ExtractFolder(folderName, targetDirectory);
    public void ExtractAll(object? targetDirectory) => Reader().ExtractAll(targetDirectory);
    public LSArray ToBytes() => PackBytes(_data);

    private XPScriptExtendedMemoryArchive Reader()
    {
        if (_reader is null)
        {
            if (_createMode) Save();
            if (_reader is null) throw new XPScriptRuntimeException(5, "Archive contains no data.");
        }
        _reader.MaxEntries = MaxEntries;
        _reader.MaxExtractSize = MaxExtractSize;
        _reader.MaxCompressionRatio = MaxCompressionRatio;
        return _reader;
    }

    private void EnsureWritable()
    {
        if (!_createMode)
            throw new XPScriptRuntimeException(5, "Existing extended in-memory archives are read-only. Call Create() to create a new writable TAR, 7z, TAR.GZip, TAR.BZip2 or TAR.LZip archive.");
    }

    private void SetEntry(string name, byte[] bytes, DateTime modified, bool isDirectory)
    {
        if (bytes.LongLength > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
        var normalized = isDirectory ? Normalize(name).TrimEnd('/') + "/" : Normalize(name).TrimEnd('/');
        _pending.RemoveAll(x => x.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (_pending.Count >= MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
        _pending.Add(new PendingEntry(normalized, isDirectory ? null : bytes, modified, isDirectory));
    }

    private void AddFolderTree(string sourceRoot, string currentDirectory, string archiveRoot, bool recursive)
    {
        var relative = System.IO.Path.GetRelativePath(sourceRoot, currentDirectory).Replace('\\', '/');
        var directoryEntry = relative == "." ? archiveRoot : archiveRoot + "/" + relative;
        SetEntry(directoryEntry, [], System.IO.Directory.GetLastWriteTimeUtc(currentDirectory), true);

        foreach (var file in System.IO.Directory.EnumerateFiles(currentDirectory, "*", System.IO.SearchOption.TopDirectoryOnly))
        {
            if ((System.IO.File.GetAttributes(file) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
            var fileRelative = System.IO.Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
            SetEntry(archiveRoot + "/" + fileRelative, System.IO.File.ReadAllBytes(file), System.IO.File.GetLastWriteTimeUtc(file), false);
        }
        if (!recursive) return;
        foreach (var directory in System.IO.Directory.EnumerateDirectories(currentDirectory, "*", System.IO.SearchOption.TopDirectoryOnly))
        {
            if ((System.IO.File.GetAttributes(directory) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
            AddFolderTree(sourceRoot, directory, archiveRoot, true);
        }
    }

    private static string NormalizeFormat(string value)
    {
        var format = value.Trim().TrimStart('.').ToUpperInvariant();
        return format switch
        {
            "TAR" => "TAR",
            "7Z" or "7ZIP" or "SEVENZIP" => "7Z",
            "TAR.GZ" or "TGZ" or "TARGZIP" => "TAR.GZ",
            "TAR.BZ2" or "TBZ2" or "TARBZIP2" => "TAR.BZ2",
            "TAR.LZ" or "TARLZIP" => "TAR.LZ",
            _ => throw new XPScriptRuntimeException(5, "Writable extended in-memory archives support TAR, 7z, TAR.GZip, TAR.BZip2 and TAR.LZip.")
        };
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

    internal sealed class PendingEntry
    {
        public string Name { get; set; }
        public byte[]? Bytes { get; }
        public DateTime Modified { get; }
        public bool IsDirectory { get; }
        public PendingEntry(string name, byte[]? bytes, DateTime modified, bool isDirectory)
        {
            Name = name;
            Bytes = bytes;
            Modified = modified;
            IsDirectory = isDirectory;
        }
    }
}

internal static class XPScriptArchiveExtendedMemoryWriter
{
    public static byte[] Write(string format, IReadOnlyList<XPScriptExtendedMemoryArchiveV2.PendingEntry> entries, int compressionLevel)
    {
        if (format == "TAR") return WriteRawTar(entries);

        try
        {
            var assembly = System.Reflection.Assembly.Load("SharpCompress");
            var archiveTypeType = assembly.GetType("SharpCompress.Common.ArchiveType", throwOnError: true)!;
            var compressionTypeType = assembly.GetType("SharpCompress.Common.CompressionType", throwOnError: true)!;
            var writerFactoryType = assembly.GetType("SharpCompress.Writers.WriterFactory", throwOnError: true)!;
            var optionsInterface = assembly.GetType("SharpCompress.Writers.IWriterOptions", throwOnError: true)!;
            object options;
            object archiveType;

            if (format == "7Z")
            {
                archiveType = Enum.Parse(archiveTypeType, "SevenZip", true);
                var optionsType = assembly.GetType("SharpCompress.Writers.SevenZip.SevenZipWriterOptions", throwOnError: true)!;
                var compressionType = Enum.Parse(compressionTypeType, "LZMA2", true);
                options = Activator.CreateInstance(optionsType, [compressionType])!;
                optionsType.GetProperty("CompressionLevel")?.SetValue(options, Math.Clamp(compressionLevel, 0, 9));
            }
            else
            {
                archiveType = Enum.Parse(archiveTypeType, "Tar", true);
                var optionsType = assembly.GetType("SharpCompress.Writers.Tar.TarWriterOptions", throwOnError: true)!;
                var compressionName = format switch
                {
                    "TAR.GZ" => "GZip",
                    "TAR.BZ2" => "BZip2",
                    "TAR.LZ" => "LZip",
                    _ => "None"
                };
                var compressionType = Enum.Parse(compressionTypeType, compressionName, true);
                options = Activator.CreateInstance(optionsType, [compressionType, true])!;
                if (compressionName == "GZip") optionsType.GetProperty("CompressionLevel")?.SetValue(options, Math.Clamp(compressionLevel, 0, 9));
            }

            var openWriter = writerFactoryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "OpenWriter" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(System.IO.Stream))
                ?? throw new MissingMethodException("Archive writer factory method was not found.");
            using var output = new System.IO.MemoryStream();
            var writer = openWriter.Invoke(null, [output, archiveType, options])
                ?? throw new XPScriptRuntimeException(5, "Unable to create archive writer.");
            try
            {
                if (format == "7Z" && entries.Count == 0)
                {
                    var ensurePlaceholder = writer.GetType().GetMethod(
                        "EnsurePlaceholderWritten",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?? throw new MissingMethodException("Empty 7z archive finalization is unavailable.");
                    ensurePlaceholder.Invoke(writer, null);
                }

                var write = writer.GetType().GetMethod("Write", [typeof(string), typeof(System.IO.Stream), typeof(DateTime?)])
                    ?? writer.GetType().GetInterfaces().SelectMany(x => x.GetMethods()).FirstOrDefault(m => m.Name == "Write" && m.GetParameters().Length == 3)
                    ?? throw new MissingMethodException("Archive writer method was not found.");
                var writeDirectory = writer.GetType().GetMethod("WriteDirectory", [typeof(string), typeof(DateTime?)])
                    ?? writer.GetType().GetInterfaces().SelectMany(x => x.GetMethods()).FirstOrDefault(m => m.Name == "WriteDirectory" && m.GetParameters().Length == 2);
                foreach (var entry in entries)
                {
                    if (entry.IsDirectory)
                    {
                        if (writeDirectory is not null) writeDirectory.Invoke(writer, [entry.Name.TrimEnd('/'), entry.Modified]);
                        continue;
                    }
                    using var input = new System.IO.MemoryStream(entry.Bytes ?? [], writable: false);
                    write.Invoke(writer, [entry.Name, input, entry.Modified]);
                }
            }
            finally
            {
                if (writer is IDisposable disposable) disposable.Dispose();
            }
            return output.ToArray();
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new XPScriptRuntimeException(5, "Unable to write extended in-memory archive: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "Unable to write extended in-memory archive: " + ex.Message);
        }
    }

    private static byte[] WriteRawTar(IReadOnlyList<XPScriptExtendedMemoryArchiveV2.PendingEntry> entries)
    {
        using var output = new System.IO.MemoryStream();
        using (var writer = new System.Formats.Tar.TarWriter(output, System.Formats.Tar.TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var pending in entries)
            {
                var entryType = pending.IsDirectory
                    ? System.Formats.Tar.TarEntryType.Directory
                    : System.Formats.Tar.TarEntryType.RegularFile;
                var name = pending.IsDirectory ? pending.Name.TrimEnd('/') + "/" : pending.Name;
                var entry = new System.Formats.Tar.PaxTarEntry(entryType, name)
                {
                    ModificationTime = new DateTimeOffset(pending.Modified.ToUniversalTime())
                };
                if (!pending.IsDirectory)
                    entry.DataStream = new System.IO.MemoryStream(pending.Bytes ?? [], writable: false);
                writer.WriteEntry(entry);
                entry.DataStream?.Dispose();
            }
        }
        return output.ToArray();
    }
}
""";
}
