from pathlib import Path

runtime_path = Path('src/XPScript.Compiler/CrossPlatformRuntimeSource.cs')
pre_path = Path('src/XPScript.Compiler/CrossPlatformPreprocessor.cs')
sample_path = Path('samples/file-convenience.xps')
docs_path = Path('docs/file-io-reference.md')
transpiler_path = Path('src/XPScript.Compiler/XPScriptTranspiler.cs')

runtime = runtime_path.read_text()
start = runtime.find('    public static class PathApi\n    {')
end_marker = '\n    public static string Dir(object? pattern = null, int mode = 0, int maxDepth = 3)'
end = runtime.find(end_marker, start)
if start < 0 or end < 0:
    raise SystemExit('PathApi block not found')
block = r'''    public sealed class XPPathValue
    {
        private readonly string _path;

        public XPPathValue(object? path)
        {
            _path = XPScriptRuntime.CStr(path);
            if (string.IsNullOrWhiteSpace(_path))
                throw new XPScriptRuntimeException(5, "Path must not be empty.");
        }

        public string FileName() => System.IO.Path.GetFileName(_path);
        public string FileNameWithoutExtension() => System.IO.Path.GetFileNameWithoutExtension(_path);
        public string Extension() => System.IO.Path.GetExtension(_path);
        public string Directory() => System.IO.Path.GetDirectoryName(Absolute()) ?? "";
        public string Root() => System.IO.Path.GetPathRoot(Absolute()) ?? "";
        public string Normalize() => System.IO.Path.GetFullPath(_path);
        public string Absolute() => XPScriptFileSystemRuntime.ResolvePath(_path);
        public string Relative(object? targetPath) =>
            System.IO.Path.GetRelativePath(Absolute(), XPScriptFileSystemRuntime.ResolvePath(targetPath));
        public string ChangeExtension(object? extension) =>
            System.IO.Path.ChangeExtension(_path, XPScriptRuntime.CStr(extension)) ?? _path;
        public bool IsAbsolute() => System.IO.Path.IsPathFullyQualified(_path);
        public bool Exists() => FileExists(_path) || DirExists(_path);
        public string Combine(object? child) => System.IO.Path.Combine(_path, XPScriptRuntime.CStr(child));
        public override string ToString() => _path;
    }

    public static XPPathValue PathValue(object? path) => new(path);
'''
runtime = runtime[:start] + block + runtime[end:]
runtime_path.write_text(runtime)

pre = pre_path.read_text()
# Remove static Path.method rewrites.
loop_start = pre.find('        foreach (var method in new[] { "Combine", "FileName", "Extension", "Directory", "Absolute", "Relative", "Exists" })')
if loop_start >= 0:
    loop_end = pre.find('        source = Regex.Replace(\n            source,\n            @"(?<![\\w.])Dir\\s*\\(",', loop_start)
    if loop_end < 0:
        raise SystemExit('Path method rewrite end not found')
    pre = pre[:loop_start] + pre[loop_end:]
# Rewrite constructor expression New Path(...)
insert_marker = '        source = Regex.Replace(\n            source,\n            @"(?<![\\w.])Dir\\s*\\(",'
constructor = r'''        source = Regex.Replace(
            source,
            @"\bNew\s+Path\s*\(",
            "XPCrossPlatformRuntime.PathValue(",
            RegexOptions.IgnoreCase);

'''
if constructor.strip() not in pre:
    pre = pre.replace(insert_marker, constructor + insert_marker, 1)
pre_path.write_text(pre)

# Ensure builtin rewrite happens before VariantIndex to avoid Files/files ambiguity.
trans = transpiler_path.read_text()
needle = '        protectedSource = new HclSelectedCompatibilityPreprocessor().Transform(protectedSource);\n        protectedSource = new VariantIndexPreprocessor().Transform(protectedSource);'
replacement = '        protectedSource = new HclSelectedCompatibilityPreprocessor().Transform(protectedSource);\n        protectedSource = new CrossPlatformPreprocessor().Transform(protectedSource);\n        protectedSource = new VariantIndexPreprocessor().Transform(protectedSource);'
if needle in trans:
    trans = trans.replace(needle, replacement, 1)
