namespace XPScript.Compiler;

internal static class NotesMimeEntityDataHeaderPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldLookup = """
        var child = ReadCurrentDirectChild("GetNthHeader");
        var headers = ParseEntityHeaders(child, FindRootBodyOffset(child));
        var found = 0;
        for (var i = 0; i < headers.Count; i++)
        {
            if (!headers[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            found++;
            if (found == occurrence) return new XPScriptNotesMIMEHeader(this, i);
        }
        return null;
""";

        const string newLookup = """
        if (occurrence != 1)
            throw new System.NotSupportedException("NotesMIMEEntity.GetNthHeader currently supports occurrence 1 for native direct-child headers.");
        var nativeIndex = NativeHeaderIndex(name);
        if (nativeIndex == 0)
            throw new System.NotSupportedException("NotesMIMEEntity.GetNthHeader currently supports Content-Type, Content-Transfer-Encoding and Content-Disposition on direct child entities.");
        return new XPScriptNotesMIMEHeader(this, nativeIndex);
""";

        if (!source.Contains(oldLookup, StringComparison.Ordinal))
            throw new CompilerException("Unable to replace direct-child MIME header lookup with MIMEEntityGetHeader.");
        source = source.Replace(oldLookup, newLookup, StringComparison.Ordinal);

        const string oldReader = """
    private XPScriptNotesMimeHeaderValue ReadEntityHeaderAt(int index)
    {
        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEHeader access is currently supported for direct child entities only.");
        var child = ReadCurrentDirectChild("NotesMIMEHeader");
        var headers = ParseEntityHeaders(child, FindRootBodyOffset(child));
        if (index < 0 || index >= headers.Count) throw new XPScriptRuntimeException(5, "MIME header index is no longer valid.");
        return headers[index];
    }
""";

        const string newReader = """
    private static int NativeHeaderIndex(string name)
    {
        if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) return -1001;
        if (name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) return -1002;
        if (name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)) return -1003;
        return 0;
    }

    private static string NativeHeaderName(int index) => index switch
    {
        -1001 => "Content-Type",
        -1002 => "Content-Transfer-Encoding",
        -1003 => "Content-Disposition",
        _ => throw new XPScriptRuntimeException(5, "Unsupported native MIME header index: " + index)
    };

    private int ResolveNativeHeaderSymbol(string name)
    {
        // HCL documents 125 MIMESYMBOL values (0..124) before MIME_SYMBOL_LAST.
        // Runtime diagnostics show that installed Domino versions can map the
        // content-header symbols differently from the published enum positions.
        // Stay inside the documented bounds and identify the requested header by
        // the semantics of the returned value instead of a numeric assumption.
        for (var symbol = 0; symbol <= 124; symbol++)
        {
            var value = _mimeDirectoryOwner.EntityHeader(_nativeEntity, symbol);
            if (value is null) continue;
            var trimmed = value.Trim();

            if (name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.Equals("7bit", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("8bit", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("base64", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("binary", StringComparison.OrdinalIgnoreCase))
                    return symbol;
                continue;
            }

            if (name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.Equals("attachment", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("inline", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("attachment;", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("inline;", StringComparison.OrdinalIgnoreCase))
                    return symbol;
                continue;
            }

            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('/'))
                return symbol;
        }
        return -1;
    }

    private XPScriptNotesMimeHeaderValue ReadEntityHeaderAt(int index)
    {
        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEHeader access is currently supported for direct child entities only.");

        if (index <= -1001)
        {
            var name = NativeHeaderName(index);
            var symbol = ResolveNativeHeaderSymbol(name);
            var value = symbol < 0 ? null : _mimeDirectoryOwner.EntityHeader(_nativeEntity, symbol);
            if (value is null || !MatchesNativeHeader(name, value))
            {
                var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
                var boundary = _mimeDirectoryOwner.TypeParam(_mimeDirectoryOwner.RootEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim();
                var contentTypeSymbol = ResolveNativeHeaderSymbol("Content-Type");
                var nativeType = contentTypeSymbol < 0 ? "" : _mimeDirectoryOwner.EntityHeader(_nativeEntity, contentTypeSymbol)?.Trim() ?? "";
                foreach (var candidate in SplitDirectChildren(raw, boundary))
                {
                    var candidateHeaders = ParseEntityHeaders(candidate, FindRootBodyOffset(candidate));
                    var candidateType = candidateHeaders.FirstOrDefault(h => h.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";
                    if (nativeType.Length > 0 && !candidateType.StartsWith(nativeType.Split(';')[0].Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var header in candidateHeaders)
                        if (header.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && MatchesNativeHeader(name, header.Value))
                            return header;
                }
                throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
            }
            return new XPScriptNotesMimeHeaderValue(name, value);
        }

        // Mutation-created header wrappers retain their serialized child-header index.
        // Keep that path for SetHeaderVal/AddValText/Remove before a reopen.
        var child = ReadCurrentDirectChild("NotesMIMEHeader");
        var headers = ParseEntityHeaders(child, FindRootBodyOffset(child));
        if (index < 0 || index >= headers.Count) throw new XPScriptRuntimeException(5, "MIME header index is no longer valid.");
        return headers[index];
    }

    private static bool MatchesNativeHeader(string name, string value)
    {
        var trimmed = value.Trim();
        if (name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            return trimmed.Equals("7bit", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("8bit", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("base64", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("binary", StringComparison.OrdinalIgnoreCase);
        if (name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase))
            return trimmed.Equals("attachment", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("inline", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("attachment;", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("inline;", StringComparison.OrdinalIgnoreCase);
        return trimmed.Contains('/');
    }
""";

        if (!source.Contains(oldReader, StringComparison.Ordinal))
            throw new CompilerException("Unable to replace child MIME header reader with MIMEEntityGetHeader.");
        source = source.Replace(oldReader, newReader, StringComparison.Ordinal);

        const string ownerMarker = """
    internal string TypeParam(nint entity, int symbol)
    {
        EnsureAlive();
        return _api!.GetMimeEntityTypeParam(entity, symbol);
    }
""";

        const string ownerReplacement = """
    internal string TypeParam(nint entity, int symbol)
    {
        EnsureAlive();
        return _api!.GetMimeEntityTypeParam(entity, symbol);
    }

    internal string? EntityHeader(nint entity, int symbol)
    {
        EnsureAlive();
        return _api!.GetMimeEntityHeader(entity, symbol);
    }

    internal byte[] EntityHeaders(uint note, nint entity)
    {
        EnsureAlive();
        return _api!.GetMimeEntityHeaders(note, entity);
    }

""";

        if (!source.Contains(ownerMarker, StringComparison.Ordinal))
            throw new CompilerException("Unable to inject native MIME entity header owner bridge.");
        source = source.Replace(ownerMarker, ownerReplacement, StringComparison.Ordinal);

        const string nativeMarker = """
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int NSFNoteHasMIMEPartDelegate(nint note);
""";

        const string nativeReplacement = """
    internal string? GetMimeEntityHeader(nint entity, int symbol)
    {
        EnsureInitialized();
        var value = Resolve<MIMEEntityGetHeaderDelegate>("MIMEEntityGetHeader")(entity, symbol);
        if (value == 0) return null;
        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(value);
    }

    internal byte[] GetMimeEntityHeaders(uint note, nint entity)
    {
        EnsureInitialized();
        const uint chunkSize = 60000;
        // The public reference names these values but does not publish their
        // numeric assignments. Try the complete byte-sized ABI range and
        // accept only a buffer that actually contains a MIME header.
        for (ushort dataType = 0; dataType <= byte.MaxValue; dataType++)
        {
            using var output = new MemoryStream();
            uint offset = 0;
            while (true)
            {
                var status = Resolve<MIMEGetEntityDataDelegate>("MIMEGetEntityData")((long)note, entity, dataType, offset, chunkSize, out var dataHandle, out var dataLength);
                if (status == ErrMimeNoData || status != 0 || dataLength == 0) break;
                nint data = 0;
                try
                {
                    data = Resolve<MimeMemoryLockDelegate>("OSLockObject")(dataHandle);
                    if (data == 0) break;
                    var bytes = new byte[checked((int)dataLength)];
                    System.Runtime.InteropServices.Marshal.Copy(data, bytes, 0, bytes.Length);
                    output.Write(bytes);
                }
                finally
                {
                    if (data != 0) Resolve<MimeMemoryUnlockDelegate>("OSUnlockObject")(dataHandle);
                    if (dataHandle != 0) Resolve<MimeMemoryFreeDelegate>("OSMemFree")(dataHandle);
                }
                offset += dataLength;
                if (dataLength < chunkSize) break;
            }
            var candidate = output.ToArray();
            var text = System.Text.Encoding.Latin1.GetString(candidate);
            if (text.IndexOf("Content-Type:", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Content-Transfer-Encoding:", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Content-Disposition:", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }
        return [];
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEGetEntityDataDelegate(long note, nint entity, ushort dataType, uint offset, uint requestedLength, out long dataHandle, out uint dataLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate nint MimeMemoryLockDelegate(long handle);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void MimeMemoryUnlockDelegate(long handle);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MimeMemoryFreeDelegate(long handle);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate nint MIMEEntityGetHeaderDelegate(nint entity, int symbol);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int NSFNoteHasMIMEPartDelegate(nint note);
""";

        if (!source.Contains(nativeMarker, StringComparison.Ordinal))
            throw new CompilerException("Unable to inject MIMEEntityGetHeader native ABI.");
        return source.Replace(nativeMarker, nativeReplacement, StringComparison.Ordinal);
    }
}
