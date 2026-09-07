namespace XPScript.Compiler;

internal static class NotesExtendedRuntimePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var richText = source.Contains("public XPScriptNotesRichTextItem? GetRichTextItem()", StringComparison.Ordinal);
        var mime = source.Contains("public bool ConvertMIME { get; set; }", StringComparison.Ordinal) ||
                   source.Contains("MIMEConvertMIMEPartsCCDelegate", StringComparison.Ordinal);
        return ApplyBuiltSurface(source, new NotesRuntimeFeatures(richText, mime));
    }

    public static string ApplyBuiltSurface(string source, NotesRuntimeFeatures features)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = NotesStreamPostProcessor.ApplyBuiltSurface(source);

        if (features.Mime)
        {
            source = NotesMimeItemAnchorPostProcessor.Prepare(source);
            source = NotesMimeEntityPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeItemAnchorPostProcessor.Cleanup(source);
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
