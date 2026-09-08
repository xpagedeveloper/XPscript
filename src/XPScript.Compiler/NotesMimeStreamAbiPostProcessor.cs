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

        const string newWriteMimeStream = """
    internal void WriteMimeStream(nint note, string itemName, byte[] data)
    {
        EnsureInitialized();
        // HCL nsfmime.h: MIME_PART_BODY = 2, MIME_PART_HAS_HEADERS = 2.
        // NSFMimePartCloseStream(..., TRUE) is the call that flushes the context
        // and creates the TYPE_MIME_PART item on the note.
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

        if (source.Contains(oldWriteMimeStreamWithItemizeBody, StringComparison.Ordinal))
            source = source.Replace(oldWriteMimeStreamWithItemizeBody, newWriteMimeStream, StringComparison.Ordinal);
        else if (source.Contains(oldWriteMimeStream, StringComparison.Ordinal))
            source = source.Replace(oldWriteMimeStream, newWriteMimeStream, StringComparison.Ordinal);
        else if (!source.Contains(newWriteMimeStream, StringComparison.Ordinal))
            throw new CompilerException("Unable to replace MIME stream writer with native NSF MIME part writer.");

        const string delegateAnchor = "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void MIMEStreamCloseDelegate(nint stream);";
        const string delegateReplacement = delegateAnchor + "\n" +
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFMimePartCreateStreamDelegate(nint note, nint itemName, ushort itemNameLength, ushort partType, uint flags, out uint context);\n" +
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFMimePartAppendStreamDelegate(uint context, nint data, ushort dataLength);\n" +
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFMimePartCloseStreamDelegate(uint context, int update);";

        if (source.Contains(delegateAnchor, StringComparison.Ordinal) && !source.Contains("NSFMimePartCreateStreamDelegate", StringComparison.Ordinal))
            source = source.Replace(delegateAnchor, delegateReplacement, StringComparison.Ordinal);
        else if (!source.Contains("private delegate ushort NSFMimePartCreateStreamDelegate", StringComparison.Ordinal))
            throw new CompilerException("Unable to inject NSF MIME part stream delegates.");

        return source;
    }
}
