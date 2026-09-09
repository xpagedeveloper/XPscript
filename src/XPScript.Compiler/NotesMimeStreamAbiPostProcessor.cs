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

        // JNX's JNAMimeWriter is our executable cross-check for the native ABI.
        // For BODY-only writes it deliberately itemizes MIME_STREAM_ITEMIZE_FULL on
        // a temporary note and copies the resulting MIME_PART items to the target.
        // XPscript currently has no safe native item-copy helper here, but its
        // CreateMIMEEntity payload contains only MIME body headers/body, so use the
        // same FULL itemization on the target note. This is preferable to the
        // NSFMimePartCreateStream experiment, which returned success but produced no
        // reopenable Body item in the runtime probe.
        const string jnxAlignedWriter = """
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

        if (source.Contains(nsfMimePartWriter, StringComparison.Ordinal))
            source = source.Replace(nsfMimePartWriter, jnxAlignedWriter, StringComparison.Ordinal);
        else if (source.Contains(oldWriteMimeStreamWithItemizeBody, StringComparison.Ordinal))
            source = source.Replace(oldWriteMimeStreamWithItemizeBody, jnxAlignedWriter, StringComparison.Ordinal);
        else if (source.Contains(oldWriteMimeStream, StringComparison.Ordinal))
            source = source.Replace(oldWriteMimeStream, jnxAlignedWriter, StringComparison.Ordinal);
        else if (!source.Contains(jnxAlignedWriter, StringComparison.Ordinal))
            throw new CompilerException("Unable to align MIME stream writer with JNX native itemization flow.");

        return source;
    }
}
