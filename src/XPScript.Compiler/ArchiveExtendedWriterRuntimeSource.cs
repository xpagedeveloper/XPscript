namespace XPScript.Compiler;

internal static class ArchiveExtendedWriterRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptExtendedArchiveV2
{
    private readonly XPScriptExtendedArchive _inner;
    private bool _gzipCreateMode;
    private bool _createdExtendedSession;
    private string? _gzipEntryName;
    private byte[]? _gzipEntryBytes;
    private DateTime _gzipModified;

    public XPScriptExtendedArchiveV2(object? path = null)
    {
        _inner = new XPScriptExtendedArchive(path);
    }

    public string Path => _inner.Path;
    public string Format => _gzipCreateMode ? "GZIP" : _inner.Format;
    public bool Exists => _inner.Exists;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => _gzipCreateMode ? false : (CanRebuildExisting ? false : _inner.IsReadOnly);
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

    private bool CanRebuildExisting => Exists && !_createdExtendedSession && (Format.Equals("TAR", StringComparison.OrdinalIgnoreCase) || Format.Equals("7Z", StringComparison.OrdinalIgnoreCase));

    public void Open() => _inner.Open();
    public void Close() => _inner.Close();

    public void Create(object? format = null)
    {
        var requested = format is null ? _inner.Format : XPScriptRuntime.CStr(format).Trim().TrimStart('.').ToUpperInvariant();
        if (requested.Equals("GZ", StringComparison.OrdinalIgnoreCase) || requested.Equals("GZIP", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Path)) throw new XPScriptRuntimeException(5, "GZip creation requires a file path.");
            if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Password-protected GZip writing is not supported.");
            _gzipCreateMode = true;
            _createdExtendedSession = false;
            _gzipEntryName = null;
            _gzipEntryBytes = null;
            _gzipModified = DateTime.UtcNow;
            if (System.IO.File.Exists(Path)) System.IO.File.Delete(Path);
            return;
        }

