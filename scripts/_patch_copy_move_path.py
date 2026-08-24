from pathlib import Path

fs_path = Path('src/XPScript.Compiler/FileSystemPortabilityRuntimeSource.cs')
cross_path = Path('src/XPScript.Compiler/CrossPlatformRuntimeSource.cs')
pre_path = Path('src/XPScript.Compiler/CrossPlatformPreprocessor.cs')
sample_path = Path('samples/file-convenience.xps')
docs_path = Path('docs/file-io-reference.md')

fs = fs_path.read_text()
fs = fs.replace('public static void CopyFile(object? sourceValue, object? destinationValue)\n    {', 'public static void CopyFile(object? sourceValue, object? destinationValue, bool overwrite = true)\n    {', 1)
fs = fs.replace('File.Move(stage, destination, overwrite: true);', 'File.Move(stage, destination, overwrite: overwrite);', 1)
fs = fs.replace('public static void MoveFile(object? sourceValue, object? destinationValue)\n    {', 'public static void MoveFile(object? sourceValue, object? destinationValue, bool overwrite = true)\n    {', 1)
fs = fs.replace('File.Move(source, destination, overwrite: true);', 'File.Move(source, destination, overwrite: overwrite);', 1)
fs_path.write_text(fs)

cross = cross_path.read_text()
marker = '    public static bool IsDir(object? path) => Directory.Exists(XPScriptRuntime.CStr(path));\n\n'
if 'public static bool CopyFile(object? source' not in cross:
    block = r'''    public static bool CopyFile(object? source, object? target, int action = 1) =>
        ApplyFileTransferPolicy(source, target, action, move: false);

    public static bool MoveFile(object? source, object? target, int action = 1) =>
        ApplyFileTransferPolicy(source, target, action, move: true);

    private static bool ApplyFileTransferPolicy(object? sourceValue, object? targetValue, int action, bool move)
    {
        if (action < 1 || action > 3)
            throw new XPScriptRuntimeException(5, "File transfer action must be 1 (fail), 2 (overwrite), or 3 (skip).");

        var source = XPScriptFileSystemRuntime.ResolvePath(sourceValue);
        var target = XPScriptFileSystemRuntime.ResolvePath(targetValue);
        if (!File.Exists(source)) return false;

        if (File.Exists(target) || Directory.Exists(target))
        {
            if (action == 1 || action == 3) return false;
            if (Directory.Exists(target)) return false;
        }

        try
        {
            if (move) XPScriptFileSystemRuntime.MoveFile(source, target, overwrite: action == 2);
            else XPScriptFileSystemRuntime.CopyFile(source, target, overwrite: action == 2);
            return true;
        }
        catch (IOException) when (action != 2 && (File.Exists(target) || Directory.Exists(target)))
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static class PathApi
    {
        public static string Combine(object? left, object? right) =>
            System.IO.Path.Combine(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right));

        public static string FileName(object? path) =>
            System.IO.Path.GetFileName(XPScriptRuntime.CStr(path));

        public static string Extension(object? path) =>
            System.IO.Path.GetExtension(XPScriptRuntime.CStr(path));

        public static string Directory(object? path) =>
            System.IO.Path.GetDirectoryName(XPScriptFileSystemRuntime.ResolvePath(path)) ?? "";

        public static string Absolute(object? path) =>
            XPScriptFileSystemRuntime.ResolvePath(path);

        public static string Relative(object? basePath, object? path) =>
            System.IO.Path.GetRelativePath(XPScriptFileSystemRuntime.ResolvePath(basePath), XPScriptFileSystemRuntime.ResolvePath(path));

        public static bool Exists(object? path)
        {
            var value = XPScriptRuntime.CStr(path);
            return FileExists(value) || DirExists(value);
        }
    }

'''
    if marker not in cross:
        raise SystemExit('CrossPlatformRuntime insertion marker not found')
    cross = cross.replace(marker, marker + block, 1)
cross_path.write_text(cross)

pre = pre_path.read_text()
if 'CopyFile", "MoveFile"' not in pre:
    pre = pre.replace('"FileInfo", "FileHash", "FileEquals", "Files", "Directories",', '"FileInfo", "FileHash", "FileEquals", "Files", "Directories", "CopyFile", "MoveFile",', 1)
if 'PathApi' not in pre:
    insert = r'''        foreach (var method in new[] { "Combine", "FileName", "Extension", "Directory", "Absolute", "Relative", "Exists" })
        {
            source = Regex.Replace(
                source,
                $@"(?<![\w.])Path\s*\.\s*{Regex.Escape(method)}\s*\(",
                $"XPCrossPlatformRuntime.PathApi.{method}(",
                RegexOptions.IgnoreCase);
        }

'''
    target = '        source = Regex.Replace(\n            source,\n            @"(?<![\\w.])Dir\\s*\\(",\n'
    if target not in pre:
        raise SystemExit('CrossPlatformPreprocessor Path insertion marker not found')
    pre = pre.replace(target, insert + target, 1)