# Keep later pass harmless but avoid duplicate execution.
trans = trans.replace('        protectedSource = new ReferenceRuntimeExtensionsPreprocessor().Transform(protectedSource);\n        protectedSource = new CrossPlatformPreprocessor().Transform(protectedSource);', '        protectedSource = new ReferenceRuntimeExtensionsPreprocessor().Transform(protectedSource);', 1)
transpiler_path.write_text(trans)

sample = sample_path.read_text()
old = r'''    If Path.FileName(Path.Combine(root, "copy-target.txt")) <> "copy-target.txt" Then
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
'''
new = r'''    Dim p As Variant
    p = New Path(root & "/copy-target.txt")
    If p.FileName() <> "copy-target.txt" Then
        Error 9827, "Path.FileName failed"
    End If
    If p.FileNameWithoutExtension() <> "copy-target" Then
        Error 9828, "Path.FileNameWithoutExtension failed"
    End If
    If p.Extension() <> ".txt" Then
        Error 9829, "Path.Extension failed"
    End If
    If p.Directory() = "" Or p.Root() = "" Then
        Error 9830, "Path.Directory/Root failed"
    End If
    If Not p.Exists() Or p.Absolute() = "" Or p.Normalize() = "" Then
        Error 9831, "Path.Exists/Absolute/Normalize failed"
    End If
    If p.ChangeExtension(".json") = "" Then
        Error 9832, "Path.ChangeExtension failed"
    End If
    If p.IsAbsolute() Then
        Error 9833, "relative Path should not report absolute"
    End If
    Dim rootPath As Variant
    rootPath = New Path(root)
    If rootPath.Relative(root & "/copy-target.txt") <> "copy-target.txt" Then
        Error 9834, "Path.Relative failed"
    End If
    If rootPath.Combine("child.txt") = "" Then
        Error 9835, "Path.Combine failed"
    End If
'''
if old not in sample:
    raise SystemExit('old static Path sample block not found')
sample = sample.replace(old, new, 1)
sample_path.write_text(sample)

docs = docs_path.read_text()
start = docs.find('## Path object')
if start >= 0:
    docs = docs[:start].rstrip() + '\n\n'
docs += r'''## Path object

`Path` is an instance object. The path is supplied once when the object is created and all methods operate on that stored path.

```xpscript
p = New Path("src/test/data.json")
Print p.FileName()
Print p.FileNameWithoutExtension()
Print p.Extension()
Print p.Directory()
Print p.Root()
Print p.Normalize()
Print p.Absolute()
Print p.Relative("src/test/archive.json")
Print p.ChangeExtension(".xml")
Print p.IsAbsolute()
Print p.Exists()
Print p.Combine("child.txt")
```

Methods:

- `FileName()` returns the final file or directory name.
- `FileNameWithoutExtension()` removes the final extension.
- `Extension()` returns the extension including the leading dot, or an empty string.
- `Directory()` returns the absolute parent directory.
- `Root()` returns the filesystem root of the absolute path.
- `Normalize()` normalizes the stored path using the target platform path rules.
- `Absolute()` resolves the stored path to an absolute path.
- `Relative(targetPath)` returns `targetPath` relative to this Path object's stored path.
- `ChangeExtension(newExtension)` returns the stored path with its extension replaced; it does not rename the file.
- `IsAbsolute()` reports whether the originally supplied path is fully qualified.
- `Exists()` returns `True` when the stored path identifies an existing file or directory and reuses the existing `FileExists`/`DirExists` checks.
- `Combine(child)` combines the stored path with one child path component.
'''
docs_path.write_text(docs)

print('patched instance Path API')
