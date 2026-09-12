namespace XPScript.Compiler;

internal static class ArchiveErrorNormalizationRuntimeSource
{
    public const string Code = """
internal static class XPScriptArchiveErrorNormalizer
{
    public static XPScriptRuntimeException Normalize(XPScriptRuntimeException ex)
    {
        var message = ex.Message ?? "";
        if (message.Contains("No password supplied for encrypted zip", StringComparison.OrdinalIgnoreCase))
            return new XPScriptRuntimeException(5, "Encrypted archive requires a password.");
        if (message.Contains("bad password", StringComparison.OrdinalIgnoreCase))
            return new XPScriptRuntimeException(5, "Invalid archive password.");
        if (message.Contains("SharpCompress", StringComparison.OrdinalIgnoreCase))
            return new XPScriptRuntimeException(5, "Unable to process the extended archive.");
        return ex;
    }

    private static bool IsBackendException(Exception ex)
    {
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            var typeName = current.GetType().FullName ?? "";
            if (typeName.StartsWith("SharpCompress.", StringComparison.Ordinal)) return true;
            if ((current.Message ?? "").Contains("SharpCompress", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static T Run<T>(Func<T> action)
    {
        try { return action(); }
        catch (XPScriptRuntimeException ex) { throw Normalize(ex); }
        catch (Exception ex) when (IsBackendException(ex))
        {
            throw new XPScriptRuntimeException(5, "Unable to process the extended archive.");
        }
    }

    public static void Run(Action action)
    {
        try { action(); }
        catch (XPScriptRuntimeException ex) { throw Normalize(ex); }
        catch (Exception ex) when (IsBackendException(ex))
        {
            throw new XPScriptRuntimeException(5, "Unable to process the extended archive.");
        }
    }
}

internal sealed class XPScriptExtendedArchiveV5
{
    private readonly XPScriptExtendedArchiveV4 _inner;
    public XPScriptExtendedArchiveV5(object? path = null) => _inner = new XPScriptExtendedArchiveV4(path);

    public string Path => _inner.Path;
    public string Format => XPScriptArchiveErrorNormalizer.Run(() => _inner.Format);
    public bool Exists => _inner.Exists;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => XPScriptArchiveErrorNormalizer.Run(() => _inner.IsReadOnly);
    public string Password { get => _inner.Password; set => _inner.Password = value ?? ""; }
    public int CompressionLevel { get => _inner.CompressionLevel; set => _inner.CompressionLevel = value; }
    public long MaxExtractSize { get => _inner.MaxExtractSize; set => _inner.MaxExtractSize = value; }
    public int MaxEntries { get => _inner.MaxEntries; set => _inner.MaxEntries = value; }
    public double MaxCompressionRatio { get => _inner.MaxCompressionRatio; set => _inner.MaxCompressionRatio = value; }
    public bool IsEncrypted => XPScriptArchiveErrorNormalizer.Run(() => _inner.IsEncrypted);
    public long FileCount => XPScriptArchiveErrorNormalizer.Run(() => _inner.FileCount);
    public long FolderCount => XPScriptArchiveErrorNormalizer.Run(() => _inner.FolderCount);
    public long CompressedSize => XPScriptArchiveErrorNormalizer.Run(() => _inner.CompressedSize);
    public long UncompressedSize => XPScriptArchiveErrorNormalizer.Run(() => _inner.UncompressedSize);
    public LSArray Entries => XPScriptArchiveErrorNormalizer.Run(() => _inner.Entries);

    public void Open() => XPScriptArchiveErrorNormalizer.Run(_inner.Open);
    public void Close() => XPScriptArchiveErrorNormalizer.Run(_inner.Close);
    public void Save() => XPScriptArchiveErrorNormalizer.Run(_inner.Save);
    public void Create(object? format = null) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Create(format));
    public void AddFile(object? sourcePath, object? archivePath = null) => XPScriptArchiveErrorNormalizer.Run(() => _inner.AddFile(sourcePath, archivePath));
    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true) => XPScriptArchiveErrorNormalizer.Run(() => _inner.AddFolder(sourcePath, archivePath, recursive));
    public void AddText(object? archivePath, object? text) => XPScriptArchiveErrorNormalizer.Run(() => _inner.AddText(archivePath, text));
    public void AddBytes(object? archivePath, object? bytes) => XPScriptArchiveErrorNormalizer.Run(() => _inner.AddBytes(archivePath, bytes));
    public bool Remove(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Remove(entryName));
    public bool Rename(object? entryName, object? newName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Rename(entryName, newName));
    public bool Contains(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Contains(entryName));
    public XPScriptArchiveEntry? GetEntry(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.GetEntry(entryName));
    public LSArray Files() => XPScriptArchiveErrorNormalizer.Run(_inner.Files);
    public LSArray Folders() => XPScriptArchiveErrorNormalizer.Run(_inner.Folders);
    public LSArray Find(object? pattern) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Find(pattern));
    public string ReadText(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.ReadText(entryName));
    public LSArray ReadBytes(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.ReadBytes(entryName));
    public void Extract(object? entryName, object? targetPath) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Extract(entryName, targetPath));
    public void ExtractFolder(object? folderName, object? targetDirectory) => XPScriptArchiveErrorNormalizer.Run(() => _inner.ExtractFolder(folderName, targetDirectory));
    public void ExtractAll(object? targetDirectory) => XPScriptArchiveErrorNormalizer.Run(() => _inner.ExtractAll(targetDirectory));
    public LSArray ToBytes() => XPScriptArchiveErrorNormalizer.Run(_inner.ToBytes);
}

internal sealed class XPScriptExtendedMemoryArchiveV4
{
    private readonly XPScriptExtendedMemoryArchiveV3 _inner;
    public XPScriptExtendedMemoryArchiveV4(object? bytes) => _inner = new XPScriptExtendedMemoryArchiveV3(bytes);

    public string Path => "";
    public string Format => XPScriptArchiveErrorNormalizer.Run(() => _inner.Format);
    public bool Exists => _inner.Exists;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => XPScriptArchiveErrorNormalizer.Run(() => _inner.IsReadOnly);
    public string Password { get => _inner.Password; set => _inner.Password = value ?? ""; }
    public int CompressionLevel { get => _inner.CompressionLevel; set => _inner.CompressionLevel = value; }
    public long MaxExtractSize { get => _inner.MaxExtractSize; set => _inner.MaxExtractSize = value; }
    public int MaxEntries { get => _inner.MaxEntries; set => _inner.MaxEntries = value; }
    public double MaxCompressionRatio { get => _inner.MaxCompressionRatio; set => _inner.MaxCompressionRatio = value; }
    public bool IsEncrypted => XPScriptArchiveErrorNormalizer.Run(() => _inner.IsEncrypted);
    public long FileCount => XPScriptArchiveErrorNormalizer.Run(() => _inner.FileCount);
    public long FolderCount => XPScriptArchiveErrorNormalizer.Run(() => _inner.FolderCount);
    public long CompressedSize => XPScriptArchiveErrorNormalizer.Run(() => _inner.CompressedSize);
    public long UncompressedSize => XPScriptArchiveErrorNormalizer.Run(() => _inner.UncompressedSize);
    public LSArray Entries => XPScriptArchiveErrorNormalizer.Run(() => _inner.Entries);

    public void Open() => XPScriptArchiveErrorNormalizer.Run(_inner.Open);
    public void Close() => XPScriptArchiveErrorNormalizer.Run(_inner.Close);
    public void Save() => XPScriptArchiveErrorNormalizer.Run(_inner.Save);
    public void Create(object? format = null) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Create(format));
    public void AddFile(object? sourcePath, object? archivePath = null) => XPScriptArchiveErrorNormalizer.Run(() => _inner.AddFile(sourcePath, archivePath));
    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true) => XPScriptArchiveErrorNormalizer.Run(() => _inner.AddFolder(sourcePath, archivePath, recursive));
    public void AddText(object? archivePath, object? text) => XPScriptArchiveErrorNormalizer.Run(() => _inner.AddText(archivePath, text));
    public void AddBytes(object? archivePath, object? bytes) => XPScriptArchiveErrorNormalizer.Run(() => _inner.AddBytes(archivePath, bytes));
    public bool Remove(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Remove(entryName));
    public bool Rename(object? entryName, object? newName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Rename(entryName, newName));
    public bool Contains(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Contains(entryName));
    public XPScriptArchiveEntry? GetEntry(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.GetEntry(entryName));
    public LSArray Files() => XPScriptArchiveErrorNormalizer.Run(_inner.Files);
    public LSArray Folders() => XPScriptArchiveErrorNormalizer.Run(_inner.Folders);
    public LSArray Find(object? pattern) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Find(pattern));
    public string ReadText(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.ReadText(entryName));
    public LSArray ReadBytes(object? entryName) => XPScriptArchiveErrorNormalizer.Run(() => _inner.ReadBytes(entryName));
    public void Extract(object? entryName, object? targetPath) => XPScriptArchiveErrorNormalizer.Run(() => _inner.Extract(entryName, targetPath));
    public void ExtractFolder(object? folderName, object? targetDirectory) => XPScriptArchiveErrorNormalizer.Run(() => _inner.ExtractFolder(folderName, targetDirectory));
    public void ExtractAll(object? targetDirectory) => XPScriptArchiveErrorNormalizer.Run(() => _inner.ExtractAll(targetDirectory));
    public LSArray ToBytes() => XPScriptArchiveErrorNormalizer.Run(_inner.ToBytes);
}
""";
}
