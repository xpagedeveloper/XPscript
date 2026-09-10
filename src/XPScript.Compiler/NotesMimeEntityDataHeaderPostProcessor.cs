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
        var rawHeaders = _mimeDirectoryOwner.EntityHeaders(_document.NativeHandle, _nativeEntity);
        var headers = ParseEntityHeaders(rawHeaders, rawHeaders.Length);
        var found = 0;
        for (var i = 0; i < headers.Count; i++)
        {
            if (!headers[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            found++;
            if (found == occurrence) return new XPScriptNotesMIMEHeader(this, -(i + 1));
        }
        return null;
""";

        if (!source.Contains(oldLookup, StringComparison.Ordinal))
            throw new CompilerException("Unable to replace direct-child MIME header lookup with MIMEGetEntityData.");
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
    private XPScriptNotesMimeHeaderValue ReadEntityHeaderAt(int index)
    {
        if (_nativeEntity == _mimeDirectoryOwner.RootEntity)
            throw new System.NotSupportedException("NotesMIMEHeader access is currently supported for direct child entities only.");

        if (index < 0)
        {
            var rawHeaders = _mimeDirectoryOwner.EntityHeaders(_document.NativeHandle, _nativeEntity);
            var headers = ParseEntityHeaders(rawHeaders, rawHeaders.Length);
            var nativeIndex = -index - 1;
            if (nativeIndex < 0 || nativeIndex >= headers.Count)
                throw new XPScriptRuntimeException(5, "MIME header is no longer present on the entity.");
            return headers[nativeIndex];
        }

        // Mutation-created header wrappers retain their serialized child-header index.
        // Keep that path for SetHeaderVal/AddValText/Remove before a reopen.
        var child = ReadCurrentDirectChild("NotesMIMEHeader");
        var serializedHeaders = ParseEntityHeaders(child, FindRootBodyOffset(child));
        if (index >= serializedHeaders.Count) throw new XPScriptRuntimeException(5, "MIME header index is no longer valid.");
        return serializedHeaders[index];
    }
""";

        if (!source.Contains(oldReader, StringComparison.Ordinal))
            throw new CompilerException("Unable to replace child MIME header reader with MIMEGetEntityData.");
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
    internal byte[] GetMimeEntityHeaders(uint note, nint entity)
    {
        EnsureInitialized();

        // HCL documents MIME_ENTITY_DATA_* as flag arguments. Probe the four
        // single-bit flag values and accept only a result that actually contains
        // RFC822 MIME header fields. This avoids relying on undocumented numeric
        // constants while remaining bounded to the documented flag surface.
        foreach (ushort selector in new ushort[] { 1, 2, 4, 8 })
        {
            var candidate = ReadMimeEntityData(note, entity, selector);
            if (candidate.Length == 0) continue;
            var text = System.Text.Encoding.Latin1.GetString(candidate);
            if (text.IndexOf("Content-Type:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Content-Transfer-Encoding:", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Content-Disposition:", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        return [];
    }

    private byte[] ReadMimeEntityData(uint note, nint entity, ushort selector)
    {
        const uint chunkSize = 60000;
        using var output = new MemoryStream();
        uint offset = 0;

        while (true)
        {
            var status = Resolve<MIMEGetEntityDataDelegate>("MIMEGetEntityData")(
                note, entity, selector, offset, chunkSize, out var dataHandle, out var dataLength);
            if (status == ErrMimeNoData)
                break;
            if (status != 0)
            {
                if (dataHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(dataHandle);
                return [];
            }
            if (dataLength == 0)
            {
                if (dataHandle != 0)
                    Check(Resolve<OSMemFreeDelegate>("OSMemFree")(dataHandle), "OSMemFree(MIMEGetEntityData probe)");
                break;
            }

            nint data = 0;
            try
            {
                data = Resolve<OSLockObjectDelegate>("OSLockObject")(dataHandle);
                if (data == 0)
                    throw new XPScriptRuntimeException(5, "Unable to lock MIME entity data.");
                var bytes = new byte[checked((int)dataLength)];
                System.Runtime.InteropServices.Marshal.Copy(data, bytes, 0, bytes.Length);
                output.Write(bytes);
            }
            finally
            {
                if (data != 0) Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(dataHandle);
                if (dataHandle != 0)
                    Check(Resolve<OSMemFreeDelegate>("OSMemFree")(dataHandle), "OSMemFree(MIMEGetEntityData probe)");
            }

            offset += dataLength;
            if (dataLength < chunkSize) break;
        }

        return output.ToArray();
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEGetEntityDataDelegate(
        uint note,
        nint entity,
        ushort dataType,
        uint offset,
        uint requestedLength,
        out uint dataHandle,
        out uint dataLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate int NSFNoteHasMIMEPartDelegate(nint note);
""";

        if (!source.Contains(nativeMarker, StringComparison.Ordinal))
            throw new CompilerException("Unable to inject MIMEGetEntityData native ABI.");
        return source.Replace(nativeMarker, nativeReplacement, StringComparison.Ordinal);
    }
}
