namespace XPScript.Compiler;

internal static class NotesExtendedRuntimePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var mime = source.Contains("public XPScriptNotesRichTextItem? GetRichTextItem()", StringComparison.Ordinal);
        return ApplyBuiltSurface(source, new NotesRuntimeFeatures(mime, mime));
    }

    public static string ApplyBuiltSurface(string source, NotesRuntimeFeatures features)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = NotesStreamPostProcessor.ApplyBuiltSurface(source);

        if (features.Mime)
        {
            source = NotesMimeEntityPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeEntityAbiPostProcessor.ApplyBuiltSurface(source);
        }

        source = NotesDocumentMetadataPostProcessor.ApplyBuiltSurface(source);
        source = NotesDocumentAuthorsPostProcessor.ApplyBuiltSurface(source);
        source = NotesSigningPostProcessor.ApplyBuiltSurface(source);
        source = NotesDocumentCollectionStampAllMultiPostProcessor.ApplyBuiltSurface(source);
        source = NotesDocumentCollectionNormalizationPostProcessor.ApplyBuiltSurface(source);
        source = NotesDocumentCollectionProvenancePostProcessor.ApplyBuiltSurface(source);
        source = NotesDocumentHandleGuardPostProcessor.ApplyBuiltSurface(source);
        return source;
    }
}
