namespace XPScript.Compiler;

internal static class NotesExtendedRuntimePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = NotesStreamPostProcessor.ApplyBuiltSurface(source);

        // The MIME surface currently extends NotesItem at the rich-text/MIME-capable
        // runtime boundary. Core Notes builds deliberately omit that boundary, so do
        // not attempt MIME injection there. NotesRuntimeFeatures enables RichText for
        // explicit NotesMIMEEntity/NotesMIMEHeader usage.
        if (source.Contains("public XPScriptNotesRichTextItem? GetRichTextItem()", StringComparison.Ordinal))
        {
            source = NotesMimeEntityPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeEntityAbiPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeMultipartPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeMultipartSemanticsPostProcessor.ApplyBuiltSurface(source);
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
