namespace XPScript.Compiler;

internal static class NotesDocumentLastAccessedPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source + "\n\n" + RuntimeSupport;
    }

    private const string RuntimeSupport = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort NoteMemberAccessedForDocument = 7;

    internal XPScriptNotesTimeDate GetDocumentLastAccessed(uint note)
    {
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(8);
        try
        {
            Zero(pointer, 8);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberAccessedForDocument, pointer);
            return System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(pointer);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }
}
""";
}
