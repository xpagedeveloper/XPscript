namespace XPScript.Compiler;

internal static class ArchiveDetectedFormatRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptExtendedArchiveV4
{
    private readonly XPScriptExtendedArchiveV3 _inner;
    private bool _rebuildAutoSaveMode;

    public XPScriptExtendedArchiveV4(object? path = null)
    {
        _inner = new XPScriptExtendedArchiveV3(path);
    }

    public string Path => _inner.Path;
    public string Format => DetectFormat(Path) ?? _inner.Format;
    public bool Exists => _inner.Exists;
    public bool ExtendedSupport => true;
    public bool IsReadOnly => _inner.IsReadOnly;
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
    public void Save()
    {
        if (_rebuildAutoSaveMode) return;
        _inner.Save();
    }
    public void Create(object? format = null)
    {
        _inner.Create(format);
        var createdFormat = _inner.Format;
        _rebuildAutoSaveMode = createdFormat.Equals("TAR", StringComparison.OrdinalIgnoreCase)
            || createdFormat.Equals("7Z", StringComparison.OrdinalIgnoreCase)
            || createdFormat.Equals("7ZIP", StringComparison.OrdinalIgnoreCase)
            || createdFormat.Equals("SEVENZIP", StringComparison.OrdinalIgnoreCase);
    }
    public void AddFile(object? sourcePath, object? archivePath = null) => _inner.AddFile(sourcePath, archivePath);
    public void AddFolder(object? sourcePath, object? archivePath = null, bool recursive = true) => _inner.AddFolder(sourcePath, archivePath, recursive);
    public void AddText(object? archivePath, object? text) => _inner.AddText(archivePath, text);
    public void AddBytes(object? archivePath, object? bytes) => _inner.AddBytes(archivePath, bytes);
    public bool Remove(object? entryName) => _inner.Remove(entryName);
    public bool Rename(object? entryName, object? newName) => _inner.Rename(entryName, newName);
    public bool Contains(object? entryName) => _inner.Contains(entryName);
    public XPScriptArchiveEntry? GetEntry(object? entryName) => _inner.GetEntry(entryName);
    public LSArray Files() => _inner.Files();
    public LSArray Folders() => _inner.Folders();
    public LSArray Find(object? pattern) => _inner.Find(pattern);

    public string ReadText(object? entryName)
    {
        if (!IsTarFormat(Format)) return _inner.ReadText(entryName);
        return System.Text.Encoding.UTF8.GetString(ReadTarBytes(entryName));
    }

    public LSArray ReadBytes(object? entryName)
    {
        if (!IsTarFormat(Format)) return _inner.ReadBytes(entryName);
        return PackBytes(ReadTarBytes(entryName));
    }

    public void Extract(object? entryName, object? targetPath) => _inner.Extract(entryName, targetPath);
    public void ExtractFolder(object? folderName, object? targetDirectory) => _inner.ExtractFolder(folderName, targetDirectory);
    public void ExtractAll(object? targetDirectory) => _inner.ExtractAll(targetDirectory);
    public LSArray ToBytes() => _inner.ToBytes();

