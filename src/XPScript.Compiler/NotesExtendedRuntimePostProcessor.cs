namespace XPScript.Compiler;

internal static class NotesExtendedRuntimePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var mime = source.Contains("XPScriptNotesMIMEEntity", StringComparison.Ordinal);
        var richText = source.Contains("public XPScriptNotesRichTextItem? GetRichTextItem()", StringComparison.Ordinal);
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
            source = NotesMimeStreamAbiPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeDirectoryPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeDirectoryOwnerPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeDocumentLifecyclePostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeNativeEntityPostProcessor.ApplyBuiltSurface(source);
            source = NotesMimeManagedRuntimeCleanupPostProcessor.ApplyBuiltSurface(source);
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
