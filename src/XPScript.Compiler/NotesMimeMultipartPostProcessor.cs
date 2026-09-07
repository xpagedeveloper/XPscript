namespace XPScript.Compiler;

internal static class NotesMimeMultipartPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    private XPScriptMimeMessage _message;\n\n    private XPScriptNotesMIMEEntity(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName, byte[] raw)\n        : base(session)\n    {\n        _document = document;\n        _itemName = itemName;\n        _raw = raw;\n        _message = XPScriptMimeMessage.Parse(raw);\n    }\n\n    internal static XPScriptNotesMIMEEntity Open(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName)\n        => new(session, document, itemName, session.Api.ReadMimeStream(document.NativeHandle, itemName));",
            "    private XPScriptMimeMessage _message;\n    private readonly int[] _mimePath;\n\n    private XPScriptNotesMIMEEntity(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName, byte[] raw, int[]? mimePath = null)\n        : base(session)\n    {\n        _document = document;\n        _itemName = itemName;\n        _raw = raw;\n        _mimePath = mimePath ?? [];\n        _message = XPScriptMimeMultipartTree.ParseAt(raw, _mimePath);\n    }\n\n    internal static XPScriptNotesMIMEEntity Open(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName)\n        => new(session, document, itemName, session.Api.ReadMimeStream(document.NativeHandle, itemName));\n\n    private XPScriptNotesMIMEEntity OpenAt(int[] path) => new(Session, _document, _itemName, _raw, path);",
            "multipart-constructor");

        source = ReplaceRequired(source,
            "    public XPScriptNotesMIMEHeader? GetNthHeader(object? nameValue) => GetNthHeader(nameValue, 1);",
            NavigationSurface + "\n\n    public XPScriptNotesMIMEHeader? GetNthHeader(object? nameValue) => GetNthHeader(nameValue, 1);",
            "multipart-navigation");

        source = ReplaceRequired(source,
            "    public void Remove()\n    {\n        EnsureEntityAlive();\n        _document.RemoveItem(_itemName);\n        Recycle();\n    }",
            "    public void Remove()\n    {\n        EnsureEntityAlive();\n        if (_mimePath.Length == 0) _document.RemoveItem(_itemName);\n        else { _raw = XPScriptMimeMultipartTree.RemoveAt(_raw, _mimePath); Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw); }\n        Recycle();\n    }",
            "multipart-remove");

        source = ReplaceRequired(source,
            "        _raw = _message.Serialize();\n        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);\n        _message = XPScriptMimeMessage.Parse(_raw);",
            "        _raw = _mimePath.Length == 0 ? _message.Serialize() : XPScriptMimeMultipartTree.ReplaceAt(_raw, _mimePath, _message.Serialize());\n        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);\n        _message = XPScriptMimeMultipartTree.ParseAt(_raw, _mimePath);",
            "multipart-commit");

        return source + "\n\n" + Runtime;
    }

    private const string NavigationSurface = """
    public XPScriptNotesMIMEEntity? GetFirstChildEntity()
    {
        EnsureEntityAlive();
        return XPScriptMimeMultipartTree.ChildCount(_raw, _mimePath) == 0 ? null : OpenAt([.. _mimePath, 0]);
    }

    public XPScriptNotesMIMEEntity? GetParentEntity()
    {
        EnsureEntityAlive();
        return _mimePath.Length == 0 ? null : OpenAt(_mimePath[..^1]);
    }

    public XPScriptNotesMIMEEntity? GetNextSibling()
    {
        EnsureEntityAlive();
        if (_mimePath.Length == 0) return null;
        var parent = _mimePath[..^1]; var next = _mimePath[^1] + 1;
        return next < XPScriptMimeMultipartTree.ChildCount(_raw, parent) ? OpenAt([.. parent, next]) : null;
    }

    public XPScriptNotesMIMEEntity? GetPrevSibling()
    {
        EnsureEntityAlive();
        if (_mimePath.Length == 0 || _mimePath[^1] == 0) return null;
        return OpenAt([.. _mimePath[..^1], _mimePath[^1] - 1]);
    }

    public XPScriptNotesMIMEEntity? GetNextEntity() => GetNextEntity(1723);
    public XPScriptNotesMIMEEntity? GetNextEntity(object? searchValue)
    {
        EnsureEntityAlive();
        var breadth = XPScriptRuntime.CInt(searchValue) == 1724;
        if (!breadth) { var child = GetFirstChildEntity(); if (child is not null) return child; }
        var sibling = GetNextSibling(); if (sibling is not null) return sibling;
        if (breadth) { var child = GetFirstChildEntity(); if (child is not null) return child; }
        var path = _mimePath;
        while (path.Length > 0) { var parent = path[..^1]; var next = path[^1] + 1; if (next < XPScriptMimeMultipartTree.ChildCount(_raw, parent)) return OpenAt([.. parent, next]); path = parent; }
        return null;
    }

    public XPScriptNotesMIMEEntity? GetPrevEntity() => GetPrevEntity(1723);
    public XPScriptNotesMIMEEntity? GetPrevEntity(object? searchValue)
    {
        EnsureEntityAlive();
        var prev = GetPrevSibling();
        if (prev is null) return GetParentEntity();
        if (XPScriptRuntime.CInt(searchValue) == 1724) return prev;
        while (true) { var count = XPScriptMimeMultipartTree.ChildCount(_raw, prev._mimePath); if (count == 0) return prev; prev = OpenAt([.. prev._mimePath, count - 1]); }
    }

    public XPScriptNotesMIMEEntity CreateChildEntity() => CreateChildEntity(null);
    public XPScriptNotesMIMEEntity CreateChildEntity(object? nextSiblingValue)
    {
        EnsureEntityAlive();
        int? before = null;
        if (nextSiblingValue is XPScriptNotesMIMEEntity sibling)
        {
            if (sibling._mimePath.Length != _mimePath.Length + 1 || !sibling._mimePath[..^1].SequenceEqual(_mimePath)) throw new XPScriptRuntimeException(5, "nextSibling is not a child of this MIME entity.");
            before = sibling._mimePath[^1];
        }
        _raw = XPScriptMimeMultipartTree.InsertChild(_raw, _mimePath, before, out var index);
        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);
        _message = XPScriptMimeMultipartTree.ParseAt(_raw, _mimePath);
        return OpenAt([.. _mimePath, index]);
    }

    public XPScriptNotesMIMEEntity CreateParentEntity()
    {
        EnsureEntityAlive();
        _raw = XPScriptMimeMultipartTree.WrapAt(_raw, _mimePath);
        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);
        var parentPath = _mimePath;
        return OpenAt(parentPath);
    }
""";

    private const string Runtime = """
internal static class XPScriptMimeMultipartTree
{
    internal static XPScriptMimeMessage ParseAt(byte[] root, int[] path) => XPScriptMimeMessage.Parse(GetAt(root, path));
    internal static int ChildCount(byte[] root, int[] path) => SplitChildren(GetAt(root, path)).Count;

    internal static byte[] ReplaceAt(byte[] root, int[] path, byte[] replacement)
    {
        if (path.Length == 0) return replacement;
        var parentPath = path[..^1]; var parentRaw = GetAt(root, parentPath); var parent = XPScriptMimeMessage.Parse(parentRaw); var children = SplitChildren(parentRaw);
        if (path[^1] < 0 || path[^1] >= children.Count) throw new XPScriptRuntimeException(5, "Invalid MIME entity path.");
        children[path[^1]] = replacement; parent.Body = BuildBody(parent, children); return ReplaceAt(root, parentPath, parent.Serialize());
    }

    internal static byte[] RemoveAt(byte[] root, int[] path)
    {
        if (path.Length == 0) return [];
        var parentPath = path[..^1]; var parentRaw = GetAt(root, parentPath); var parent = XPScriptMimeMessage.Parse(parentRaw); var children = SplitChildren(parentRaw);
        if (path[^1] < 0 || path[^1] >= children.Count) throw new XPScriptRuntimeException(5, "Invalid MIME entity path.");
        children.RemoveAt(path[^1]); parent.Body = BuildBody(parent, children); return ReplaceAt(root, parentPath, parent.Serialize());
    }

    internal static byte[] InsertChild(byte[] root, int[] parentPath, int? before, out int index)
    {
        var parentRaw = GetAt(root, parentPath); var parent = XPScriptMimeMessage.Parse(parentRaw);
        var boundary = parent.Boundary;
        if (!parent.ContentType.Equals("multipart", StringComparison.OrdinalIgnoreCase) || boundary.Length == 0)
        {
            boundary = "XPScript_" + Guid.NewGuid().ToString("N"); parent.SetHeader("Content-Type", "multipart/mixed; boundary=\"" + boundary + "\""); parent.Body = [];
        }
        var children = SplitChildren(parent.Serialize()); index = before.HasValue ? Math.Clamp(before.Value, 0, children.Count) : children.Count;
        children.Insert(index, System.Text.Encoding.UTF8.GetBytes("Content-Type: text/plain; charset=UTF-8\r\nContent-Transfer-Encoding: 8bit\r\n\r\n"));
        parent.Body = BuildBody(parent, children); return ReplaceAt(root, parentPath, parent.Serialize());
    }

    internal static byte[] WrapAt(byte[] root, int[] path)
    {
        var current = GetAt(root, path); var parent = new XPScriptMimeMessage(); var boundary = "XPScript_" + Guid.NewGuid().ToString("N");
        parent.SetHeader("Content-Type", "multipart/mixed; boundary=\"" + boundary + "\""); parent.Body = BuildBody(parent, [current]);
        return ReplaceAt(root, path, parent.Serialize());
    }

    private static byte[] GetAt(byte[] root, int[] path)
    {
        var current = root;
        foreach (var index in path) { var children = SplitChildren(current); if (index < 0 || index >= children.Count) throw new XPScriptRuntimeException(5, "Invalid MIME entity path."); current = children[index]; }
        return current;
    }

    private static List<byte[]> SplitChildren(byte[] raw)
    {
        var message = XPScriptMimeMessage.Parse(raw); var boundary = message.Boundary;
        if (!message.ContentType.Equals("multipart", StringComparison.OrdinalIgnoreCase) || boundary.Length == 0) return [];
        var text = System.Text.Encoding.Latin1.GetString(message.Body); var marker = "--" + boundary; var parts = text.Split(marker, StringSplitOptions.None); var result = new List<byte[]>();
        for (var i = 1; i < parts.Length; i++) { var part = parts[i]; if (part.StartsWith("--", StringComparison.Ordinal)) break; part = part.TrimStart('\r', '\n').TrimEnd('\r', '\n'); if (part.Length > 0) result.Add(System.Text.Encoding.Latin1.GetBytes(part)); }
        return result;
    }

    private static byte[] BuildBody(XPScriptMimeMessage parent, List<byte[]> children)
    {
        var boundary = parent.Boundary; var output = new MemoryStream();
        if (parent.Preamble.Length > 0) { var pre = System.Text.Encoding.Latin1.GetBytes(parent.Preamble + "\r\n"); output.Write(pre); }
        foreach (var child in children) { var mark = System.Text.Encoding.ASCII.GetBytes("--" + boundary + "\r\n"); output.Write(mark); output.Write(child); var crlf = System.Text.Encoding.ASCII.GetBytes("\r\n"); output.Write(crlf); }
        var end = System.Text.Encoding.ASCII.GetBytes("--" + boundary + "--\r\n"); output.Write(end); return output.ToArray();
    }
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal)) throw new CompilerException("Unable to apply NotesMIMEEntity multipart surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
