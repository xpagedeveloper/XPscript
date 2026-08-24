from pathlib import Path

runtime_path = Path('src/XPScript.Compiler/CrossPlatformRuntimeSource.cs')
pre_path = Path('src/XPScript.Compiler/CrossPlatformPreprocessor.cs')
fileio_pre_path = Path('src/XPScript.Compiler/FileIoExtensionsPreprocessor.cs')
sample_path = Path('samples/file-convenience.xps')
docs_path = Path('docs/file-io-reference.md')

runtime = runtime_path.read_text()
if 'public sealed class XPFileInfoValue' not in runtime:
    marker = '    public static string StrTemplate(object? template, object? values)\n'
    if marker not in runtime:
        raise SystemExit('CrossPlatformRuntimeSource insertion marker not found')
    code = r'''    public sealed class XPFileInfoValue
    {
        public string Name { get; }
        public string FullPath { get; }
        public string Extension { get; }
        public long Length { get; }
        public DateTime Created { get; }
        public DateTime Modified { get; }
        public DateTime Accessed { get; }
        public bool IsFile { get; }
        public bool IsDirectory { get; }
        public bool IsLink { get; }
        public int Attributes { get; }

        public XPFileInfoValue(string path)
        {
            FullPath = Path.GetFullPath(path);
            FileSystemInfo info;
            if (File.Exists(FullPath)) info = new System.IO.FileInfo(FullPath);
            else if (Directory.Exists(FullPath)) info = new DirectoryInfo(FullPath);
            else throw new FileNotFoundException("Path not found.", FullPath);

            Name = info.Name;
            Extension = info.Extension;
            Created = info.CreationTime;
            Modified = info.LastWriteTime;
            Accessed = info.LastAccessTime;
            IsFile = info is System.IO.FileInfo;
            IsDirectory = info is DirectoryInfo;
            IsLink = info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0;
            Attributes = XPScriptFileSystemRuntime.GetFileAttr(FullPath);
            Length = info is System.IO.FileInfo file ? file.Length : 0L;
        }
    }

    public static XPFileInfoValue FileInfo(object? path) =>
        new(Path.GetFullPath(XPScriptRuntime.CStr(path)));

    public static string FileHash(object? path, object? algorithm = null)
    {
        var file = Path.GetFullPath(XPScriptRuntime.CStr(path));
        var name = algorithm is null ? "SHA256" : XPScriptRuntime.CStr(algorithm).Trim().ToUpperInvariant().Replace("-", "", StringComparison.Ordinal);
        using System.Security.Cryptography.HashAlgorithm hash = name switch
        {
            "SHA256" => System.Security.Cryptography.SHA256.Create(),
            "SHA384" => System.Security.Cryptography.SHA384.Create(),
            "SHA512" => System.Security.Cryptography.SHA512.Create(),
            "SHA1" => System.Security.Cryptography.SHA1.Create(),
            "MD5" => System.Security.Cryptography.MD5.Create(),
            _ => throw new XPScriptRuntimeException(5, "FileHash algorithm must be SHA256, SHA384, SHA512, SHA1, or MD5.")
        };
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(hash.ComputeHash(stream));
    }

    public static bool FileEquals(object? leftValue, object? rightValue)
    {
        var left = Path.GetFullPath(XPScriptRuntime.CStr(leftValue));
        var right = Path.GetFullPath(XPScriptRuntime.CStr(rightValue));
        var leftInfo = new System.IO.FileInfo(left);
        var rightInfo = new System.IO.FileInfo(right);
        if (!leftInfo.Exists || !rightInfo.Exists) return false;
        if (leftInfo.Length != rightInfo.Length) return false;
        if (string.Equals(left, right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return true;

        const int bufferSize = 128 * 1024;
        using var a = new FileStream(left, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
        using var b = new FileStream(right, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan);
        var ab = new byte[bufferSize];
        var bb = new byte[bufferSize];
        while (true)
        {
            var ac = a.Read(ab, 0, ab.Length);
            var bc = b.Read(bb, 0, bb.Length);
            if (ac != bc) return false;
            if (ac == 0) return true;
            if (!ab.AsSpan(0, ac).SequenceEqual(bb.AsSpan(0, bc))) return false;
        }
    }

    public static string[] Files(object? pathOrPattern, object? maskValue = null, bool recursive = false, int maxDepth = 3) =>
        EnumeratePaths(pathOrPattern, maskValue, recursive, maxDepth, directories: false).ToArray();

    public static string[] Directories(object? pathOrPattern, object? maskValue = null, bool recursive = false, int maxDepth = 3) =>
        EnumeratePaths(pathOrPattern, maskValue, recursive, maxDepth, directories: true).ToArray();

    private static IEnumerable<string> EnumeratePaths(object? pathOrPattern, object? maskValue, bool recursive, int maxDepth, bool directories)
    {
        if (maxDepth < 0 || maxDepth > 32)
            throw new XPScriptRuntimeException(5, "Filesystem maxDepth must be between 0 and 32.");

        var raw = XPScriptRuntime.CStr(pathOrPattern);
        if (string.IsNullOrWhiteSpace(raw)) raw = ".";
        string root;
        string mask;
        if (maskValue is not null)
        {
            root = Path.GetFullPath(raw);
            mask = XPScriptRuntime.CStr(maskValue);
            if (string.IsNullOrWhiteSpace(mask)) mask = "*";
        }
        else if (raw.IndexOfAny(['*', '?']) >= 0)
        {
            var directoryPart = Path.GetDirectoryName(raw);
            root = Path.GetFullPath(string.IsNullOrEmpty(directoryPart) ? "." : directoryPart);
            mask = Path.GetFileName(raw);
            if (string.IsNullOrWhiteSpace(mask)) mask = "*";
        }
        else
        {
            root = Path.GetFullPath(raw);
            mask = "*";
        }
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Directory does not exist.");

        foreach (var value in EnumerateLimited(root, mask, recursive ? maxDepth : 0, directories))
            yield return Path.GetFullPath(value);
    }

    private static IEnumerable<string> EnumerateLimited(string root, string mask, int maxDepth, bool directories)
    {
        var pending = new Stack<(string Directory, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            var matches = directories
                ? Directory.EnumerateDirectories(current.Directory, mask, SearchOption.TopDirectoryOnly)
                : Directory.EnumerateFiles(current.Directory, mask, SearchOption.TopDirectoryOnly);
            foreach (var match in matches) yield return match;

            if (current.Depth >= maxDepth) continue;
            foreach (var child in Directory.EnumerateDirectories(current.Directory, "*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                    pending.Push((child, current.Depth + 1));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public static string ReadFile(object? path, object? charset = null)
    {
        var encoding = ResolveTextEncoding(charset);
        using var reader = new StreamReader(Path.GetFullPath(XPScriptRuntime.CStr(path)), encoding, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static void WriteFile(object? path, object? content, object? charset = null) =>
        File.WriteAllText(Path.GetFullPath(XPScriptRuntime.CStr(path)), XPScriptRuntime.CStr(content), ResolveTextEncoding(charset));

    public static void AppendFile(object? path, object? content, object? charset = null) =>
        File.AppendAllText(Path.GetFullPath(XPScriptRuntime.CStr(path)), XPScriptRuntime.CStr(content), ResolveTextEncoding(charset));

    public static string[] ReadLines(object? path, object? charset = null)
    {
        var values = new List<string>();
        using var reader = new StreamReader(Path.GetFullPath(XPScriptRuntime.CStr(path)), ResolveTextEncoding(charset), detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = reader.ReadLine()) is not null) values.Add(line);
        return values.ToArray();
    }

    public static void WriteLines(object? path, object? values, object? charset = null)
    {
        using var writer = new StreamWriter(Path.GetFullPath(XPScriptRuntime.CStr(path)), append: false, ResolveTextEncoding(charset));
        foreach (var value in EnumerateValues(values)) writer.WriteLine(XPScriptRuntime.CStr(value));
    }

    public static byte[] ReadBytes(object? path) => File.ReadAllBytes(Path.GetFullPath(XPScriptRuntime.CStr(path)));

    public static void WriteBytes(object? path, object? values) =>
        File.WriteAllBytes(Path.GetFullPath(XPScriptRuntime.CStr(path)), ToBytes(values));

    private static IEnumerable<object?> EnumerateValues(object? values)
    {
        if (values is LSArray array)
        {
            if (!array.IsAllocated) yield break;
            var lower = array.LBound();
            var upper = array.UBound();
            for (var i = lower; i <= upper; i++) yield return array.Get(new object?[] { i });
            yield break;
        }
        if (values is System.Collections.IEnumerable enumerable && values is not string)
        {
            foreach (var value in enumerable) yield return value;
            yield break;
        }
        throw new XPScriptRuntimeException(13, "Expected an array or enumerable value.");
    }

    private static byte[] ToBytes(object? values)
    {
        if (values is byte[] bytes) return bytes;
        var result = new List<byte>();
        foreach (var value in EnumerateValues(values)) result.Add(XPScriptRuntime.CByte(value));
        return result.ToArray();
    }

    private static Encoding ResolveTextEncoding(object? charset)
    {
        if (charset is null || string.IsNullOrWhiteSpace(XPScriptRuntime.CStr(charset))) return new UTF8Encoding(false);
        var name = XPScriptRuntime.CStr(charset).Trim();
        var normalized = name.Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        if (normalized is "utf8" or "utf-8") return new UTF8Encoding(false);
        if (normalized is "utf16" or "utf-16" or "unicode") return new UnicodeEncoding(false, true);
        if (normalized is "utf-16be" or "unicodefffe") return new UnicodeEncoding(true, true);
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        try { return System.Text.Encoding.GetEncoding(name); }
        catch (ArgumentException) { throw new XPScriptRuntimeException(5, "Unsupported text charset: " + name); }
    }

'''
    runtime = runtime.replace(marker, code + marker, 1)
    runtime_path.write_text(runtime)