        _gzipCreateMode = false;
        _gzipEntryName = null;
        _gzipEntryBytes = null;
        _inner.Create(format);
        _createdExtendedSession = requested.Equals("TAR", StringComparison.OrdinalIgnoreCase)
            || requested.Equals("7Z", StringComparison.OrdinalIgnoreCase)
            || requested.Equals("7ZIP", StringComparison.OrdinalIgnoreCase)
            || requested.Equals("SEVENZIP", StringComparison.OrdinalIgnoreCase);
    }

    public void Save()
    {
        if (!_gzipCreateMode)
        {
            _inner.Save();
            return;
        }
        if (_gzipEntryName is null || _gzipEntryBytes is null)
            throw new XPScriptRuntimeException(5, "GZip requires exactly one file entry before Save().");
        XPScriptArchiveGZipWriter.WriteAtomic(Path, _gzipEntryName, _gzipEntryBytes, _gzipModified, CompressionLevel);
    }

    public void AddFile(object? sourcePath, object? archivePath = null)
    {
        if (!_gzipCreateMode && !CanRebuildExisting)
        {
            _inner.AddFile(sourcePath, archivePath);
            return;
        }
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.File.Exists(source)) throw new XPScriptRuntimeException(53, "Archive source file was not found.");
        if ((System.IO.File.GetAttributes(source) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Archive.AddFile does not allow symbolic links or reparse points.");
        var name = Normalize(archivePath is null ? System.IO.Path.GetFileName(source) : XPScriptRuntime.CStr(archivePath), allowFolders: !_gzipCreateMode);
        var bytes = System.IO.File.ReadAllBytes(source);
        var modified = System.IO.File.GetLastWriteTimeUtc(source);
        if (_gzipCreateMode)
        {
            SetGZipEntry(name, bytes, modified);
            return;
        }
        RebuildExisting(entries => Upsert(entries, new RebuildEntry(name, bytes, modified, false)));
    }

    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true)
    {
        if (_gzipCreateMode) throw new XPScriptRuntimeException(5, "GZip is a single-stream format and does not support folder entries.");
        if (!CanRebuildExisting)
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
        rootName = Normalize(rootName, true).TrimEnd('/');
        var additions = new List<RebuildEntry> { new(rootName + "/", null, DateTime.UtcNow, true) };
        CollectFolderEntries(source, source, rootName, recursive, additions);
        RebuildExisting(entries =>
        {
            foreach (var item in additions) Upsert(entries, item);
        });
    }

    public void AddText(object? archivePath, object? text)
    {
        var name = Normalize(XPScriptRuntime.CStr(archivePath), allowFolders: !_gzipCreateMode);
        var bytes = System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(text));
        if (_gzipCreateMode)
        {
            SetGZipEntry(name, bytes, DateTime.UtcNow);
            return;
        }
        if (CanRebuildExisting)
        {
            RebuildExisting(entries => Upsert(entries, new RebuildEntry(name, bytes, DateTime.UtcNow, false)));
            return;
        }
        _inner.AddText(archivePath, text);
    }

    public void AddBytes(object? archivePath, object? bytes)
    {
        var name = Normalize(XPScriptRuntime.CStr(archivePath), allowFolders: !_gzipCreateMode);
        var raw = ToRawBytes(bytes);
        if (_gzipCreateMode)
        {
            SetGZipEntry(name, raw, DateTime.UtcNow);
            return;
        }
        if (CanRebuildExisting)
        {
            RebuildExisting(entries => Upsert(entries, new RebuildEntry(name, raw, DateTime.UtcNow, false)));
            return;
        }
        _inner.AddBytes(archivePath, bytes);
    }

    public bool Remove(object? entryName)
    {
        if (_gzipCreateMode)
        {
            if (_gzipEntryName is null) return false;
            var wanted = Normalize(XPScriptRuntime.CStr(entryName), false);
            if (!_gzipEntryName.Equals(wanted, StringComparison.OrdinalIgnoreCase)) return false;
            _gzipEntryName = null;
            _gzipEntryBytes = null;
            if (System.IO.File.Exists(Path)) System.IO.File.Delete(Path);
            return true;
        }
        if (!CanRebuildExisting) return _inner.Remove(entryName);

        var name = Normalize(XPScriptRuntime.CStr(entryName), true).TrimEnd('/');
        var removed = false;
        RebuildExisting(entries =>
        {
            var prefix = name + "/";
            removed = entries.RemoveAll(x => x.Name.TrimEnd('/').Equals(name, StringComparison.OrdinalIgnoreCase) || x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) > 0;
        }, skipWriteWhenUnchanged: () => !removed);
        return removed;
    }

    public bool Rename(object? entryName, object? newName)
    {
        if (_gzipCreateMode)
        {
            if (_gzipEntryName is null || _gzipEntryBytes is null) return false;
            var wanted = Normalize(XPScriptRuntime.CStr(entryName), false);
            if (!_gzipEntryName.Equals(wanted, StringComparison.OrdinalIgnoreCase)) return false;
            _gzipEntryName = Normalize(XPScriptRuntime.CStr(newName), false);
            Save();
            return true;
        }
        if (!CanRebuildExisting) return _inner.Rename(entryName, newName);

        var oldKey = Normalize(XPScriptRuntime.CStr(entryName), true).TrimEnd('/');
        var newKey = Normalize(XPScriptRuntime.CStr(newName), true).TrimEnd('/');
        var renamed = false;
        RebuildExisting(entries =>
        {
            var prefix = oldKey + "/";
            foreach (var item in entries.Where(x => x.Name.TrimEnd('/').Equals(oldKey, StringComparison.OrdinalIgnoreCase) || x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                var sourceName = item.Name.TrimEnd('/');
                var suffix = sourceName.Length == oldKey.Length ? "" : sourceName[oldKey.Length..];
                item.Name = Normalize(newKey + suffix + (item.IsDirectory ? "/" : ""), true);
                renamed = true;
            }
        }, skipWriteWhenUnchanged: () => !renamed);
        return renamed;
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

    private void SetGZipEntry(string name, byte[] bytes, DateTime modified)
    {
        if (bytes.LongLength > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");
        if (_gzipEntryName is not null && !_gzipEntryName.Equals(name, StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, "GZip supports exactly one file entry. Create a TAR.GZip archive when multiple files are required.");
        _gzipEntryName = name;
        _gzipEntryBytes = bytes;
        _gzipModified = modified;
        Save();
    }

    private void RebuildExisting(Action<List<RebuildEntry>> mutate, Func<bool>? skipWriteWhenUnchanged = null)
    {
        if (!CanRebuildExisting) throw new XPScriptRuntimeException(5, "This archive format is read-only.");
        if (!string.IsNullOrEmpty(Password) || IsEncrypted)
            throw new XPScriptRuntimeException(5, "Encrypted TAR/7z archives cannot be modified because encryption preservation is not implemented.");

        var entries = LoadRebuildEntries();
        mutate(entries);
        if (skipWriteWhenUnchanged?.Invoke() == true) return;
        if (entries.Count > MaxEntries) throw new XPScriptRuntimeException(5, "Archive exceeds MaxEntries.");
        long total = 0;
        foreach (var entry in entries.Where(x => !x.IsDirectory))
        {
            total = checked(total + (entry.Bytes?.LongLength ?? 0));
            if (total > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive exceeds MaxExtractSize.");
        }

        var temp = Path + "." + Guid.NewGuid().ToString("N") + ".rebuild.tmp";
        try
        {
            var rebuilt = new XPScriptArchive(temp, true)
            {
                CompressionLevel = CompressionLevel,
                MaxExtractSize = MaxExtractSize,
                MaxEntries = MaxEntries,
                MaxCompressionRatio = MaxCompressionRatio
            };
            rebuilt.Create(Format);
            PopulateRebuildArchive(rebuilt, entries);
            rebuilt.Save();
            XPScriptArchiveGZipWriter.ReplaceAtomic(temp, Path);
        }
        catch
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
            throw;
        }
    }

    private List<RebuildEntry> LoadRebuildEntries()
    {
        var result = new List<RebuildEntry>();
        var values = _inner.Entries;
        if (!values.IsAllocated) return result;
        var isTar = Format.Equals("TAR", StringComparison.OrdinalIgnoreCase);
        for (var i = values.LBound(); i <= values.UBound(); i++)
        {
            if (values.Get(i) is not XPScriptArchiveEntry entry) continue;
            if (entry.IsDirectory)
            {
                result.Add(new RebuildEntry(Normalize(entry.FullName.TrimEnd('/') + "/", true), null, entry.Modified, true));
                continue;
            }
            var bytes = ToRawBytes(_inner.ReadBytes(entry.FullName));
            if (isTar)
            {
                if (bytes.LongLength < entry.Size)
                    throw new XPScriptRuntimeException(5, "Archive entry ended before its declared size.");
                if (bytes.LongLength > entry.Size)
                    Array.Resize(ref bytes, checked((int)entry.Size));
            }
            result.Add(new RebuildEntry(Normalize(entry.FullName, true), bytes, entry.Modified, false));
        }
        return result;
    }

    private static void PopulateRebuildArchive(XPScriptArchive archive, List<RebuildEntry> entries)
    {
        var type = typeof(XPScriptArchive);
        var suppress = type.GetField("_suppressExtendedAutoSave", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingFieldException("Archive rebuild autosave field was not found.");
        var addFile = type.GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException("Archive rebuild file writer was not found.");
        var addDirectory = type.GetMethod("AddDirectoryEntry", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException("Archive rebuild directory writer was not found.");
        suppress.SetValue(archive, true);
        try
        {
            foreach (var entry in entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (entry.IsDirectory) addDirectory.Invoke(archive, [entry.Name]);
                else addFile.Invoke(archive, [entry.Name, entry.Bytes ?? [], entry.Modified]);
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw ex.InnerException ?? ex;
        }
        finally
        {
            suppress.SetValue(archive, false);
        }
    }

    private static void Upsert(List<RebuildEntry> entries, RebuildEntry replacement)
    {
        entries.RemoveAll(x => x.Name.Equals(replacement.Name, StringComparison.OrdinalIgnoreCase));
        entries.Add(replacement);
    }

    private static void CollectFolderEntries(string sourceRoot, string currentDirectory, string archiveRoot, bool recursive, List<RebuildEntry> result)
    {
        foreach (var file in System.IO.Directory.EnumerateFiles(currentDirectory, "*", System.IO.SearchOption.TopDirectoryOnly))
        {
            if ((System.IO.File.GetAttributes(file) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
            var relative = System.IO.Path.GetRelativePath(sourceRoot, file).Replace('\\', '/');
            result.Add(new RebuildEntry(Normalize(archiveRoot + "/" + relative, true), System.IO.File.ReadAllBytes(file), System.IO.File.GetLastWriteTimeUtc(file), false));
        }
        if (!recursive) return;
        foreach (var directory in System.IO.Directory.EnumerateDirectories(currentDirectory, "*", System.IO.SearchOption.TopDirectoryOnly))
        {
            if ((System.IO.File.GetAttributes(directory) & System.IO.FileAttributes.ReparsePoint) != 0) continue;
            var relative = System.IO.Path.GetRelativePath(sourceRoot, directory).Replace('\\', '/');
            var name = Normalize(archiveRoot + "/" + relative + "/", true);
            result.Add(new RebuildEntry(name, null, System.IO.Directory.GetLastWriteTimeUtc(directory), true));
            CollectFolderEntries(sourceRoot, directory, archiveRoot, true, result);
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

    private static string Normalize(string value, bool allowFolders)
    {
        var name = (value ?? "").Replace('\\', '/').Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Archive entry name must not be empty.");
        if (name.StartsWith("/", StringComparison.Ordinal) || name.StartsWith("//", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z]:"))
            throw new XPScriptRuntimeException(5, "Absolute archive paths are not allowed.");
        var hadTrailingSlash = name.EndsWith("/", StringComparison.Ordinal);
        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(x => x == "..")) throw new XPScriptRuntimeException(5, "Archive path traversal is not allowed.");
        var result = string.Join('/', parts.Where(x => x != "."));
        if (!allowFolders && result.Contains('/', StringComparison.Ordinal))
            throw new XPScriptRuntimeException(5, "GZip entry names cannot contain folders. Use TAR.GZip for directory structures.");
        if (hadTrailingSlash && allowFolders) result += "/";
        return result;
    }

    private sealed class RebuildEntry
    {
        public string Name { get; set; }
        public byte[]? Bytes { get; }
        public DateTime Modified { get; }
        public bool IsDirectory { get; }

        public RebuildEntry(string name, byte[]? bytes, DateTime modified, bool isDirectory)
        {
            Name = name;
            Bytes = bytes;
            Modified = modified;
            IsDirectory = isDirectory;
        }
    }
}

internal static class XPScriptArchiveGZipWriter
{
    public static void WriteAtomic(string path, string entryName, byte[] bytes, DateTime modified, int compressionLevel)
    {
        var parent = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Write(temp, entryName, bytes, modified, compressionLevel);
            ReplaceAtomic(temp, path);
        }
        catch
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
            throw;
        }
    }

    private static void Write(string path, string entryName, byte[] bytes, DateTime modified, int compressionLevel)
    {
        try
        {
            var assembly = System.Reflection.Assembly.Load("SharpCompress");
            var archiveTypeType = assembly.GetType("SharpCompress.Common.ArchiveType", throwOnError: true)!;
            var archiveType = Enum.Parse(archiveTypeType, "GZip", true);
            var optionsType = assembly.GetType("SharpCompress.Writers.GZip.GZipWriterOptions", throwOnError: true)!;
            var options = Activator.CreateInstance(optionsType)!;
            optionsType.GetProperty("CompressionLevel")?.SetValue(options, Math.Clamp(compressionLevel, 0, 9));
            var writerFactoryType = assembly.GetType("SharpCompress.Writers.WriterFactory", throwOnError: true)!;
            var openWriter = writerFactoryType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "OpenWriter" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(System.IO.Stream))
                ?? throw new MissingMethodException("SharpCompress WriterFactory.OpenWriter(Stream, ArchiveType, IWriterOptions) was not found.");

            using var stream = new System.IO.FileStream(path, System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.None);
            var writer = openWriter.Invoke(null, [stream, archiveType, options])
                ?? throw new XPScriptRuntimeException(5, "SharpCompress failed to create the GZip writer.");
            try
            {
                var write = writer.GetType().GetMethod("Write", [typeof(string), typeof(System.IO.Stream), typeof(DateTime?)])
                    ?? writer.GetType().GetInterfaces().SelectMany(x => x.GetMethods()).FirstOrDefault(m => m.Name == "Write" && m.GetParameters().Length == 3)
                    ?? throw new MissingMethodException("SharpCompress GZip writer Write() was not found.");
                using var input = new System.IO.MemoryStream(bytes, writable: false);
                write.Invoke(writer, [entryName, input, modified]);
            }
            finally
            {
                if (writer is IDisposable disposable) disposable.Dispose();
            }
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            throw new XPScriptRuntimeException(5, "Unable to write GZip archive: " + (ex.InnerException?.Message ?? ex.Message));
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "Unable to write GZip archive: " + ex.Message);
        }
    }

    public static void ReplaceAtomic(string temp, string destination)
    {
        if (!System.IO.File.Exists(destination))
        {
            System.IO.File.Move(temp, destination);
            return;
        }

        try
        {
            System.IO.File.Replace(temp, destination, null);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException || ex is System.IO.IOException)
        {
            System.IO.File.Move(temp, destination, true);
        }
    }
}
""";
}
