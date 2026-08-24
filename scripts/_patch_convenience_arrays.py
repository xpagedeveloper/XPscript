from pathlib import Path
p=Path('src/XPScript.Compiler/CrossPlatformRuntimeSource.cs')
s=p.read_text()
old='''    public static string[] Files(object? pathOrPattern, object? maskValue = null, bool recursive = false, int maxDepth = 3) =>
        EnumeratePaths(pathOrPattern, maskValue, recursive, maxDepth, directories: false).ToArray();

    public static string[] Directories(object? pathOrPattern, object? maskValue = null, bool recursive = false, int maxDepth = 3) =>
        EnumeratePaths(pathOrPattern, maskValue, recursive, maxDepth, directories: true).ToArray();
'''
new='''    public static LSArray Files(object? pathOrPattern, object? maskValue = null, bool recursive = false, int maxDepth = 3) =>
        ToXPScriptArray("String", EnumeratePaths(pathOrPattern, maskValue, recursive, maxDepth, directories: false).Cast<object?>());

    public static LSArray Directories(object? pathOrPattern, object? maskValue = null, bool recursive = false, int maxDepth = 3) =>
        ToXPScriptArray("String", EnumeratePaths(pathOrPattern, maskValue, recursive, maxDepth, directories: true).Cast<object?>());
'''
if old not in s: raise SystemExit('Files/Directories block not found')
s=s.replace(old,new,1)
old='''    public static string[] ReadLines(object? path, object? charset = null)
    {
        var values = new List<string>();
        using var reader = new StreamReader(Path.GetFullPath(XPScriptRuntime.CStr(path)), ResolveTextEncoding(charset), detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = reader.ReadLine()) is not null) values.Add(line);
        return values.ToArray();
    }
'''
new='''    public static LSArray ReadLines(object? path, object? charset = null)
    {
        var values = new List<object?>();
        using var reader = new StreamReader(Path.GetFullPath(XPScriptRuntime.CStr(path)), ResolveTextEncoding(charset), detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = reader.ReadLine()) is not null) values.Add(line);
        return ToXPScriptArray("String", values);
    }
'''
if old not in s: raise SystemExit('ReadLines block not found')
s=s.replace(old,new,1)
old='''    public static byte[] ReadBytes(object? path) => File.ReadAllBytes(Path.GetFullPath(XPScriptRuntime.CStr(path)));

    public static void WriteBytes(object? path, object? values) =>
        File.WriteAllBytes(Path.GetFullPath(XPScriptRuntime.CStr(path)), ToBytes(values));
'''
new='''    public static LSArray ReadBytes(object? path)
    {
        var bytes = File.ReadAllBytes(Path.GetFullPath(XPScriptRuntime.CStr(path)));
        return ToXPScriptArray("Byte", bytes.Cast<object?>());
    }

    public static void WriteBytes(object? path, object? values) =>
        File.WriteAllBytes(Path.GetFullPath(XPScriptRuntime.CStr(path)), ToBytes(values));
'''
if old not in s: raise SystemExit('ReadBytes block not found')
s=s.replace(old,new,1)
marker='''    private static IEnumerable<object?> EnumerateValues(object? values)
'''
helper='''    private static LSArray ToXPScriptArray(string elementType, IEnumerable<object?> values)
    {
        var items = values.ToList();
        if (items.Count == 0) return new LSArray(elementType, true);
        var array = new LSArray(elementType, true, [0], [items.Count - 1]);
        for (var i = 0; i < items.Count; i++) array.Set(items[i], i);
        return array;
    }

'''
if marker not in s: raise SystemExit('array helper marker not found')
s=s.replace(marker,helper+marker,1)
p.write_text(s)
print('patched convenience arrays to LSArray')