pre = pre_path.read_text()
if 'FileHash' not in pre:
    marker = '        source = Regex.Replace(\n            source,\n            @"(?<![\\w.])Dir\\s*\\(",\n'
    if marker not in pre:
        raise SystemExit('CrossPlatformPreprocessor marker not found')
    block = r'''        foreach (var function in new[]
        {
            "FileInfo", "FileHash", "FileEquals", "Files", "Directories",
            "ReadFile", "ReadLines", "ReadBytes", "WriteFile", "AppendFile", "WriteLines", "WriteBytes"
        })
        {
            source = Regex.Replace(
                source,
                $@"(?<![\w.]){Regex.Escape(function)}\s*\(",
                $"XPCrossPlatformRuntime.{function}(",
                RegexOptions.IgnoreCase);
        }

'''
    pre = pre.replace(marker, block + marker, 1)
    pre_path.write_text(pre)

fileio = fileio_pre_path.read_text()
if 'WriteFile|AppendFile|WriteLines|WriteBytes' not in fileio:
    marker = '            var lockMatch = Regex.Match(line, @"^(Lock|Unlock)\\s+#?([^,\\s]+)(?:\\s*,\\s*(.+))?$", RegexOptions.IgnoreCase);\n'
    if marker not in fileio:
        raise SystemExit('FileIoExtensionsPreprocessor marker not found')
    block = r'''            var wholeFileWrite = Regex.Match(line, @"^(WriteFile|AppendFile|WriteLines|WriteBytes)\s+(.+)$", RegexOptions.IgnoreCase);
            if (wholeFileWrite.Success)
            {
                output.Add(indent + $"Call XPCrossPlatformRuntime.{wholeFileWrite.Groups[1].Value}({wholeFileWrite.Groups[2].Value})");
                continue;
            }

'''
    fileio = fileio.replace(marker, block + marker, 1)
    fileio_pre_path.write_text(fileio)

