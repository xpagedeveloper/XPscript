namespace XPScript.Compiler;

internal static class ArchiveExtendedWriterRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptExtendedArchiveV2
{
    private readonly XPScriptExtendedArchive _inner;
    private bool _gzipCreateMode;
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
    public bool IsReadOnly => _gzipCreateMode ? false : _inner.IsReadOnly;
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
        var requested = format is null ? _inner.Format : XPScriptRuntime.CStr(format).Trim().TrimStart('.').ToUpperInvariant();
        if (requested.Equals("GZ", StringComparison.OrdinalIgnoreCase) || requested.Equals("GZIP", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(Path)) throw new XPScriptRuntimeException(5, "GZip creation requires a file path.");
            if (!string.IsNullOrEmpty(Password)) throw new XPScriptRuntimeException(5, "Password-protected GZip writing is not supported.");
            _gzipCreateMode = true;
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
        if (!_gzipCreateMode)
        {
            _inner.AddFile(sourcePath, archivePath);
            return;
        }
        var source = XPScriptFileSystemRuntime.ResolvePath(sourcePath);
        if (!System.IO.File.Exists(source)) throw new XPScriptRuntimeException(53, "Archive source file was not found.");
        if ((System.IO.File.GetAttributes(source) & System.IO.FileAttributes.ReparsePoint) != 0)
            throw new XPScriptRuntimeException(5, "Archive.AddFile does not allow symbolic links or reparse points.");
        var name = Normalize(archivePath is null ? System.IO.Path.GetFileName(source) : XPScriptRuntime.CStr(archivePath));
        SetGZipEntry(name, System.IO.File.ReadAllBytes(source), System.IO.File.GetLastWriteTimeUtc(source));
    }

    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true)
    {
        if (_gzipCreateMode) throw new XPScriptRuntimeException(5, "GZip is a single-stream format and does not support folder entries.");
        _inner.AddFolder(sourcePath, archivePath, recursive);
    }

    public void AddText(object? archivePath, object? text)
    {
        if (!_gzipCreateMode)
        {
            _inner.AddText(archivePath, text);
            return;
        }
        SetGZipEntry(Normalize(XPScriptRuntime.CStr(archivePath)), System.Text.Encoding.UTF8.GetBytes(XPScriptRuntime.CStr(text)), DateTime.UtcNow);
    }

    public void AddBytes(object? archivePath, object? bytes)
    {
        if (!_gzipCreateMode)
        {
            _inner.AddBytes(archivePath, bytes);
            return;
        }
        SetGZipEntry(Normalize(XPScriptRuntime.CStr(archivePath)), ToRawBytes(bytes), DateTime.UtcNow);
    }

    public bool Remove(object? entryName)
    {
        if (!_gzipCreateMode) return _inner.Remove(entryName);
        if (_gzipEntryName is null) return false;
        var wanted = Normalize(XPScriptRuntime.CStr(entryName));
        if (!_gzipEntryName.Equals(wanted, StringComparison.OrdinalIgnoreCase)) return false;
        _gzipEntryName = null;
        _gzipEntryBytes = null;
        if (System.IO.File.Exists(Path)) System.IO.File.Delete(Path);
        return true;
    }

    public bool Rename(object? entryName, object? newName)
    {
        if (!_gzipCreateMode) return _inner.Rename(entryName, newName);
        if (_gzipEntryName is null || _gzipEntryBytes is null) return false;
        var wanted = Normalize(XPScriptRuntime.CStr(entryName));
        if (!_gzipEntryName.Equals(wanted, StringComparison.OrdinalIgnoreCase)) return false;
        _gzipEntryName = Normalize(XPScriptRuntime.CStr(newName));
        Save();
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

    private static string Normalize(string value)
    {
        var name = (value ?? "").Replace('\\', '/').Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Archive entry name must not be empty.");
        if (name.StartsWith("/", StringComparison.Ordinal) || name.StartsWith("//", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z]:"))
            throw new XPScriptRuntimeException(5, "Absolute archive paths are not allowed.");
        var parts = name.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(x => x == "..")) throw new XPScriptRuntimeException(5, "Archive path traversal is not allowed.");
        var result = string.Join('/', parts.Where(x => x != "."));
        if (result.Contains('/', StringComparison.Ordinal))
            throw new XPScriptRuntimeException(5, "GZip entry names cannot contain folders. Use TAR.GZip for directory structures.");
        return result;
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

    private static void ReplaceAtomic(string temp, string destination)
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
