namespace XPScript.Compiler;

internal static class NotesMimeChildMutationPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "        WriteRootContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue), \"SetContentFromText\");",
            "        WriteEntityContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue), \"SetContentFromText\");",
            "MIME entity SetContentFromText");

        source = ReplaceRequired(source,
            "        WriteRootContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue), \"SetContentFromBytes\");",
            "        WriteEntityContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue), \"SetContentFromBytes\");",
            "MIME entity SetContentFromBytes");

        source = ReplaceRequired(source,
            "    public XPScriptNotesMIMEHeader? GetNthHeader(object? nameValue) => GetNthHeader(nameValue, 1);",
            ChildSurface + "\n    public XPScriptNotesMIMEHeader? GetNthHeader(object? nameValue) => GetNthHeader(nameValue, 1);",
            "MIME child creation surface");

        source = ReplaceRequired(source,
            "        throw new System.NotSupportedException(\"NotesMIMEEntity.CreateHeader requires verified Domino MIME entity header mutation support; managed MIME header serialization is intentionally not used.\");",
            "        return CreateEntityHeader(XPScriptRuntime.CStr(nameValue));",
            "MIME child CreateHeader");

        source = ReplaceRequired(source,
            "    private static string MimeSymbolText(int symbol) => symbol switch",
            ChildHelpers + "\n    private static string MimeSymbolText(int symbol) => symbol switch",
            "MIME child mutation helpers");

        source = ReplaceRangeRequired(source,
            "internal sealed class XPScriptNotesMIMEHeader : XPScriptNotesObject",
            "internal sealed partial class XPScriptNotesNativeApi",
            HeaderRuntime + "\n\n",
            "MIME child header runtime");

        return source;
    }

    private const string ChildSurface = """
    public XPScriptNotesMIMEEntity CreateChildEntity() => CreateChildEntity(null);

    public XPScriptNotesMIMEEntity CreateChildEntity(object? nextSiblingValue)
    {
        EnsureRootEntity("CreateChildEntity");

        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var rootHeaders = ParseEntityHeaders(raw, FindRootBodyOffset(raw));
        var wasMultipart = ContentType.Equals("multipart", StringComparison.OrdinalIgnoreCase);
        var boundary = wasMultipart ? _mimeDirectoryOwner.TypeParam(_nativeEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim() : "";
        if (boundary.Length == 0) boundary = "----=_XPscript_" + Guid.NewGuid().ToString("N");

        var children = wasMultipart ? SplitDirectChildren(raw, boundary) : new List<byte[]>();
        var insertAt = children.Count;
        if (nextSiblingValue is not null)
        {
            if (nextSiblingValue is not XPScriptNotesMIMEEntity sibling)
                throw new XPScriptRuntimeException(13, "CreateChildEntity nextSibling must be a NotesMIMEEntity.");
            if (!object.ReferenceEquals(sibling._mimeDirectoryOwner, _mimeDirectoryOwner))
                throw new XPScriptRuntimeException(5, "CreateChildEntity nextSibling belongs to another MIME directory.");
            insertAt = sibling.GetDirectChildIndex("CreateChildEntity");
        }

        children.Insert(insertAt, System.Text.Encoding.Latin1.GetBytes(
            "Content-Type: text/plain; charset=UTF-8\r\n" +
            "Content-Transfer-Encoding: 8bit\r\n\r\n"));

        var multipart = BuildMultipartRoot(rootHeaders, boundary, children);
        RewriteMimeTree(multipart, -1);
        return WrapDirectChild(insertAt);
    }
""";

    private const string ChildHelpers = """
    internal XPScriptNotesMimeHeaderValue HeaderAt(int index)
    {
        EnsureEntityAlive();
        return ReadEntityHeaderAt(index);
    }

    internal void SetHeader(int index, string value)
    {
        EnsureEntityAlive();
        SetEntityHeader(index, value);
    }

    internal void RemoveHeader(int index)
    {
        EnsureEntityAlive();
        RemoveEntityHeader(index);
    }

    private void WriteEntityContent(byte[] data, string contentType, int encoding, string member)
    {
        EnsureEntityAlive();
        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
        {
            WriteRootContent(data, contentType, encoding, member);
            return;
        }

        var childIndex = GetDirectChildIndex(member);
        contentType = NormalizeChildContentType(contentType);

        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var boundary = _mimeDirectoryOwner.TypeParam(_mimeDirectoryOwner.RootEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim();
        if (boundary.Length == 0)
            throw new System.NotSupportedException("NotesMIMEEntity." + member + " child mutation requires a multipart root boundary.");

        var rootHeaders = ParseEntityHeaders(raw, FindRootBodyOffset(raw));
        var children = SplitDirectChildren(raw, boundary);
        if (childIndex < 0 || childIndex >= children.Count)
            throw new XPScriptRuntimeException(5, "MIME child index is no longer valid.");

        var existing = children[childIndex];
        var existingHeaders = ParseEntityHeaders(existing, FindRootBodyOffset(existing));
        var preserved = existingHeaders.Where(h =>
            !h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) &&
            !h.Name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase)).ToList();
        preserved.Insert(0, new XPScriptNotesMimeHeaderValue("Content-Type", contentType));

        var transfer = "8bit";
        byte[] body = data;
        switch (encoding)
        {
            case 1726:
                transfer = "quoted-printable";
                body = EncodeRootQuotedPrintable(data);
                break;
            case 1727:
                transfer = "base64";
                body = System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks));
                break;
            case 1730:
                transfer = "binary";
                break;
        }
        preserved.Insert(1, new XPScriptNotesMimeHeaderValue("Content-Transfer-Encoding", transfer));
        children[childIndex] = BuildEntity(preserved, body);

        RewriteMimeTree(BuildMultipartRoot(rootHeaders, boundary, children), childIndex);
    }

    private XPScriptNotesMIMEHeader CreateEntityHeader(string name)
    {
        EnsureEntityAlive();
        name = name.Trim();
        if (name.Length == 0 || name.Contains(':') || name.Contains('\r') || name.Contains('\n'))
            throw new XPScriptRuntimeException(5, "Invalid MIME header name.");

        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEEntity.CreateHeader is currently supported for direct child entities only.");

        var childIndex = GetDirectChildIndex("CreateHeader");
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var boundary = _mimeDirectoryOwner.TypeParam(_mimeDirectoryOwner.RootEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim();
        var rootHeaders = ParseEntityHeaders(raw, FindRootBodyOffset(raw));
        var children = SplitDirectChildren(raw, boundary);
        var child = children[childIndex];
        var bodyOffset = FindRootBodyOffset(child);
        var headers = ParseEntityHeaders(child, bodyOffset);
        headers.Add(new XPScriptNotesMimeHeaderValue(name, ""));
        children[childIndex] = BuildEntity(headers, bodyOffset >= child.Length ? [] : child[bodyOffset..]);
        RewriteMimeTree(BuildMultipartRoot(rootHeaders, boundary, children), childIndex);
        return new XPScriptNotesMIMEHeader(this, headers.Count - 1);
    }

    private XPScriptNotesMimeHeaderValue ReadEntityHeaderAt(int index)
    {
        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEHeader access is currently supported for direct child entities only.");
        var child = ReadCurrentDirectChild("NotesMIMEHeader");
        var headers = ParseEntityHeaders(child, FindRootBodyOffset(child));
        if (index < 0 || index >= headers.Count) throw new XPScriptRuntimeException(5, "MIME header index is no longer valid.");
        return headers[index];
    }

    private void SetEntityHeader(int index, string value)
    {
        RewriteEntityHeader(index, value, false);
    }

    private void RemoveEntityHeader(int index)
    {
        RewriteEntityHeader(index, "", true);
    }

    private void RewriteEntityHeader(int index, string value, bool remove)
    {
        if (value.Contains('\r') || value.Contains('\n'))
            throw new XPScriptRuntimeException(5, "Invalid MIME header value.");
        var childIndex = GetDirectChildIndex("NotesMIMEHeader");
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var boundary = _mimeDirectoryOwner.TypeParam(_mimeDirectoryOwner.RootEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim();
        var rootHeaders = ParseEntityHeaders(raw, FindRootBodyOffset(raw));
        var children = SplitDirectChildren(raw, boundary);
        var child = children[childIndex];
        var bodyOffset = FindRootBodyOffset(child);
        var headers = ParseEntityHeaders(child, bodyOffset);
        if (index < 0 || index >= headers.Count) throw new XPScriptRuntimeException(5, "MIME header index is no longer valid.");
        // Domino can reorder the documented MIME headers when the directory is
        // reopened. Preserve the header identity for disposition updates.
        if (!remove && (value.StartsWith("attachment", StringComparison.OrdinalIgnoreCase) || value.StartsWith("inline", StringComparison.OrdinalIgnoreCase)))
        {
            var dispositionIndex = headers.FindIndex(h => h.Name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase));
            if (dispositionIndex >= 0) index = dispositionIndex;
        }
        if (remove) headers.RemoveAt(index); else headers[index].Value = value;
        children[childIndex] = BuildEntity(headers, bodyOffset >= child.Length ? [] : child[bodyOffset..]);
        RewriteMimeTree(BuildMultipartRoot(rootHeaders, boundary, children), childIndex);
    }

    private byte[] ReadCurrentDirectChild(string member)
    {
        var childIndex = GetDirectChildIndex(member);
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var boundary = _mimeDirectoryOwner.TypeParam(_mimeDirectoryOwner.RootEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim();
        var children = SplitDirectChildren(raw, boundary);
        if (childIndex < 0 || childIndex >= children.Count) throw new XPScriptRuntimeException(5, "MIME child index is no longer valid.");
        return children[childIndex];
    }

    private int GetDirectChildIndex(string member)
    {
        EnsureEntityAlive();
        var root = _mimeDirectoryOwner.RootEntity;
        if (_nativeEntity == root)
            throw new System.NotSupportedException("NotesMIMEEntity." + member + " requires a child entity.");
        if (_mimeDirectoryOwner.Parent(_nativeEntity) != root)
            throw new System.NotSupportedException("NotesMIMEEntity." + member + " currently supports direct children of the root entity only.");

        var current = _mimeDirectoryOwner.FirstSubpart(root);
        var index = 0;
        while (current != 0)
        {
            if (current == _nativeEntity) return index;
            current = _mimeDirectoryOwner.NextSibling(current);
            index++;
        }
        throw new XPScriptRuntimeException(5, "MIME child is no longer present in the parent document.");
    }

    private XPScriptNotesMIMEEntity WrapDirectChild(int index)
    {
        var entity = _mimeDirectoryOwner.FirstSubpart(_mimeDirectoryOwner.RootEntity);
        for (var i = 0; i < index && entity != 0; i++) entity = _mimeDirectoryOwner.NextSibling(entity);
        if (entity == 0) throw new XPScriptRuntimeException(5, "Unable to reopen the created MIME child entity.");
        return WrapNativeEntity(entity)!;
    }

    private void RewriteMimeTree(byte[] raw, int mutatingChildIndex)
    {
        _document.InvalidateMimeDirectory();
        Session.Api.WriteMimeStream(_document.NativeDatabaseHandle, _document.NativeHandle, _itemName, raw);
        _mimeDirectoryOwner = _document.GetMimeDirectoryOwner();
        _nativeEntity = mutatingChildIndex < 0 ? _mimeDirectoryOwner.RootEntity : ResolveDirectChild(mutatingChildIndex);
    }

    private nint ResolveDirectChild(int index)
    {
        var entity = _mimeDirectoryOwner.FirstSubpart(_mimeDirectoryOwner.RootEntity);
        for (var i = 0; i < index && entity != 0; i++) entity = _mimeDirectoryOwner.NextSibling(entity);
        if (entity == 0) throw new XPScriptRuntimeException(5, "Unable to reopen the mutated MIME child entity.");
        return entity;
    }

    private static string NormalizeChildContentType(string contentType)
    {
        contentType = contentType.Trim();
        if (contentType.Length == 0) contentType = "application/octet-stream";
        if (contentType.Contains('\r') || contentType.Contains('\n')) throw new XPScriptRuntimeException(5, "Invalid MIME content type.");
        return contentType;
    }

    private static List<XPScriptNotesMimeHeaderValue> ParseEntityHeaders(byte[] raw, int bodyOffset)
    {
        var result = new List<XPScriptNotesMimeHeaderValue>();
        var headerLength = Math.Max(0, Math.Min(raw.Length, bodyOffset));
        var text = System.Text.Encoding.Latin1.GetString(raw, 0, headerLength)
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        XPScriptNotesMimeHeaderValue? current = null;
        foreach (var line in text.Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && current is not null)
            {
                current.Value += " " + line.Trim();
                continue;
            }
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            current = new XPScriptNotesMimeHeaderValue(line[..colon].Trim(), line[(colon + 1)..].Trim());
            result.Add(current);
        }
        return result;
    }

    private static List<byte[]> SplitDirectChildren(byte[] raw, string boundary)
    {
        if (boundary.Length == 0) throw new XPScriptRuntimeException(5, "Multipart MIME boundary is missing.");
        var result = new List<byte[]>();
        var bodyOffset = FindRootBodyOffset(raw);
        var body = bodyOffset >= raw.Length ? [] : raw[bodyOffset..];
        var marker = System.Text.Encoding.Latin1.GetBytes("--" + boundary);
        var pos = 0;
        while (true)
        {
            var start = IndexOfMimeBytes(body, marker, pos);
            if (start < 0) break;
            var afterMarker = start + marker.Length;
            if (afterMarker + 1 < body.Length && body[afterMarker] == '-' && body[afterMarker + 1] == '-') break;
            if (afterMarker + 1 < body.Length && body[afterMarker] == '\r' && body[afterMarker + 1] == '\n') afterMarker += 2;
            else if (afterMarker < body.Length && body[afterMarker] == '\n') afterMarker++;
            var next = IndexOfMimeBytes(body, marker, afterMarker);
            if (next < 0) break;
            var end = next;
            while (end > afterMarker && (body[end - 1] == '\r' || body[end - 1] == '\n')) end--;
            result.Add(body[afterMarker..end]);
            pos = next;
        }
        return result;
    }

    private static int IndexOfMimeBytes(byte[] haystack, byte[] needle, int start)
    {
        if (needle.Length == 0) return start;
        for (var i = Math.Max(0, start); i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++) if (haystack[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    private static byte[] BuildMultipartRoot(List<XPScriptNotesMimeHeaderValue> rootHeaders, string boundary, List<byte[]> children)
    {
        var headers = rootHeaders.Where(h =>
            !h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) &&
            !h.Name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase)).ToList();
        headers.Insert(0, new XPScriptNotesMimeHeaderValue("Content-Type", "multipart/mixed; boundary=\"" + boundary + "\""));

        using var output = new MemoryStream();
        var head = System.Text.Encoding.Latin1.GetBytes(string.Join("\r\n", headers.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n");
        output.Write(head);
        foreach (var child in children)
        {
            var marker = System.Text.Encoding.Latin1.GetBytes("--" + boundary + "\r\n");
            output.Write(marker);
            output.Write(child);
            output.Write(System.Text.Encoding.ASCII.GetBytes("\r\n"));
        }
        output.Write(System.Text.Encoding.Latin1.GetBytes("--" + boundary + "--\r\n"));
        return output.ToArray();
    }

    private static byte[] BuildEntity(List<XPScriptNotesMimeHeaderValue> headers, byte[] body)
    {
        var head = System.Text.Encoding.Latin1.GetBytes(string.Join("\r\n", headers.Select(h => h.Name + ": " + h.Value)) + "\r\n\r\n");
        var result = new byte[head.Length + body.Length];
        Buffer.BlockCopy(head, 0, result, 0, head.Length);
        Buffer.BlockCopy(body, 0, result, head.Length, body.Length);
        return result;
    }
""";

    private const string HeaderRuntime = """
internal sealed class XPScriptNotesMIMEHeader : XPScriptNotesObject
{
    private readonly XPScriptNotesMIMEEntity _entity;
    private int _index;

    internal XPScriptNotesMIMEHeader(XPScriptNotesMIMEEntity entity, int index) : base(entity.Parent.SessionForItem)
    {
        _entity = entity;
        _index = index;
    }

    public XPScriptNotesMIMEEntity Parent { get { EnsureAlive(); return _entity; } }
    public string HeaderName { get { EnsureAlive(); return _entity.HeaderAt(_index).Name; } }
    public string GetHeaderVal() { EnsureAlive(); return _entity.HeaderAt(_index).Value; }
    public string GetHeaderValAndParams() { EnsureAlive(); return _entity.HeaderAt(_index).Value; }
    public string GetParamVal(object? nameValue) { EnsureAlive(); throw new System.NotSupportedException("NotesMIMEHeader.GetParamVal is not yet supported for bounded child headers."); }
    public void SetHeaderVal(object? value) { EnsureAlive(); _entity.SetHeader(_index, XPScriptRuntime.CStr(value)); }
    public void SetHeaderValAndParams(object? value) { SetHeaderVal(value); }
    public void AddValText(object? value) { EnsureAlive(); _entity.SetHeader(_index, _entity.HeaderAt(_index).Value + XPScriptRuntime.CStr(value)); }
    public void SetParamVal(object? nameValue, object? value) { EnsureAlive(); throw new System.NotSupportedException("NotesMIMEHeader.SetParamVal is not yet supported for bounded child headers."); }
    public void Remove() { EnsureAlive(); _entity.RemoveHeader(_index); Recycle(); }
    protected override void ReleaseNative() { _index = -1; }
}

internal sealed class XPScriptNotesMimeHeaderValue
{
    internal XPScriptNotesMimeHeaderValue(string name, string value)
    {
        Name = name;
        Value = value;
    }

    internal string Name { get; }
    internal string Value { get; set; }
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string label)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to inject {label}.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static string ReplaceRangeRequired(string source, string startMarker, string endMarker, string replacement, string label)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0) throw new CompilerException($"Unable to locate start of {label}.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0) throw new CompilerException($"Unable to locate end of {label}.");
        return source[..start] + replacement + source[end..];
    }
}
