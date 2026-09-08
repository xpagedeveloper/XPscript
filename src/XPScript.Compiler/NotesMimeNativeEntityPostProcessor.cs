namespace XPScript.Compiler;

internal static class NotesMimeNativeEntityPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    private byte[] _raw;\n    private XPScriptMimeMessage _message;",
            "    private byte[] _raw;\n    private XPScriptMimeMessage _message;\n    private readonly XPScriptNotesMimeDirectoryOwner _mimeDirectoryOwner;\n    private readonly nint _nativeEntity;",
            "mime-native-fields");

        source = ReplaceRequired(source,
            "        _document = document;\n        _itemName = itemName;\n        _raw = raw;\n        _message = XPScriptMimeMessage.Parse(raw);",
            "        _document = document;\n        _itemName = itemName;\n        _raw = raw;\n        _message = XPScriptMimeMessage.Parse(raw);\n        _mimeDirectoryOwner = document.GetMimeDirectoryOwner();\n        _nativeEntity = _mimeDirectoryOwner.RootEntity;",
            "mime-native-open");

        source = ReplaceRequired(source,
            "    private void EnsureEntityAlive() { EnsureAlive(); _ = _document.NativeHandle; }",
            "    private void EnsureEntityAlive()\n    {\n        EnsureAlive();\n        _ = _document.NativeHandle;\n        if (!_mimeDirectoryOwner.IsAlive)\n            throw new System.ObjectDisposedException(nameof(XPScriptNotesMIMEEntity), \"MIME entities were closed for the parent document.\");\n        if (_nativeEntity == 0)\n            throw new System.ObjectDisposedException(nameof(XPScriptNotesMIMEEntity));\n    }",
            "mime-native-lifetime-guard");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string label)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to inject {label}.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
