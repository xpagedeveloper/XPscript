namespace XPScript.Compiler;

internal sealed class FileSystemPortabilityPostProcessor
{
    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        generated = System.Text.RegularExpressions.Regex.Replace(
            generated,
            @"\.(StartsWith|EndsWith)\('([^']{1})',\s*(StringComparison\.[A-Za-z]+)\)",
            m => $".{m.Groups[1].Value}(\"{m.Groups[2].Value}\", {m.Groups[3].Value})",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        generated = generated.Replace("Encoding.Default", "XPScriptFileSystemRuntime.LegacyEncoding", StringComparison.Ordinal);
        generated = generated.Replace("Path.GetFullPath(XPScriptRuntime.CStr(pathValue))", "XPScriptFileSystemRuntime.ResolvePath(pathValue)", StringComparison.Ordinal);
        generated = generated.Replace("Path.GetFullPath(CStr(pathValue))", "XPScriptFileSystemRuntime.ResolvePath(pathValue)", StringComparison.Ordinal);
        generated = generated.Replace("new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)", "XPScriptFileSystemRuntime.OpenInputStream(path)", StringComparison.Ordinal);
        generated = generated.Replace("new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read)", "XPScriptFileSystemRuntime.OpenOutputStream(path, append)", StringComparison.Ordinal);
        generated = generated.Replace("new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)", "XPScriptFileSystemRuntime.OpenBinaryStream(path)", StringComparison.Ordinal);
        generated = generated.Replace("new StreamReader(path, charset, detectEncodingFromByteOrderMarks: true)", "new StreamReader(XPScriptFileSystemRuntime.OpenInputStream(path), charset, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false)", StringComparison.Ordinal);
        generated = generated.Replace("public static long FileLen(object? fileName) => new FileInfo(CStr(fileName)).Length;", "public static long FileLen(object? fileName) => XPScriptFileSystemRuntime.FileLen(fileName);", StringComparison.Ordinal);
        generated = generated.Replace("public static DateTime FileDateTime(object? fileName) => File.GetLastWriteTime(CStr(fileName));", "public static DateTime FileDateTime(object? fileName) => XPScriptFileSystemRuntime.FileDateTime(fileName);", StringComparison.Ordinal);
        generated = generated.Replace("public static int GetFileAttr(object? fileName) => (int)File.GetAttributes(CStr(fileName));", "public static int GetFileAttr(object? fileName) => XPScriptFileSystemRuntime.GetFileAttr(fileName);", StringComparison.Ordinal);
        generated = generated.Replace("public static void SetFileAttr(object? fileName, int attributes) => File.SetAttributes(CStr(fileName), (FileAttributes)attributes);", "public static void SetFileAttr(object? fileName, int attributes) => XPScriptFileSystemRuntime.SetFileAttr(fileName, attributes);", StringComparison.Ordinal);
        generated = generated.Replace("public static void FileCopy(object? source, object? destination) => File.Copy(CStr(source), CStr(destination), true);", "public static void FileCopy(object? source, object? destination) => XPScriptFileSystemRuntime.CopyFile(source, destination);", StringComparison.Ordinal);
        generated = generated.Replace("public static void Kill(object? path) => File.Delete(CStr(path));", "public static void Kill(object? path) => XPScriptFileSystemRuntime.DeleteFile(path);", StringComparison.Ordinal);
        generated = generated.Replace("public static void NameFile(object? oldPath, object? newPath) => File.Move(CStr(oldPath), CStr(newPath), true);", "public static void NameFile(object? oldPath, object? newPath) => XPScriptFileSystemRuntime.MoveFile(oldPath, newPath);", StringComparison.Ordinal);
        generated = generated.Replace("public static void MkDir(object? path) => Directory.CreateDirectory(CStr(path));", "public static void MkDir(object? path) => XPScriptFileSystemRuntime.MakeDirectory(path);", StringComparison.Ordinal);
        generated = generated.Replace("public static void RmDir(object? path) => Directory.Delete(CStr(path), false);", "public static void RmDir(object? path) => XPScriptFileSystemRuntime.RemoveDirectory(path);", StringComparison.Ordinal);
        generated = generated.Replace("public static void ChDir(object? path) => Environment.CurrentDirectory = Path.GetFullPath(CStr(path));", "public static void ChDir(object? path) => XPScriptFileSystemRuntime.ChangeDirectory(path);", StringComparison.Ordinal);

        generated = generated.Replace(
            "DirEnumerator = Directory.EnumerateFileSystemEntries(directory, mask)\n                .Select(Path.GetFileName)\n                .Where(x => x is not null)\n                .Cast<string>()\n                .GetEnumerator();",
            "DirEnumerator = XPScriptFileSystemRuntime.Enumerate(raw).GetEnumerator();",
            StringComparison.Ordinal);

        return generated;
    }
}
