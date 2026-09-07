namespace XPScript.Compiler;

internal static class NotesMimeSessionPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public string Platform { get; }\n    public bool IsRecycled => _recycled;",
            "    public string Platform { get; }\n    public bool ConvertMIME { get; set; } = true;\n    public bool IsRecycled => _recycled;",
            "session-convert-mime");

        source = ReplaceRequired(source,
            "        _handle = handle;\n        NoteId = noteId;",
            "        _handle = handle;\n        NoteId = noteId;\n        if (handle != 0 && session.ConvertMIME)\n            Session.Api.ConvertMimePartsToComposite(checked((uint)handle));",
            "document-open-convert-mime");

        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal void ConvertMimePartsToComposite(uint note)
    {
        EnsureInitialized();
        if (!HasMimePart(note)) return;
        Check(Resolve<MIMEConvertMIMEPartsCCDelegate>("MIMEConvertMIMEPartsCC")(note, 0, 0), "MIMEConvertMIMEPartsCC");
    }

    private bool HasMimePart(uint note)
    {
        foreach (var name in GetItemNames(note))
        {
            if (!TryGetFirstItemInfo(note, name, out var info)) continue;
            if (info.DataType == NotesTypeMimePart) return true;
        }
        return false;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEConvertMIMEPartsCCDelegate(uint note, int canonical, nint conversionControls);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes MIME session patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