sample = r'''Option Declare

Sub Main()
    Dim root As String
    Dim info As Variant
    Dim dirInfo As Variant
    Dim files As Variant
    Dim dirs As Variant
    Dim lines As Variant
    Dim bytes As Variant
    Dim item As Variant
    Dim count As Integer
    Dim h1 As String
    Dim h2 As String

    root = "xps-file-convenience"
    MkDir root
    MkDir root & "/sub"

    WriteFile root & "/alpha.txt", "alpha", "utf-8"
    AppendFile root & "/alpha.txt", "-beta", "utf-8"
    If ReadFile(root & "/alpha.txt", "utf-8") <> "alpha-beta" Then Error 9801, "ReadFile/WriteFile/AppendFile failed"

    WriteFile root & "/latin.txt", "ÅÄÖ", "iso-8859-1"
    If ReadFile(root & "/latin.txt", "iso-8859-1") <> "ÅÄÖ" Then Error 9802, "ISO charset roundtrip failed"

    WriteFile root & "/sub/nested.txt", "nested"
    WriteLines root & "/lines.txt", Array("one", "two", "three"), "utf-8"
    lines = ReadLines(root & "/lines.txt", "utf-8")
    count = 0
    ForAll item In lines
        count = count + 1
    End ForAll
    If count <> 3 Then Error 9803, "ReadLines/WriteLines array failed"

    bytes = ReadBytes(root & "/alpha.txt")
    WriteBytes root & "/copy.bin", bytes
    If Not FileEquals(root & "/alpha.txt", root & "/copy.bin") Then Error 9804, "ReadBytes/WriteBytes/FileEquals failed"

    h1 = FileHash(root & "/alpha.txt")
    h2 = FileHash(root & "/copy.bin", "SHA256")
    If Len(h1) <> 64 Or h1 <> h2 Then Error 9805, "FileHash SHA256 failed"
    If Len(FileHash(root & "/alpha.txt", "SHA384")) <> 96 Then Error 9806, "FileHash SHA384 failed"
    If Len(FileHash(root & "/alpha.txt", "SHA512")) <> 128 Then Error 9807, "FileHash SHA512 failed"

    info = FileInfo(root & "/alpha.txt")
    If info.Name <> "alpha.txt" Then Error 9808, "FileInfo.Name failed"
    If info.FullPath = "" Or info.Extension <> ".txt" Then Error 9809, "FileInfo path metadata failed"
    If info.Length <= 0 Or Not info.IsFile Or info.IsDirectory Then Error 9810, "FileInfo file flags failed"
    If info.IsLink Then Error 9811, "FileInfo link flag failed"
    If info.Attributes < 0 Then Error 9812, "FileInfo attributes failed"
    If Year(info.Created) < 1970 Or Year(info.Modified) < 1970 Or Year(info.Accessed) < 1970 Then Error 9813, "FileInfo timestamps failed"

    dirInfo = FileInfo(root & "/sub")
    If Not dirInfo.IsDirectory Or dirInfo.IsFile Then Error 9814, "FileInfo directory flags failed"

    files = Files(root, "*.txt", True, 2)
    count = 0
    ForAll item In files
        If IsFile(item) Then count = count + 1
    End ForAll
    If count < 4 Then Error 9815, "Files recursive ForAll failed"

    dirs = Directories(root)
    count = 0
    ForAll item In dirs
        If IsDir(item) Then count = count + 1
    End ForAll
    If count <> 1 Then Error 9816, "Directories ForAll failed"

    Kill root & "/alpha.txt"
    Kill root & "/latin.txt"
    Kill root & "/lines.txt"
    Kill root & "/copy.bin"
    Kill root & "/sub/nested.txt"
    RmDir root & "/sub"
    RmDir root

    Print "FILE_CONVENIENCE=OK"
End Sub
'''
sample_path.write_text(sample)

