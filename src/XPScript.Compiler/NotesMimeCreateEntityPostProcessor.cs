namespace XPScript.Compiler;

internal static class NotesMimeCreateEntityPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal void CreateEmptyMimePart(uint note, string itemName)
    {
        EnsureInitialized();

        const ushort mimePartVersion = 2;
        const byte mimePartBody = 2;
        const int mimePartSize = 20;

        // MIME_PART is an ODS structure. JNX maps it with ALIGN_NONE, so build the
        // packed 20-byte value explicitly instead of relying on CLR struct packing.
        // An uninitialized top-level entity has no boundary, headers, or body bytes.
        var mimePart = new byte[mimePartSize];
        mimePart[0] = (byte)mimePartVersion;
        mimePart[1] = (byte)(mimePartVersion >> 8);
        mimePart[6] = mimePartBody;

        using var name = ToLmbcs(itemName);
        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(mimePart.Length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(mimePart, 0, value, mimePart.Length);
            Check(Resolve<XPScriptNSFItemAppendDelegate>("NSFItemAppend")(
                note,
                0,
                name.Pointer,
                checked((ushort)name.Length),
                NotesTypeMimePart,
                value,
                checked((uint)mimePart.Length)),
                "NSFItemAppend(empty MIME entity)");
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(value);
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort XPScriptNSFItemAppendDelegate(
        uint note,
        ushort itemFlags,
        nint itemName,
        ushort itemNameLength,
        ushort itemType,
        nint itemValue,
        uint itemValueLength);
}
""";
}
