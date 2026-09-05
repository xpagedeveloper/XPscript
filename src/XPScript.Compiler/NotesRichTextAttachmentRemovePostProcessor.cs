namespace XPScript.Compiler;

internal static class NotesRichTextAttachmentRemovePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public object ToByteArray()
    {
        EnsureEmbeddedAlive();
        var bytes = Session.Api.ReadAttachmentBytes(_parent.Parent.NativeHandle, _metadata.Name);
        return XPScriptNotesBinaryArrayFactory.Create(bytes);
    }

    private void EnsureEmbeddedAlive()
""",
            """
    public object ToByteArray()
    {
        EnsureEmbeddedAlive();
        var bytes = Session.Api.ReadAttachmentBytes(_parent.Parent.NativeHandle, _metadata.Name);
        return XPScriptNotesBinaryArrayFactory.Create(bytes);
    }

    public void Remove()
    {
        EnsureEmbeddedAlive();
        // Removing only the $FILE object would leave a dangling file hotspot.
        // Keep this operation fail-closed until the shared composite-data rewrite
        // path can remove the hotspot and detach the file as one guarded mutation.
        throw new XPScriptRuntimeException(
            445,
            "NotesEmbeddedObject.Remove requires atomic rich-text hotspot rewrite before the $FILE object can be detached.");
    }

    private void EnsureEmbeddedAlive()
""",
            "embedded-attachment-remove-surface");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text attachment removal patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
