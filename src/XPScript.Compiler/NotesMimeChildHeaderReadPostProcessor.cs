namespace XPScript.Compiler;

internal static class NotesMimeChildHeaderReadPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldValue = """
        throw new System.NotSupportedException("NotesMIMEEntity.GetNthHeader requires verified Domino MIME entity header lookup; managed root-stream header parsing is intentionally not used.");
""";

        const string newValue = """
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) return null;
        var occurrence = Math.Max(1, XPScriptRuntime.CInt(occurrenceValue));
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var child = _nativeEntity == _mimeDirectoryOwner.RootEntity
            ? raw
            : _mimeDirectoryOwner.Parent(_nativeEntity) == _mimeDirectoryOwner.RootEntity
                ? ReadCurrentDirectChild("GetNthHeader")
                : GetSerializedEntity(raw, GetEntityPath());
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

        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to inject direct-child MIME header lookup.");

        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