pre_path.write_text(pre)

sample = sample_path.read_text()
if 'COPY_MOVE_PATH=OK' not in sample:
    needle = '    Kill root & "/alpha.txt"\n'
    block = r'''    WriteFile root & "/copy-source.txt", "one"
    If Not CopyFile(root & "/copy-source.txt", root & "/copy-target.txt") Then
        Error 9820, "CopyFile default fail policy should copy to a missing target"
    End If
    If CopyFile(root & "/copy-source.txt", root & "/copy-target.txt") Then
        Error 9821, "CopyFile default fail policy should return False when target exists"
    End If
    If CopyFile(root & "/copy-source.txt", root & "/copy-target.txt", 3) Then
        Error 9822, "CopyFile skip policy should return False when skipped"
    End If
    WriteFile root & "/copy-source.txt", "two"
    If Not CopyFile(root & "/copy-source.txt", root & "/copy-target.txt", 2) Then
        Error 9823, "CopyFile overwrite policy failed"
    End If
    If ReadFile(root & "/copy-target.txt") <> "two" Then
        Error 9824, "CopyFile overwrite content failed"
    End If

    WriteFile root & "/move-source.txt", "move"
    If Not MoveFile(root & "/move-source.txt", root & "/move-target.txt") Then
        Error 9825, "MoveFile default fail policy failed"
    End If
    If IsFile(root & "/move-source.txt") Or Not IsFile(root & "/move-target.txt") Then
        Error 9826, "MoveFile did not move the file"
    End If

    If Path.FileName(Path.Combine(root, "copy-target.txt")) <> "copy-target.txt" Then
        Error 9827, "Path.Combine/FileName failed"
    End If
    If Path.Extension(root & "/copy-target.txt") <> ".txt" Then
        Error 9828, "Path.Extension failed"
    End If
    If Path.Directory(root & "/copy-target.txt") = "" Then
        Error 9829, "Path.Directory failed"
    End If
    If Not Path.Exists(root & "/copy-target.txt") Then
        Error 9830, "Path.Exists failed"
    End If
    If Path.Absolute(root & "/copy-target.txt") = "" Then
        Error 9831, "Path.Absolute failed"
    End If
    If Path.Relative(root, root & "/copy-target.txt") <> "copy-target.txt" Then
        Error 9832, "Path.Relative failed"
    End If
    Print "COPY_MOVE_PATH=OK"

    Kill root & "/copy-source.txt"
    Kill root & "/copy-target.txt"
    Kill root & "/move-target.txt"

'''
    if needle not in sample:
        raise SystemExit('sample insertion marker not found')
    sample = sample.replace(needle, block + needle, 1)
sample_path.write_text(sample)

docs = docs_path.read_text()
if '## CopyFile and MoveFile' not in docs:
    docs += r'''

## CopyFile and MoveFile

`CopyFile(source, target [, action])` and `MoveFile(source, target [, action])` return `True` only when a file was actually copied or moved. The optional `action` defaults to `1`.

- `1` = fail if the target already exists; returns `False` and leaves both files unchanged.
- `2` = overwrite an existing target; returns `True` when the transfer succeeds.
- `3` = skip if the target already exists; returns `False` because no transfer was performed.

A missing source or unavailable destination directory returns `False`. Invalid action values raise runtime error 5. The legacy `FileCopy` statement remains available for compatibility and keeps its existing behavior.

```xpscript
ok = CopyFile("in.dat", "out.dat")
ok = CopyFile("in.dat", "out.dat", 2)
ok = MoveFile("out.dat", "archive/out.dat", 3)
```

## Path object

The built-in `Path` object centralizes cross-platform path manipulation and reuses XPscript's existing filesystem path normalization and existence checks.

```xpscript
full = Path.Combine("data", "config.json")
name = Path.FileName(full)
ext = Path.Extension(full)
dir = Path.Directory(full)
absolute = Path.Absolute(full)
relative = Path.Relative("data", absolute)
exists = Path.Exists(full)
```

`Path.Combine(left, right)` joins two path parts using the target platform separator. `Path.FileName(path)` returns the final file or directory name. `Path.Extension(path)` returns the extension including the leading dot or an empty string. `Path.Directory(path)` returns the absolute parent directory. `Path.Absolute(path)` resolves an absolute path. `Path.Relative(basePath, path)` calculates `path` relative to `basePath`. `Path.Exists(path)` returns `True` for either an existing file or an existing directory and internally reuses the existing `FileExists`/`DirExists` behavior.
'''
docs_path.write_text(docs)

print('patched copy/move/path APIs')
