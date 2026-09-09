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
        var symbol = HeaderSymbol(name);
        if (symbol == 0)
            throw new System.NotSupportedException("NotesMIMEEntity.GetNthHeader currently supports Content-Type, Content-Transfer-Encoding and Content-Disposition on direct child entities.");
        var value = _mimeDirectoryOwner.EntityHeader(_nativeEntity, symbol);
        if (value is null) return null;
        return new XPScriptNotesMIMEHeader(this, -(symbol + 1));
""";

        if (!source.Contains(oldLookup, StringComparison.Ordinal))
            throw new CompilerException("Unable to replace direct-child MIME header lookup with MIMEEntityGetHeader.");
        source = source.Replace(oldLookup, newLookup, StringComparison.Ordinal);

        const string oldReader = """
    private XPScriptNotesMimeHeaderValue ReadEntityHeaderAt(int index)
    {
        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEHeader access is currently supported for direct child entities only.");
        var rawHeaders = _mimeDirectoryOwner.EntityHeaders(_document.NativeHandle, _nativeEntity);
        var headers = ParseEntityHeaders(rawHeaders, rawHeaders.Length);
        if (index < 0 || index >= headers.Count) throw new XPScriptRuntimeException(5, "MIME header index is no longer valid.");
        return headers[index];
    }
""";

        const string newReader = """
    private static int HeaderSymbol(string name)
    {
        if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) return 39;
        if (name.Equals("Content-Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) return 40;
        if (name.Equals("Content-Disposition", StringComparison.OrdinalIgnoreCase)) return 41;
        return 0;
    }

    private static string HeaderNameFromSymbol(int symbol) => symbol switch
    {
        39 => "Content-Type",
        40 => "Content-Transfer-Encoding",
        41 => "Content-Disposition",
        _ => throw new XPScriptRuntimeException(5, "Unsupported native MIME header symbol: " + symbol)
    };

    private XPScriptNotesMimeHeaderValue ReadEntityHeaderAt(int index)
    {
        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEHeader access is currently supported for direct child entities only.");

        if (index < 0)
        {
            var symbol = -index - 1;
            var value = _mimeDirectoryOwner.EntityHeader(_nativeEntity, symbol);
            if (value is null) throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
            return new XPScriptNotesMimeHeaderValue(HeaderNameFromSymbol(symbol), value);
        }

        // Mutation-created header wrappers retain their serialized child-header index.
        // Keep that path for SetHeaderVal/AddValText/Remove before a reopen.
        var child = ReadCurrentDirectChild("NotesMIMEHeader");
        var headers = ParseEntityHeaders(child, FindRootBodyOffset(child));
        if (index >= headers.Count) throw new XPScriptRuntimeException(5, "MIME header index is no longer valid.");
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
