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
        if (ResolveNativeHeaderSymbol(name) < 0) return null;
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
            if (symbol < 0) throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
            var value = _mimeDirectoryOwner.EntityHeader(_nativeEntity, symbol);
            if (value is null) throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
            return new XPScriptNotesMimeHeaderValue(name, value);
        }

        // Mutation-created header wrappers retain their serialized child-header index.
        // Keep that path for SetHeaderVal/AddValText/Remove before a reopen.
        var child = ReadCurrentDirectChild("NotesMIMEHeader");
        var headers = ParseEntityHeaders(child, FindRootBodyOffset(child));
        if (index < 0 || index >= headers.Count) throw new XPScriptRuntimeException(5, "MIME header index is no longer valid.");
        return headers[index];
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
