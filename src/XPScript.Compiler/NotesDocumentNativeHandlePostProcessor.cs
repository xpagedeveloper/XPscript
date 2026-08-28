using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class NotesDocumentNativeHandlePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // This processor is intentionally applied only to the new NotesDocument
        // native fragment before it is concatenated with the rest of the Notes
        // runtime. DBHANDLE, NOTEHANDLE and DHANDLE are 32-bit Domino handles;
        // memory pointers remain nint.
        source = source.Replace("IDScanDelegate", "NotesDocumentIDScanDelegate", StringComparison.Ordinal);
        source = Regex.Replace(
            source,
            @"\bnint\s+(note|parentNote|sourceNote|destinationNote|db|destinationDb|table|original|dataDb|folderDb|idTable)\b",
            "uint $1");

        source = source.Replace(
            "internal nint CopyDocumentToDatabase(",
            "internal uint CopyDocumentToDatabase(",
            StringComparison.Ordinal);
        source = source.Replace(
            "NSFNoteCopyDelegate(nint source, out nint destination)",
            "NSFNoteCopyDelegate(uint source, out uint destination)",
            StringComparison.Ordinal);
        source = source.Replace(
            "IDTableCopyDelegate(nint source, out nint destination)",
            "IDTableCopyDelegate(uint source, out uint destination)",
            StringComparison.Ordinal);
        source = source.Replace(
            "Action<nint>",
            "Action<uint>",
            StringComparison.Ordinal);
        source = source.Replace(
            "var table = System.Runtime.InteropServices.Marshal.ReadIntPtr(pointer);",
            "var table = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer));",
            StringComparison.Ordinal);
        source = source.Replace(
            "System.Runtime.InteropServices.Marshal.WriteIntPtr(dbPointer, destinationDb);",
            "System.Runtime.InteropServices.Marshal.WriteInt32(dbPointer, unchecked((int)destinationDb));",
            StringComparison.Ordinal);

        return source;
    }
}
