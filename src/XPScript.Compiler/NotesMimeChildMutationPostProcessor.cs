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

    public XPScriptNotesMIMEHeader CreateHeader(object? nameValue, object? valueValue)
    {
        var header = CreateEntityHeader(XPScriptRuntime.CStr(nameValue));
        header.SetHeaderVal(valueValue);
        return header;
    }

    public object?[] GetHeaders() => GetHeaders(null);

    public object?[] GetChildren()
    {
        EnsureEntityAlive();
        var result = new List<object?>();
        var child = WrapNativeEntity(_mimeDirectoryOwner.FirstSubpart(_nativeEntity));
        while (child is not null)
        {
            result.Add(child);
            child = child.GetNextSibling();
        }
        return result.ToArray();
    }

    public void AppendChildEntity(object? childValue)
    {
        EnsureEntityAlive();
        if (childValue is not XPScriptNotesMIMEEntity child)
            throw new XPScriptRuntimeException(13, "AppendChildEntity child must be a NotesMIMEEntity.");
        if (!object.ReferenceEquals(child._mimeDirectoryOwner, _mimeDirectoryOwner))
            throw new XPScriptRuntimeException(5, "AppendChildEntity child belongs to another MIME directory.");
        var parentPath = GetEntityPath();
        var childPath = child.GetEntityPath();
        if (childPath.Length != parentPath.Length + 1 || !childPath[..^1].SequenceEqual(parentPath))
            throw new XPScriptRuntimeException(5, "AppendChildEntity child must belong directly to this entity.");
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var parent = GetSerializedEntity(raw, parentPath);
        var bodyOffset = FindRootBodyOffset(parent);
        var headers = ParseEntityHeaders(parent, bodyOffset);
        var boundary = headers.First(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value.Split(';').Skip(1).First(p => p.TrimStart().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase)).Split('=', 2)[1].Trim().Trim('"');
        var children = SplitDirectChildren(parent, boundary);
        var moved = children[childPath[^1]];
        children.RemoveAt(childPath[^1]);
        children.Add(moved);
        RewriteMimeTree(ReplaceSerializedEntity(raw, parentPath, BuildMultipartRoot(headers, boundary, children)), parentPath);
    }

    public object?[] GetHeaders(object? nameValue)
    {
        EnsureEntityAlive();
        var name = nameValue is null ? "" : XPScriptRuntime.CStr(nameValue).Trim();
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var entity = _nativeEntity == _mimeDirectoryOwner.RootEntity ? raw : GetSerializedEntity(raw, GetEntityPath());
        var headers = ParseEntityHeaders(entity, FindRootBodyOffset(entity));
        return headers
            .Select((header, index) => new { header, index })
            .Where(item => name.Length == 0 || item.header.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(item => (object?)new XPScriptNotesMIMEHeader(this, item.index, item.header.Name))
            .ToArray();
    }

    public XPScriptNotesMIMEEntity CreateChildEntity(object? nextSiblingValue)
    {
        EnsureEntityAlive();
        if (_nativeEntity != _mimeDirectoryOwner.RootEntity)
            return CreateNestedChildEntity(nextSiblingValue);

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

    private XPScriptNotesMIMEEntity CreateNestedChildEntity(object? nextSiblingValue)
    {
        var path = GetEntityPath();
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var target = GetSerializedEntity(raw, path);
        var targetBodyOffset = FindRootBodyOffset(target);
        var targetHeaders = ParseEntityHeaders(target, targetBodyOffset);
        var contentType = targetHeaders.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        var boundary = contentType.Split(';').Skip(1).FirstOrDefault(p => p.TrimStart().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? "";
        if (boundary.Length == 0) throw new XPScriptRuntimeException(5, "CreateChildEntity requires a multipart target entity.");
        var children = SplitDirectChildren(target, boundary);
        var insertAt = children.Count;
        if (nextSiblingValue is XPScriptNotesMIMEEntity sibling)
        {
            var siblingPath = sibling.GetEntityPath();
            if (siblingPath.Length != path.Length || !siblingPath[..^1].SequenceEqual(path)) throw new XPScriptRuntimeException(5, "CreateChildEntity nextSibling must share the same parent.");
            insertAt = siblingPath[^1];
        }
        children.Insert(insertAt, System.Text.Encoding.Latin1.GetBytes("Content-Type: text/plain; charset=UTF-8\r\nContent-Transfer-Encoding: 8bit\r\n\r\n"));
        var updated = BuildMultipartRoot(targetHeaders, boundary, children);
        RewriteMimeTree(ReplaceSerializedEntity(raw, path, updated), path);
        return WrapNativeEntity(ResolvePath(path.Concat(new[] { insertAt }).ToArray()))!;
    }
    public void RemoveChildEntity(object? childValue)
    {
        EnsureEntityAlive();
        if (childValue is not XPScriptNotesMIMEEntity child)
            throw new XPScriptRuntimeException(13, "RemoveChildEntity child must be a NotesMIMEEntity.");
        var parentPath = GetEntityPath();
        var childPath = child.GetEntityPath();
        if (childPath.Length != parentPath.Length + 1 || !childPath[..^1].SequenceEqual(parentPath))
            throw new XPScriptRuntimeException(5, "RemoveChildEntity child is not a direct child of this entity.");
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var parent = GetSerializedEntity(raw, parentPath);
        var bodyOffset = FindRootBodyOffset(parent);
        var headers = ParseEntityHeaders(parent, bodyOffset);
        var boundary = headers.First(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)).Value.Split(';').Skip(1).First(p => p.TrimStart().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase)).Split('=', 2)[1].Trim().Trim('"');
        var children = SplitDirectChildren(parent, boundary);
        children.RemoveAt(childPath[^1]);
        RewriteMimeTree(ReplaceSerializedEntity(raw, parentPath, BuildMultipartRoot(headers, boundary, children)), parentPath);
    }

    public void RemoveHeaders(object? nameValue)
    {
        EnsureEntityAlive();
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "MIME header name is required.");
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var path = GetEntityPath();
        var entity = GetSerializedEntity(raw, path);
        var bodyOffset = FindRootBodyOffset(entity);
        var headers = ParseEntityHeaders(entity, bodyOffset);
        headers.RemoveAll(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        RewriteMimeTree(ReplaceSerializedEntity(raw, path, BuildEntity(headers, bodyOffset >= entity.Length ? [] : entity[bodyOffset..])), path);
    }
""";

    private const string ChildHelpers = """
    internal XPScriptNotesMimeHeaderValue HeaderAt(int index)
    {
        EnsureEntityAlive();
        return ReadEntityHeaderAt(index);
    }

    internal XPScriptNotesMimeHeaderValue HeaderAt(string name)
    {
        EnsureEntityAlive();
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var entity = _nativeEntity == _mimeDirectoryOwner.RootEntity ? raw : GetSerializedEntity(raw, GetEntityPath());
        var headers = ParseEntityHeaders(entity, FindRootBodyOffset(entity));
        var header = headers.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (header is null) throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
        return header;
    }

    internal void SetHeader(int index, string value)
    {
        EnsureEntityAlive();
        SetEntityHeader(index, value);
    }

    internal void SetHeader(string name, string value)
    {
        EnsureEntityAlive();
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var entity = _nativeEntity == _mimeDirectoryOwner.RootEntity ? raw : GetSerializedEntity(raw, GetEntityPath());
        var headers = ParseEntityHeaders(entity, FindRootBodyOffset(entity));
        var index = headers.FindIndex(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
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

        if (_mimeDirectoryOwner.Parent(_nativeEntity) != _mimeDirectoryOwner.RootEntity)
        {
            WriteNestedEntityContent(data, contentType, encoding, member);
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

    private void WriteNestedEntityContent(byte[] data, string contentType, int encoding, string member)
    {
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var path = GetEntityPath();
        var entity = GetSerializedEntity(raw, path);
        var bodyOffset = FindRootBodyOffset(entity);
        var headers = ParseEntityHeaders(entity, bodyOffset);
        contentType = NormalizeChildContentType(contentType);
        headers.RemoveAll(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) || h.Name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase));
        headers.Insert(0, new XPScriptNotesMimeHeaderValue("Content-Type", contentType));
        var transfer = "8bit";
        var body = data;
        if (encoding == 1726) { transfer = "quoted-printable"; body = EncodeRootQuotedPrintable(data); }
        else if (encoding == 1727) { transfer = "base64"; body = System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks)); }
        else if (encoding == 1730) transfer = "binary";
        headers.Insert(1, new XPScriptNotesMimeHeaderValue("Content-Transfer-Encoding", transfer));
        RewriteMimeTree(ReplaceSerializedEntity(raw, path, BuildEntity(headers, body)), path);
    }

    private int[] GetEntityPath()
    {
        var root = _mimeDirectoryOwner.RootEntity;
        var current = _nativeEntity;
        var parts = new List<int>();
        while (current != root)
        {
            var parent = _mimeDirectoryOwner.Parent(current);
            if (parent == 0) throw new XPScriptRuntimeException(5, "MIME entity is no longer present in the directory.");
            var sibling = _mimeDirectoryOwner.FirstSubpart(parent);
            var index = 0;
            while (sibling != current && sibling != 0) { sibling = _mimeDirectoryOwner.NextSibling(sibling); index++; }
            if (sibling == 0) throw new XPScriptRuntimeException(5, "MIME entity is no longer present in its parent.");
            parts.Insert(0, index);
            current = parent;
        }
        return parts.ToArray();
    }

    private static byte[] GetSerializedEntity(byte[] raw, int[] path)
    {
        if (path.Length == 0) return raw;
        var current = raw;
        for (var depth = 0; depth < path.Length; depth++)
        {
            var headers = ParseEntityHeaders(current, FindRootBodyOffset(current));
            var boundary = headers.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value.Split(';').Skip(1).FirstOrDefault(p => p.TrimStart().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? "";
            current = SplitDirectChildren(current, boundary)[path[depth]];
        }
        return current;
    }

    private static byte[] ReplaceSerializedEntity(byte[] raw, int[] path, byte[] replacement)
    {
        if (path.Length == 0) return replacement;
        var bodyOffset = FindRootBodyOffset(raw);
        var rootHeaders = ParseEntityHeaders(raw, bodyOffset);
        var contentType = rootHeaders.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        var boundary = contentType.Split(';').Skip(1).FirstOrDefault(p => p.TrimStart().StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2).ElementAtOrDefault(1)?.Trim().Trim('"') ?? "";
        var children = SplitDirectChildren(raw, boundary);
        children[path[0]] = ReplaceSerializedEntity(children[path[0]], path[1..], replacement);
        return BuildMultipartRoot(rootHeaders, boundary, children);
    }

    private XPScriptNotesMIMEHeader CreateEntityHeader(string name)
    {
        EnsureEntityAlive();
        name = name.Trim();
        if (name.Length == 0 || name.Contains(':') || name.Contains('\r') || name.Contains('\n'))
            throw new XPScriptRuntimeException(5, "Invalid MIME header name.");

        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEEntity.CreateHeader is currently supported for direct child entities only.");

        if (_mimeDirectoryOwner.Parent(_nativeEntity) != _mimeDirectoryOwner.RootEntity)
            return CreateNestedEntityHeader(name);

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
        var nativeIndex = name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ? -1001
            : name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ? -1002
            : name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase) ? -1003
            : headers.Count - 1;
        return new XPScriptNotesMIMEHeader(this, nativeIndex, nativeIndex >= 0 ? name : null);
    }

    private XPScriptNotesMIMEHeader CreateNestedEntityHeader(string name)
    {
        var path = GetEntityPath();
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var entity = GetSerializedEntity(raw, path);
        var bodyOffset = FindRootBodyOffset(entity);
        var headers = ParseEntityHeaders(entity, bodyOffset);
        headers.Add(new XPScriptNotesMimeHeaderValue(name, ""));
        var replacement = BuildEntity(headers, bodyOffset >= entity.Length ? [] : entity[bodyOffset..]);
        RewriteMimeTree(ReplaceSerializedEntity(raw, path, replacement), path);
        var nativeIndex = name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ? -1001
            : name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ? -1002
            : name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase) ? -1003
            : headers.Count - 1;
        return new XPScriptNotesMIMEHeader(this, nativeIndex, nativeIndex >= 0 ? name : null);
    }

    private XPScriptNotesMimeHeaderValue ReadEntityHeaderAt(int index)
    {
        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEHeader access is currently supported for direct child entities only.");
        var child = _mimeDirectoryOwner.Parent(_nativeEntity) == _mimeDirectoryOwner.RootEntity
            ? ReadCurrentDirectChild("NotesMIMEHeader")
            : GetSerializedEntity(Session.Api.ReadMimeStream(_document.NativeHandle, _itemName), GetEntityPath());
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
        if (_mimeDirectoryOwner.Parent(_nativeEntity) != _mimeDirectoryOwner.RootEntity)
        {
            RewriteNestedEntityHeader(index, value, remove);
            return;
        }
        var childIndex = GetDirectChildIndex("NotesMIMEHeader");
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var boundary = _mimeDirectoryOwner.TypeParam(_mimeDirectoryOwner.RootEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim();
        var rootHeaders = ParseEntityHeaders(raw, FindRootBodyOffset(raw));
        var children = SplitDirectChildren(raw, boundary);
        var child = children[childIndex];
        var bodyOffset = FindRootBodyOffset(child);
        var headers = ParseEntityHeaders(child, bodyOffset);
        if (index <= -1001)
        {
            var nativeName = index switch
            {
                -1001 => "Content-Type",
                -1002 => "Content-Transfer-Encoding",
                -1003 => "Content-Disposition",
                _ => throw new XPScriptRuntimeException(5, "Unsupported native MIME header index.")
            };
            index = headers.FindIndex(h => h.Name.Equals(nativeName, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
        }
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

    private void RewriteNestedEntityHeader(int index, string value, bool remove)
    {
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var path = GetEntityPath();
        var entity = GetSerializedEntity(raw, path);
        var bodyOffset = FindRootBodyOffset(entity);
        var headers = ParseEntityHeaders(entity, bodyOffset);
        if (index <= -1001)
        {
            var name = index switch { -1001 => "Content-Type", -1002 => "Content-Transfer-Encoding", -1003 => "Content-Disposition", _ => throw new XPScriptRuntimeException(5, "Unsupported native MIME header index.") };
            index = headers.FindIndex(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        if (index < 0 || index >= headers.Count) throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
        if (remove) headers.RemoveAt(index); else headers[index].Value = value;
        RewriteMimeTree(ReplaceSerializedEntity(raw, path, BuildEntity(headers, bodyOffset >= entity.Length ? [] : entity[bodyOffset..])), path);
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
        => RewriteMimeTree(raw, mutatingChildIndex < 0 ? [] : new[] { mutatingChildIndex });

    private void RewriteMimeTree(byte[] raw, int[] mutatingPath)
    {
        _document.InvalidateMimeDirectory();
        Session.Api.WriteMimeStream(_document.NativeDatabaseHandle, _document.NativeHandle, _itemName, raw);
        _mimeDirectoryOwner = _document.GetMimeDirectoryOwner();
        _nativeEntity = ResolvePath(mutatingPath);
    }

    private nint ResolvePath(int[] path)
    {
        var entity = _mimeDirectoryOwner.RootEntity;
        foreach (var index in path)
        {
            entity = _mimeDirectoryOwner.FirstSubpart(entity);
            for (var i = 0; i < index && entity != 0; i++) entity = _mimeDirectoryOwner.NextSibling(entity);
            if (entity == 0) throw new XPScriptRuntimeException(5, "Unable to rebind the mutated MIME entity.");
        }
        return entity;
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
    private readonly string? _name;

    internal XPScriptNotesMIMEHeader(XPScriptNotesMIMEEntity entity, int index, string? name = null) : base(entity.Parent.SessionForItem)
    {
        _entity = entity;
        _index = index;
        _name = name;
    }

    public XPScriptNotesMIMEEntity Parent { get { EnsureAlive(); return _entity; } }
    public string HeaderName { get { EnsureAlive(); return (_name is null ? _entity.HeaderAt(_index) : _entity.HeaderAt(_name)).Name; } }
    public string GetHeaderVal() { EnsureAlive(); return (_name is null ? _entity.HeaderAt(_index) : _entity.HeaderAt(_name)).Value; }
    public string GetHeaderValAndParams() { EnsureAlive(); return (_name is null ? _entity.HeaderAt(_index) : _entity.HeaderAt(_name)).Value; }
    public string GetParamVal(object? nameValue)
    {
        EnsureAlive();
        var parameterName = XPScriptRuntime.CStr(nameValue).Trim();
        if (parameterName.Length == 0) return "";
        foreach (var part in (_name is null ? _entity.HeaderAt(_index) : _entity.HeaderAt(_name)).Value.Split(';').Skip(1))
        {
            var equals = part.IndexOf('=');
            if (equals <= 0 || !part[..equals].Trim().Equals(parameterName, StringComparison.OrdinalIgnoreCase)) continue;
            return part[(equals + 1)..].Trim().Trim('"');
        }
        return "";
    }
    public void SetHeaderVal(object? value) { EnsureAlive(); if (_name is null) _entity.SetHeader(_index, XPScriptRuntime.CStr(value)); else _entity.SetHeader(_name, XPScriptRuntime.CStr(value)); }
    public void SetHeaderValAndParams(object? value) { SetHeaderVal(value); }
    public void AddValText(object? value) { EnsureAlive(); _entity.SetHeader(_index, _entity.HeaderAt(_index).Value + XPScriptRuntime.CStr(value)); }
    public void SetParamVal(object? nameValue, object? value)
    {
        EnsureAlive();
        var parameterName = XPScriptRuntime.CStr(nameValue).Trim();
        if (parameterName.Length == 0 || parameterName.Contains('=') || parameterName.Contains(';')) throw new XPScriptRuntimeException(5, "Invalid MIME parameter name.");
        var parameterValue = XPScriptRuntime.CStr(value).Replace("\"", "\\\"", StringComparison.Ordinal);
        var header = _entity.HeaderAt(_index);
        var parts = header.Value.Split(';').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();
        var replaced = false;
        for (var i = 1; i < parts.Count; i++)
        {
            var equals = parts[i].IndexOf('=');
            if (equals <= 0 || !parts[i][..equals].Trim().Equals(parameterName, StringComparison.OrdinalIgnoreCase)) continue;
            parts[i] = parameterName + "=\"" + parameterValue + "\"";
            replaced = true;
        }
        if (!replaced) parts.Add(parameterName + "=\"" + parameterValue + "\"");
        _entity.SetHeader(_index, string.Join("; ", parts));
    }
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
