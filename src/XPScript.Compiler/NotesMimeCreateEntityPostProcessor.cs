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
        const ushort mimePartBody = 2;
        const uint mimePartFlags = 0;

        using var name = ToLmbcs(itemName);
        Check(Resolve<XPScriptNSFMimePartCreateStreamDelegate>("NSFMimePartCreateStream")(
            note,
            name.Pointer,
            checked((ushort)name.Length),
            mimePartBody,
            mimePartFlags,
            out var context),
            "NSFMimePartCreateStream(empty MIME entity)");

        Check(Resolve<XPScriptNSFMimePartCloseStreamDelegate>("NSFMimePartCloseStream")(
            context,
            1),
            "NSFMimePartCloseStream(empty MIME entity)");
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort XPScriptNSFMimePartCreateStreamDelegate(
        uint note,
        nint itemName,
        ushort itemNameLength,
        ushort partType,
        uint flags,
        out uint context);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort XPScriptNSFMimePartCloseStreamDelegate(uint context, int update);
}
""";
}
