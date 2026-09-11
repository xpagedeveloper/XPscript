namespace XPScript.Compiler;

internal static class ArchiveExtendedMemoryRebuildRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptExtendedMemoryArchiveV3
{
    private XPScriptExtendedMemoryArchiveV2 _inner;
    private readonly byte[] _sourceBytes;
    private bool _editing;

    public XPScriptExtendedMemoryArchiveV3(object? bytes)
    {
        _sourceBytes = ToRawBytes(bytes);
        _inner = new XPScriptExtendedMemoryArchiveV2(_sourceBytes);
    }

    public string Path => "";
    public string Format => _inner.Format;
    public bool Exists => _inner.Exists;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => !_editing && !CanSafelyRebuild;
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

    private bool CanRebuild => DetectWritableFormat() is not null;
    private bool CanSafelyRebuild
    {
        get
        {
            if (!CanRebuild || !string.IsNullOrEmpty(Password)) return false;
            try { return !IsEncrypted; }
            catch { return false; }
        }
    }

    public void Open() => _inner.Open();
    public void Close() => _inner.Close();

    public void Create(object? format = null)
    {
        _inner.Create(format);
        _editing = true;
    }

    public void Save() => _inner.Save();

    public void AddFile(object? sourcePath, object? archivePath = null)
    {
        EnsureEditing();
        _inner.AddFile(sourcePath, archivePath);
    }

    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true)
    {
        EnsureEditing();
        _inner.AddFolder(sourcePath, archivePath, recursive);
    }

    public void AddText(object? archivePath, object? text)
    {
        EnsureEditing();
        _inner.AddText(archivePath, text);
    }

    public void AddBytes(object? archivePath, object? bytes)
    {
        EnsureEditing();
        _inner.AddBytes(archivePath, bytes);
    }

    public bool Remove(object? entryName)
    {
        EnsureEditing();
        return _inner.Remove(entryName);
    }

    public bool Rename(object? entryName, object? newName)
    {
        EnsureEditing();
        return _inner.Rename(entryName, newName);
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

    private void EnsureEditing()
    {
        if (_editing) return;
        var format = DetectWritableFormat();
        if (format is null)
            throw new XPScriptRuntimeException(5, "This extended in-memory archive format is read-only.");
        if (!CanSafelyRebuild)
            throw new XPScriptRuntimeException(5, "Encrypted extended in-memory archives are read-only.");

        var replacement = new XPScriptExtendedMemoryArchiveV2(new LSArray("Byte", true));
        replacement.CompressionLevel = CompressionLevel;
        replacement.MaxExtractSize = MaxExtractSize;
        replacement.MaxEntries = MaxEntries;
        replacement.MaxCompressionRatio = MaxCompressionRatio;
        replacement.Create(format);

        var setEntry = typeof(XPScriptExtendedMemoryArchiveV2).GetMethod(
            "SetEntry",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new XPScriptRuntimeException(5, "Archive rebuild support is unavailable.");

        var entries = _inner.Entries;
        if (entries.IsAllocated)
        {
            for (var i = entries.LBound(); i <= entries.UBound(); i++)
            {
                if (entries.Get(i) is not XPScriptArchiveEntry entry) continue;
                byte[] bytes = entry.IsDirectory ? [] : ToRawBytes(_inner.ReadBytes(entry.FullName));
                setEntry.Invoke(replacement, [entry.FullName, bytes, entry.Modified, entry.IsDirectory]);
            }
        }
        replacement.Save();
        _inner = replacement;
        _editing = true;
    }

    private string? DetectWritableFormat()
    {
        if (_sourceBytes.Length == 0) return null;
        if (_sourceBytes.Length >= 6 &&
            _sourceBytes[0] == 0x37 && _sourceBytes[1] == 0x7A && _sourceBytes[2] == 0xBC &&
            _sourceBytes[3] == 0xAF && _sourceBytes[4] == 0x27 && _sourceBytes[5] == 0x1C)
            return "7Z";

        var readerFormat = _inner.Format;
        if (!readerFormat.Equals("TAR", StringComparison.OrdinalIgnoreCase)) return null;
        if (_sourceBytes.Length >= 2 && _sourceBytes[0] == 0x1F && _sourceBytes[1] == 0x8B) return "TAR.GZ";
        if (_sourceBytes.Length >= 3 && _sourceBytes[0] == (byte)'B' && _sourceBytes[1] == (byte)'Z' && _sourceBytes[2] == (byte)'h') return "TAR.BZ2";
        if (_sourceBytes.Length >= 4 && _sourceBytes[0] == (byte)'L' && _sourceBytes[1] == (byte)'Z' && _sourceBytes[2] == (byte)'I' && _sourceBytes[3] == (byte)'P') return "TAR.LZ";
        return "TAR";
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
}
""";
}