docs = docs_path.read_text()
if '`FileHash`' not in docs:
    dir_row_marker = '| `Dir` | `Dir([pattern] [, mode [, maxDepth]])`'
    pos = docs.find(dir_row_marker)
    if pos < 0:
        raise SystemExit('Dir docs row not found')
    end = docs.find('\n', pos)
    rows = '''
| `FileInfo` | `FileInfo(path)` | `path`: existing file or directory. | Returns a metadata object with `Name`, `FullPath`, `Extension`, `Length`, `Created`, `Modified`, `Accessed`, `IsFile`, `IsDirectory`, `IsLink`, and `Attributes`. | [file-convenience.xps](../samples/file-convenience.xps) |
| `FileHash` | `FileHash(path [, algorithm])` | `algorithm`: optional `SHA256` (default), `SHA384`, `SHA512`; legacy compatibility also accepts `SHA1` and `MD5`. | Streams the file and returns an uppercase hexadecimal digest. | [file-convenience.xps](../samples/file-convenience.xps) |
| `FileEquals` | `FileEquals(path1, path2)` | two file paths. | Returns `True` when files have equal length and byte-for-byte content. | [file-convenience.xps](../samples/file-convenience.xps) |
| `Files` | `Files(pathOrPattern [, mask [, recursive [, maxDepth]]])` | path/pattern, optional mask, recursive flag, optional depth `0..32` (default `3`). | Returns a String array of matching full file paths; recursive traversal skips link/reparse directories. | [file-convenience.xps](../samples/file-convenience.xps) |
| `Directories` | `Directories(pathOrPattern [, mask [, recursive [, maxDepth]]])` | path/pattern, optional mask, recursive flag, optional depth `0..32` (default `3`). | Returns a String array of matching full directory paths and works directly with `ForAll`. | [file-convenience.xps](../samples/file-convenience.xps) |
| `ReadFile` | `ReadFile(path [, charset])` | file path and optional charset. | Reads an entire text file. UTF-8 is the default; BOMs are detected. | [file-convenience.xps](../samples/file-convenience.xps) |
| `WriteFile` | `WriteFile path, content [, charset]` | path, text, optional charset. | Replaces an entire text file. Function-call syntax is also accepted. | [file-convenience.xps](../samples/file-convenience.xps) |
| `AppendFile` | `AppendFile path, content [, charset]` | path, text, optional charset. | Appends text to a file. Function-call syntax is also accepted. | [file-convenience.xps](../samples/file-convenience.xps) |
| `ReadLines` | `ReadLines(path [, charset])` | path and optional charset. | Returns a String array with one element per text line. | [file-convenience.xps](../samples/file-convenience.xps) |
| `WriteLines` | `WriteLines path, values [, charset]` | path, array/list values, optional charset. | Writes one array/list value per line. | [file-convenience.xps](../samples/file-convenience.xps) |
| `ReadBytes` | `ReadBytes(path)` | file path. | Returns the complete file as a Byte array. | [file-convenience.xps](../samples/file-convenience.xps) |
| `WriteBytes` | `WriteBytes path, values` | path and Byte array/array-like value. | Replaces a file with the supplied bytes. | [file-convenience.xps](../samples/file-convenience.xps) |
'''
    docs = docs[:end+1] + rows + docs[end+1:]

    anchor = '### `Dir` mode examples\n'
    section = '''### Whole-file convenience API\n\n`Files(...)`, `Directories(...)`, `ReadLines(...)`, and `ReadBytes(...)` return arrays that can be consumed directly by `ForAll`. Recursive `Files`/`Directories` use the same bounded-depth model as recursive `Dir`: default depth `3`, valid range `0..32`, and link/reparse-point directories are not traversed.\n\nText helpers accept .NET charset names. Common names include `utf-8`, `utf-16`, `utf-16be`, `iso-8859-1`, and other ISO/code-page names available through the platform encoding provider. UTF-8 without BOM is used when charset is omitted; readers still detect BOMs.\n\n`FileHash` defaults to SHA-256. SHA-384 and SHA-512 are recommended alternatives. SHA-1 and MD5 are available only for compatibility with legacy file manifests and should not be chosen for security-sensitive integrity checks.\n\n'''
    if anchor not in docs:
        raise SystemExit('Dir examples docs anchor not found')
    docs = docs.replace(anchor, section + anchor, 1)
    docs_path.write_text(docs)

print('filesystem convenience implementation applied')
