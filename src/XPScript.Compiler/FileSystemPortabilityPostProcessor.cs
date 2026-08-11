namespace XPScript.Compiler;

internal sealed class FileSystemPortabilityPostProcessor
{
    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        // Never let implicit file encoding vary with the target operating system.
        generated = generated.Replace(
            "Encoding.Default",
            "XPScriptFileSystemRuntime.LegacyEncoding",
            StringComparison.Ordinal);

        // Route both core and Charset-aware Open paths through one target-OS resolver.
        generated = generated.Replace(
            "Path.GetFullPath(XPScriptRuntime.CStr(pathValue))",
            "XPScriptFileSystemRuntime.ResolvePath(pathValue)",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "Path.GetFullPath(CStr(pathValue))",
            "XPScriptFileSystemRuntime.ResolvePath(pathValue)",
            StringComparison.Ordinal);

        // Centralize FileShare semantics. Binary/Random intentionally allow multiple
        // read/write handles so Lock/Unlock, rather than Open itself, coordinates regions.
        generated = generated.Replace(
            "new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)",
            "XPScriptFileSystemRuntime.OpenInputStream(path)",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read)",
            "XPScriptFileSystemRuntime.OpenOutputStream(path, append)",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)",
            "XPScriptFileSystemRuntime.OpenBinaryStream(path)",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "new StreamReader(path, charset, detectEncodingFromByteOrderMarks: true)",
            "new StreamReader(XPScriptFileSystemRuntime.OpenInputStream(path), charset, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: false)",
            StringComparison.Ordinal);

        // Route the standard filesystem surface through the same portability layer.
        generated = generated.Replace(
            "public static long FileLen(object? fileName) => new FileInfo(CStr(fileName)).Length;",
            "public static long FileLen(object? fileName) => XPScriptFileSystemRuntime.FileLen(fileName);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static DateTime FileDateTime(object? fileName) => File.GetLastWriteTime(CStr(fileName));",
            "public static DateTime FileDateTime(object? fileName) => XPScriptFileSystemRuntime.FileDateTime(fileName);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static int GetFileAttr(object? fileName) => (int)File.GetAttributes(CStr(fileName));",
            "public static int GetFileAttr(object? fileName) => XPScriptFileSystemRuntime.GetFileAttr(fileName);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static void SetFileAttr(object? fileName, int attributes) => File.SetAttributes(CStr(fileName), (FileAttributes)attributes);",
            "public static void SetFileAttr(object? fileName, int attributes) => XPScriptFileSystemRuntime.SetFileAttr(fileName, attributes);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static void FileCopy(object? source, object? destination) => File.Copy(CStr(source), CStr(destination), true);",
            "public static void FileCopy(object? source, object? destination) => XPScriptFileSystemRuntime.CopyFile(source, destination);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static void Kill(object? path) => File.Delete(CStr(path));",
            "public static void Kill(object? path) => XPScriptFileSystemRuntime.DeleteFile(path);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static void NameFile(object? oldPath, object? newPath) => File.Move(CStr(oldPath), CStr(newPath), true);",
            "public static void NameFile(object? oldPath, object? newPath) => XPScriptFileSystemRuntime.MoveFile(oldPath, newPath);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static void MkDir(object? path) => Directory.CreateDirectory(CStr(path));",
            "public static void MkDir(object? path) => XPScriptFileSystemRuntime.MakeDirectory(path);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static void RmDir(object? path) => Directory.Delete(CStr(path), false);",
            "public static void RmDir(object? path) => XPScriptFileSystemRuntime.RemoveDirectory(path);",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "public static void ChDir(object? path) => Environment.CurrentDirectory = Path.GetFullPath(CStr(path));",
            "public static void ChDir(object? path) => XPScriptFileSystemRuntime.ChangeDirectory(path);",
            StringComparison.Ordinal);

        // Dir keeps its stateful iterator in XPScriptRuntime, but delegates directory/mask
        // resolution and filesystem case semantics to the portability layer.
        generated = generated.Replace(
            "DirEnumerator = Directory.EnumerateFileSystemEntries(directory, mask)\n                .Select(Path.GetFileName)\n                .Where(x => x is not null)\n                .Cast<string>()\n                .GetEnumerator();",
            "DirEnumerator = XPScriptFileSystemRuntime.Enumerate(raw).GetEnumerator();",
            StringComparison.Ordinal);

        // Evaluate collection isolation is finalized after all runtime source blocks have
        // been appended. Input snapshots are internal/read-only; returned Lists are converted
        // back to detached normal XPScript List values.
        generated = generated.Replace(
            "return new Evaluator(source, Snapshot(callvar)).Run();",
            "return new Evaluator(source, XPScriptEvaluateCollectionRuntime.Snapshot(callvar)).Run();",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "return Snapshot(ParseExpression());",
            "return XPScriptEvaluateCollectionRuntime.SnapshotReturn(ParseExpression());",
            StringComparison.Ordinal);

        const string oldReadCallvar = """
        private object? ReadCallvar(IReadOnlyList<object?> args)
        {
            if (_callvar is LSArray array) return array.Get(args.ToArray());
            if (_callvar is Array clrArray)
            {
                if (args.Count != 1) throw Error("CLR array callvar requires one index.");
                return clrArray.GetValue(XPScriptRuntime.CInt(args[0]));
            }
            throw Error("callvar is not an indexed value.");
        }
""";

        const string newReadCallvar = """
        private object? ReadCallvar(IReadOnlyList<object?> args) =>
            XPScriptEvaluateCollectionRuntime.ReadIndexed(_callvar, args);
""";
        generated = generated.Replace(oldReadCallvar, newReadCallvar, StringComparison.Ordinal);

        // Evaluate uses the same dynamic '+' coercion and comparison rules as the main
        // XPScript runtime instead of maintaining subtly different evaluator-only behavior.
        generated = generated.Replace(
            "if (Match(TokenKind.Plus)) value = Add(value, ParseMultiplicative());",
            "if (Match(TokenKind.Plus)) value = XPScriptCoercion.AddVariant(value, ParseMultiplicative());",
            StringComparison.Ordinal);

        const string oldCompare = """
        private static bool Compare(object? left, object? right, TokenKind op)
        {
            int comparison;
            if (left is DateTime || right is DateTime) comparison = DateTime.Compare(XPScriptRuntime.CDate(left), XPScriptRuntime.CDate(right));
            else if (XPScriptRuntime.IsNumeric(left) && XPScriptRuntime.IsNumeric(right)) comparison = XPScriptRuntime.CDbl(left).CompareTo(XPScriptRuntime.CDbl(right));
            else comparison = string.Compare(XPScriptRuntime.CStr(left), XPScriptRuntime.CStr(right), StringComparison.CurrentCulture);
            return op switch { TokenKind.Equal => comparison == 0, TokenKind.NotEqual => comparison != 0, TokenKind.Less => comparison < 0, TokenKind.LessEqual => comparison <= 0, TokenKind.Greater => comparison > 0, TokenKind.GreaterEqual => comparison >= 0, _ => false };
        }
""";

        const string newCompare = """
        private static bool Compare(object? left, object? right, TokenKind op)
        {
            var operation = op switch
            {
                TokenKind.Equal => "=",
                TokenKind.NotEqual => "<>",
                TokenKind.Less => "<",
                TokenKind.LessEqual => "<=",
                TokenKind.Greater => ">",
                TokenKind.GreaterEqual => ">=",
                _ => throw new XPScriptRuntimeException(5, "Unsupported Evaluate comparison operator.")
            };
            return XPScriptEvaluateSemanticsRuntime.Compare(left, right, operation);
        }
""";
        generated = generated.Replace(oldCompare, newCompare, StringComparison.Ordinal);

        generated = generated.Replace(
            "throw new XPScriptRuntimeException(5, \"Evaluate failed: \" + ex.Message);",
            "throw XPScriptEvaluateSemanticsRuntime.Normalize(ex);",
            StringComparison.Ordinal);

        generated += "\n\n" + EvaluateCollectionIsolationRuntimeSource.Code + "\n";
        generated += "\n\n" + EvaluateSemanticsRuntimeSource.Code + "\n";
        return generated;
    }
}
