namespace XPScript.Compiler;

internal static class NotesMailSenderSemanticsPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    private string _from = \"\";\n    private string _replyTo = \"\";",
            "    private string _replyTo = \"\";",
            "remove-writable-from-storage");

        source = ReplaceRequired(
            source,
            "    internal XPScriptNotesMail(XPScriptNotesSession session) : base(session)\n    {\n        _from = session.UserName;\n    }",
            "    internal XPScriptNotesMail(XPScriptNotesSession session) : base(session) { }",
            "remove-from-initializer");

        source = ReplaceRequired(
            source,
            "    public string From\n    {\n        get { EnsureAlive(); return _from; }\n        set { EnsureAlive(); _from = (value ?? \"\").Trim(); }\n    }",
            "    public string Sender\n    {\n        get { EnsureAlive(); return Session.UserName; }\n    }",
            "sender-read-only");

        source = ReplaceRequired(
            source,
            "    public string Principal\n    {\n        get { EnsureAlive(); return _principal; }\n        set { EnsureAlive(); _principal = (value ?? \"\").Trim(); }\n    }",
            "    public string OnBehalfOf\n    {\n        get { EnsureAlive(); return _principal; }\n        set { EnsureAlive(); _principal = (value ?? \"\").Trim(); }\n    }",
            "principal-as-on-behalf-of");

        source = ReplaceRequired(
            source,
            "        if (_blindCopyTo.Length > 0) document.ReplaceItemValue(\"BlindCopyTo\", ToItemValue(_blindCopyTo));\n        if (_from.Length > 0) document.ReplaceItemValue(\"From\", _from);\n        if (_replyTo.Length > 0) document.ReplaceItemValue(\"ReplyTo\", _replyTo);",
            "        if (_blindCopyTo.Length > 0) document.ReplaceItemValue(\"BlindCopyTo\", ToItemValue(_blindCopyTo));\n        if (_replyTo.Length > 0) document.ReplaceItemValue(\"ReplyTo\", _replyTo);",
            "router-controls-from");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesMail sender semantics (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