    private byte[] ReadTarBytes(object? entryName)
    {
        var wanted = NormalizeEntryName(XPScriptRuntime.CStr(entryName));
        var snapshots = XPScriptArchiveExtendedReader.Snapshots(Path, Format, Password, MaxEntries, MaxExtractSize, MaxCompressionRatio);
        var entry = snapshots.FirstOrDefault(x => x.FullName.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            ?? throw new XPScriptRuntimeException(53, "Archive entry was not found.");
        if (entry.IsDirectory) throw new XPScriptRuntimeException(5, "Archive entry is a directory.");
        if (entry.Size < 0 || entry.Size > MaxExtractSize) throw new XPScriptRuntimeException(5, "Archive entry exceeds MaxExtractSize.");

        var bytes = XPScriptArchiveExtendedReader.ReadEntry(Path, Format, Password, wanted, MaxExtractSize, MaxCompressionRatio);
        if (bytes.LongLength < entry.Size)
            throw new XPScriptRuntimeException(5, "Archive entry ended before its declared size.");
        if (bytes.LongLength > entry.Size)
            Array.Resize(ref bytes, checked((int)entry.Size));
        return bytes;
    }

    private static bool IsTarFormat(string format) =>
        format.Equals("TAR", StringComparison.OrdinalIgnoreCase) ||
        format.StartsWith("TAR.", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeEntryName(string value)
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

    private static LSArray PackBytes(byte[] bytes)
    {
        if (bytes.Length == 0) return new LSArray("Byte", true);
        var result = new LSArray("Byte", true, [0], [bytes.Length - 1]);
        for (var i = 0; i < bytes.Length; i++) result.Set(bytes[i], i);
        return result;
    }

    private static string? DetectFormat(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
        try
        {
            using var stream = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            var header = new byte[Math.Min(560, checked((int)Math.Min(stream.Length, 560L)))];
            var read = stream.Read(header, 0, header.Length);
            if (read >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
                ((header[2] == 0x03 && header[3] == 0x04) || (header[2] == 0x05 && header[3] == 0x06) || (header[2] == 0x07 && header[3] == 0x08)))
                return "ZIP";
            if (read >= 6 && header[0] == 0x37 && header[1] == 0x7A && header[2] == 0xBC && header[3] == 0xAF && header[4] == 0x27 && header[5] == 0x1C)
                return "7Z";
            if (read >= 7 && header[0] == 0x52 && header[1] == 0x61 && header[2] == 0x72 && header[3] == 0x21 && header[4] == 0x1A && header[5] == 0x07 && (header[6] == 0x00 || header[6] == 0x01))
                return "RAR";
            if (LooksLikeTar(header, read)) return "TAR";
            if (read >= 2 && header[0] == 0x1F && header[1] == 0x8B)
                return IsGZipTar(path) ? "TAR.GZIP" : "GZIP";
            if (read >= 3 && header[0] == (byte)'B' && header[1] == (byte)'Z' && header[2] == (byte)'h')
                return "BZIP2";
            if (read >= 4 && header[0] == (byte)'L' && header[1] == (byte)'Z' && header[2] == (byte)'I' && header[3] == (byte)'P')
                return "LZIP";
            if (read >= 6 && header[0] == 0xFD && header[1] == 0x37 && header[2] == 0x7A && header[3] == 0x58 && header[4] == 0x5A && header[5] == 0x00)
                return "XZ";
            if (read >= 4 && header[0] == 0x28 && header[1] == 0xB5 && header[2] == 0x2F && header[3] == 0xFD)
                return "ZSTANDARD";
        }
        catch (System.IO.IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return null;
    }

    private static bool IsGZipTar(string path)
    {
        try
        {
            using var file = new System.IO.FileStream(path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
            using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress, leaveOpen: false);
            var header = new byte[560];
            var total = 0;
            while (total < header.Length)
            {
                var read = gzip.Read(header, total, header.Length - total);
                if (read <= 0) break;
                total += read;
            }
            return LooksLikeTar(header, total);
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeTar(byte[] header, int count)
    {
        if (count < 512) return false;
        if (count >= 262 &&
            header[257] == (byte)'u' && header[258] == (byte)'s' && header[259] == (byte)'t' && header[260] == (byte)'a' && header[261] == (byte)'r')
            return true;

        // Old TAR variants do not always carry the ustar marker. A valid octal checksum field
        // combined with a plausible name is sufficient for detection, but failure falls back
        // to the existing extension/reader-based behavior rather than guessing.
        var hasName = false;
        for (var i = 0; i < 100; i++)
        {
            if (header[i] == 0) break;
            if (header[i] < 0x20 || header[i] > 0x7E) return false;
            hasName = true;
        }
        if (!hasName) return false;

        long declared = 0;
        var hasChecksumDigit = false;
        for (var i = 148; i < 156; i++)
        {
            var c = header[i];
            if (c == 0 || c == (byte)' ') continue;
            if (c < (byte)'0' || c > (byte)'7') return false;
            declared = checked(declared * 8 + c - (byte)'0');
            hasChecksumDigit = true;
        }
        if (!hasChecksumDigit) return false;

        long actual = 0;
        for (var i = 0; i < 512; i++) actual += i is >= 148 and < 156 ? 32 : header[i];
        return declared == actual;
    }
}
""";
}
