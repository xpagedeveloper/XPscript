namespace XPScript.Compiler;

internal static class NotesMimeStreamAbiPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldIncludeHeaders = "const uint includeHeaders = 0x00000004u;";
        const string newIncludeHeaders = "const uint includeHeaders = 0x00000010u;";

        if (source.Contains(oldIncludeHeaders, StringComparison.Ordinal))
            source = source.Replace(oldIncludeHeaders, newIncludeHeaders, StringComparison.Ordinal);
        else if (!source.Contains(newIncludeHeaders, StringComparison.Ordinal))
            throw new CompilerException("Unable to normalize MIME_STREAM_MIME_INCLUDE_HEADERS.");

        const string oldWriteMimeStream = """
    internal void WriteMimeStream(nint note, string itemName, byte[] data)
    {
        EnsureInitialized();
        const uint openWrite = 0x00000002u;
        Check(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(note, 0, 0, openWrite, out var stream), "MIMEStreamOpen(write)");
        try
        {
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, data.Length));
            try
            {
                if (data.Length > 0) System.Runtime.InteropServices.Marshal.Copy(data, 0, buffer, data.Length);
                if (Resolve<MIMEStreamWriteDelegate>("MIMEStreamWrite")(buffer, checked((uint)data.Length), stream) != 0) throw new XPScriptRuntimeException(5, "MIMEStreamWrite failed.");
                using var name = ToLmbcs(itemName);
                Check(Resolve<MIMEStreamItemizeDelegate>("MIMEStreamItemize")(note, name.Pointer, checked((ushort)name.Length), 0, stream), "MIMEStreamItemize");
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
        }
        finally { Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream); }
    }
""";

        const string oldWriteMimeStreamWithItemizeBody = """
    internal void WriteMimeStream(nint note, string itemName, byte[] data)
    {
        EnsureInitialized();
        const uint openWrite = 0x00000002u;
        Check(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(note, 0, 0, openWrite, out var stream), "MIMEStreamOpen(write)");
        try
        {
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, data.Length));
            try
            {
                if (data.Length > 0) System.Runtime.InteropServices.Marshal.Copy(data, 0, buffer, data.Length);
                if (Resolve<MIMEStreamWriteDelegate>("MIMEStreamWrite")(buffer, checked((uint)data.Length), stream) != 0) throw new XPScriptRuntimeException(5, "MIMEStreamWrite failed.");
                using var name = ToLmbcs(itemName);
                const uint itemizeBody = 0x00000004u;
                Check(Resolve<MIMEStreamItemizeDelegate>("MIMEStreamItemize")(note, name.Pointer, checked((ushort)name.Length), itemizeBody, stream), "MIMEStreamItemize");
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
        }
        finally { Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream); }
    }
""";

        const string nsfMimePartWriter = """
    internal void WriteMimeStream(nint note, string itemName, byte[] data)
    {
        EnsureInitialized();
        // HCL nsfmime.h: MIME_PART_BODY = 2, MIME_PART_HAS_HEADERS = 2.
        // Closing with bUpdate=TRUE flushes the context and creates TYPE_MIME_PART.
        const ushort mimePartBody = 2;
        const uint mimePartHasHeaders = 2u;
        using var name = ToLmbcs(itemName);
        Check(Resolve<NSFMimePartCreateStreamDelegate>("NSFMimePartCreateStream")(
            note, name.Pointer, checked((ushort)name.Length), mimePartBody, mimePartHasHeaders, out var context),
            "NSFMimePartCreateStream");

        var update = false;
        try
        {
            var offset = 0;
            while (offset < data.Length)
            {
                var count = Math.Min(ushort.MaxValue, data.Length - offset);
                var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(count);
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(data, offset, buffer, count);
                    Check(Resolve<NSFMimePartAppendStreamDelegate>("NSFMimePartAppendStream")(context, buffer, checked((ushort)count)), "NSFMimePartAppendStream");
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                }
                offset += count;
            }
            update = true;
        }
        finally
        {
            Check(Resolve<NSFMimePartCloseStreamDelegate>("NSFMimePartCloseStream")(context, update ? 1 : 0), "NSFMimePartCloseStream");
        }
    }
""";

        const string directFullItemizeWriter = """
    internal void WriteMimeStream(nint note, string itemName, byte[] data)
    {
        EnsureInitialized();
        const uint openWrite = 0x00000002u;
        const uint itemizeFull = 0x00000006u;
        Check(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(note, 0, 0, openWrite, out var stream), "MIMEStreamOpen(write)");
        try
        {
            var offset = 0;
            while (offset < data.Length)
            {
                var count = Math.Min(60000, data.Length - offset);
                var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, count));
                try
                {
                    if (count > 0) System.Runtime.InteropServices.Marshal.Copy(data, offset, buffer, count);
                    if (Resolve<MIMEStreamWriteDelegate>("MIMEStreamWrite")(buffer, checked((uint)count), stream) != 0)
                        throw new XPScriptRuntimeException(5, "MIMEStreamWrite failed.");
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
                offset += count;
            }

            using var name = ToLmbcs(itemName);
            Check(Resolve<MIMEStreamItemizeDelegate>("MIMEStreamItemize")(
                note, name.Pointer, checked((ushort)name.Length), itemizeFull, stream),
                "MIMEStreamItemize(full)");
        }
        finally { Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream); }
    }
""";

        // Mirror HCL JNX JNAMimeWriter's BODY-only branch exactly at the native
        // operation level: itemize on an unsaved temporary note, remove old target
        // Body items, then copy every generated Body and $file item by BLOCKID.
        const string jnxBodyWriter = """
    internal void WriteMimeStream(uint db, uint note, string itemName, byte[] data)
    {
        EnsureInitialized();
        const uint openWrite = 0x00000002u;
        const uint itemizeFull = 0x00000006u;
        const int mimeStreamIo = 2;

        var tmpNote = CreateNote(db);
        try
        {
            using var name = ToLmbcs(itemName);
            Check(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(
                tmpNote, name.Pointer, checked((ushort)name.Length), openWrite, out var stream),
                "MIMEStreamOpen(write temp)");
            try
            {
                var offset = 0;
                while (offset < data.Length)
                {
                    var count = Math.Min(60000, data.Length - offset);
                    var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, count));
                    try
                    {
                        if (count > 0) System.Runtime.InteropServices.Marshal.Copy(data, offset, buffer, count);
                        var result = Resolve<MIMEStreamWriteDelegate>("MIMEStreamWrite")(
                            buffer, checked((uint)count), stream);
                        if (result == mimeStreamIo)
                            throw new XPScriptRuntimeException(5, "MIMEStreamWrite failed.");
                    }
                    finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
                    offset += count;
                }

                Check(Resolve<MIMEStreamItemizeDelegate>("MIMEStreamItemize")(
                    tmpNote, name.Pointer, checked((ushort)name.Length), itemizeFull, stream),
                    "MIMEStreamItemize(full temp)");
            }
            finally
            {
                Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream);
            }

            while (HasItem(note, itemName))
                DeleteItem(note, itemName);

            CopyMimeItemsByName(tmpNote, note, itemName);
            CopyMimeItemsByName(tmpNote, note, "$file");
        }
        finally
        {
            CloseNote(tmpNote);
        }
    }

    private void CopyMimeItemsByName(uint sourceNote, uint destinationNote, string itemName)
    {
        using var name = ToLmbcs(itemName);
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            sourceNote, name.Pointer, checked((ushort)name.Length),
            out var itemBlock, out _, out _, out _);
        if ((status & ErrMask) == ErrItemNotFound) return;
        Check(status, "NSFItemInfo(MIME copy)");

        while (true)
        {
            Check(Resolve<XPScriptMimeNSFItemCopyDelegate>("NSFItemCopy")(
                destinationNote, itemBlock), "NSFItemCopy(MIME)");

            status = Resolve<NSFItemInfoNextDelegate>("NSFItemInfoNext")(
                sourceNote, itemBlock, name.Pointer, checked((ushort)name.Length),
                out var nextItemBlock, out _, out _, out _);
            if ((status & ErrMask) == ErrItemNotFound) return;
            Check(status, "NSFItemInfoNext(MIME copy)");
            itemBlock = nextItemBlock;
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort XPScriptMimeNSFItemCopyDelegate(uint destinationNote, XPScriptNotesBlockId itemBlock);
""";

        if (source.Contains(directFullItemizeWriter, StringComparison.Ordinal))
            source = source.Replace(directFullItemizeWriter, jnxBodyWriter, StringComparison.Ordinal);
        else if (source.Contains(nsfMimePartWriter, StringComparison.Ordinal))
            source = source.Replace(nsfMimePartWriter, jnxBodyWriter, StringComparison.Ordinal);
        else if (source.Contains(oldWriteMimeStreamWithItemizeBody, StringComparison.Ordinal))
            source = source.Replace(oldWriteMimeStreamWithItemizeBody, jnxBodyWriter, StringComparison.Ordinal);
        else if (source.Contains(oldWriteMimeStream, StringComparison.Ordinal))
            source = source.Replace(oldWriteMimeStream, jnxBodyWriter, StringComparison.Ordinal);
        else if (!source.Contains(jnxBodyWriter, StringComparison.Ordinal))
            throw new CompilerException("Unable to mirror JNX MIME BODY itemization flow.");

        return source;
    }
}
