namespace XPScript.Compiler;

internal static class NotesMimeMultipartSemanticsPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = ReplaceRequired(source, "    private readonly int[] _mimePath;", "    private int[] _mimePath;", "mutable-path");
        source = ReplaceRequired(source,
            "        _raw = XPScriptMimeMultipartTree.WrapAt(_raw, _mimePath);\n        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);\n        var parentPath = _mimePath;\n        return OpenAt(parentPath);",
            "        var parentPath = _mimePath;\n        _raw = XPScriptMimeMultipartTree.WrapAt(_raw, parentPath);\n        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);\n        _mimePath = [.. parentPath, 0];\n        _message = XPScriptMimeMultipartTree.ParseAt(_raw, _mimePath);\n        return OpenAt(parentPath);",
            "create-parent-identity");
        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal)) throw new CompilerException("Unable to apply NotesMIMEEntity multipart semantics (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
