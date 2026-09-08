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
            "        _document = document;\n        _itemName = itemName;\n        _raw = raw;\n        _message = XPScriptMimeMessage.Parse(raw);\n        if (!string.Equals(itemName, \"Body\", System.StringComparison.OrdinalIgnoreCase))\n            throw new System.NotSupportedException(\"Native NotesMIMEEntity directory access is currently supported for the Body item only; MIMEOpenDirectory is note-level and does not accept an item name.\");\n        _mimeDirectoryOwner = document.GetMimeDirectoryOwner();\n        _nativeEntity = _mimeDirectoryOwner.RootEntity;",
            "mime-native-open");

        source = ReplaceRequired(source,
            "    internal static XPScriptNotesMIMEEntity Open(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName)\n        => new(session, document, itemName, session.Api.ReadMimeStream(document.NativeHandle, itemName));",
            "    internal static XPScriptNotesMIMEEntity Open(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName)\n        => new(session, document, itemName, session.Api.ReadMimeStream(document.NativeHandle, itemName));\n\n    private XPScriptNotesMIMEEntity(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName, byte[] raw, XPScriptNotesMimeDirectoryOwner owner, nint nativeEntity)\n        : base(session)\n    {\n        _document = document;\n        _itemName = itemName;\n        _raw = raw;\n        _message = XPScriptMimeMessage.Parse(raw);\n        _mimeDirectoryOwner = owner;\n        _nativeEntity = nativeEntity;\n    }\n\n    private XPScriptNotesMIMEEntity? WrapNativeEntity(nint nativeEntity)\n    {\n        EnsureEntityAlive();\n        if (nativeEntity == 0) return null;\n        return new XPScriptNotesMIMEEntity(Session, _document, _itemName, _raw, _mimeDirectoryOwner, nativeEntity);\n    }",
            "mime-native-wrapper-constructor");

        source = ReplaceRequired(source,
            "    public string BoundaryStart { get { EnsureEntityAlive(); return _message.Boundary.Length == 0 ? \"\" : \"--\" + _message.Boundary; } }\n    public string BoundaryEnd { get { EnsureEntityAlive(); return _message.Boundary.Length == 0 ? \"\" : \"--\" + _message.Boundary + \"--\"; } }\n    public string Charset { get { EnsureEntityAlive(); return _message.Charset; } }",
            "    public string BoundaryStart { get { EnsureEntityAlive(); var boundary = _mimeDirectoryOwner.TypeParam(_nativeEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY); return boundary.Length == 0 ? \"\" : \"--\" + boundary; } }\n    public string BoundaryEnd { get { EnsureEntityAlive(); var boundary = _mimeDirectoryOwner.TypeParam(_nativeEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY); return boundary.Length == 0 ? \"\" : \"--\" + boundary + \"--\"; } }\n    public string Charset { get { EnsureEntityAlive(); return _mimeDirectoryOwner.TypeParam(_nativeEntity, XPScriptNotesConst.MIME_SYMBOL_CHARSET); } }",
            "mime-native-type-parameters");

        source = ReplaceRequired(source,
            "    public string ContentType { get { EnsureEntityAlive(); return _message.ContentType; } }\n    public string ContentSubType { get { EnsureEntityAlive(); return _message.ContentSubType; } }",
            "    public string ContentType { get { EnsureEntityAlive(); return MimeSymbolText(_mimeDirectoryOwner.ContentTypeSymbol(_nativeEntity)); } }\n    public string ContentSubType { get { EnsureEntityAlive(); return MimeSymbolText(_mimeDirectoryOwner.ContentSubtypeSymbol(_nativeEntity)); } }",
            "mime-native-content-properties");

        source = ReplaceRequired(source,
            "    public XPScriptNotesDocument Parent { get { EnsureEntityAlive(); return _document; } }",
            "    public XPScriptNotesDocument Parent { get { EnsureEntityAlive(); return _document; } }\n\n    public XPScriptNotesMIMEEntity? GetFirstChildEntity()\n    {\n        EnsureEntityAlive();\n        return WrapNativeEntity(_mimeDirectoryOwner.FirstSubpart(_nativeEntity));\n    }\n\n    public XPScriptNotesMIMEEntity? GetParentEntity()\n    {\n        EnsureEntityAlive();\n        return WrapNativeEntity(_mimeDirectoryOwner.Parent(_nativeEntity));\n    }\n\n    public XPScriptNotesMIMEEntity? GetNextSibling()\n    {\n        EnsureEntityAlive();\n        return WrapNativeEntity(_mimeDirectoryOwner.NextSibling(_nativeEntity));\n    }\n\n    public XPScriptNotesMIMEEntity? GetPrevSibling()\n    {\n        EnsureEntityAlive();\n        return WrapNativeEntity(_mimeDirectoryOwner.PrevSibling(_nativeEntity));\n    }\n\n    public XPScriptNotesMIMEEntity? GetNextEntity()\n    {\n        EnsureEntityAlive();\n        return WrapNativeEntity(_mimeDirectoryOwner.IterateNext(_mimeDirectoryOwner.RootEntity, _nativeEntity));\n    }\n\n    public XPScriptNotesMIMEEntity? GetNextEntity(object? searchValue)\n    {\n        EnsureEntityAlive();\n        var search = XPScriptRuntime.CInt(searchValue);\n        if (search != XPScriptNotesConst.SEARCH_DEPTH)\n            throw new System.NotSupportedException(\"NotesMIMEEntity.GetNextEntity currently supports SEARCH_DEPTH only; Domino MIMEIterateNext is depth-first.\");\n        return GetNextEntity();\n    }",
            "mime-native-navigation");

        source = ReplaceRequired(source,
            "    private void EnsureEntityAlive() { EnsureAlive(); _ = _document.NativeHandle; }",
            "    private static string MimeSymbolText(int symbol) => symbol switch\n    {\n        XPScriptNotesConst.MIME_SYMBOL_UNKNOWN => \"\",\n        XPScriptNotesConst.MIME_SYMBOL_TEXT => \"text\",\n        XPScriptNotesConst.MIME_SYMBOL_MULTIPART => \"multipart\",\n        XPScriptNotesConst.MIME_SYMBOL_MESSAGE => \"message\",\n        XPScriptNotesConst.MIME_SYMBOL_APPLICATION => \"application\",\n        XPScriptNotesConst.MIME_SYMBOL_IMAGE => \"image\",\n        XPScriptNotesConst.MIME_SYMBOL_AUDIO => \"audio\",\n        XPScriptNotesConst.MIME_SYMBOL_VIDEO => \"video\",\n        XPScriptNotesConst.MIME_SYMBOL_PLAIN => \"plain\",\n        XPScriptNotesConst.MIME_SYMBOL_OCTET_STREAM => \"octet-stream\",\n        XPScriptNotesConst.MIME_SYMBOL_HTML => \"html\",\n        XPScriptNotesConst.MIME_SYMBOL_ALTERNATIVE => \"alternative\",\n        XPScriptNotesConst.MIME_SYMBOL_MIXED => \"mixed\",\n        XPScriptNotesConst.MIME_SYMBOL_7BIT => \"7bit\",\n        XPScriptNotesConst.MIME_SYMBOL_8BIT => \"8bit\",\n        XPScriptNotesConst.MIME_SYMBOL_QUOTED_PRINTABLE => \"quoted-printable\",\n        XPScriptNotesConst.MIME_SYMBOL_BASE64 => \"base64\",\n        XPScriptNotesConst.MIME_SYMBOL_BINARY => \"binary\",\n        _ => throw new System.NotSupportedException(\"Unsupported Domino MIME symbol: \" + symbol)\n    };\n\n    private void EnsureEntityAlive()\n    {\n        EnsureAlive();\n        _ = _document.NativeHandle;\n        if (!_mimeDirectoryOwner.IsAlive)\n            throw new System.ObjectDisposedException(nameof(XPScriptNotesMIMEEntity), \"MIME entities were closed for the parent document.\");\n        if (_nativeEntity == 0)\n            throw new System.ObjectDisposedException(nameof(XPScriptNotesMIMEEntity));\n    }",
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
