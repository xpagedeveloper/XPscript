namespace XPScript.Compiler;

internal static class NotesRuntimeSource
{
    internal static string Apply(string source, NotesRuntimeFeatureSet features)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = NotesSessionPostProcessor.Apply(source);
        source = NotesDatabasePostProcessor.Apply(source);
        source = NotesDocumentPostProcessor.Apply(source);
        source = NotesItemPostProcessor.Apply(source);
        source = NotesDateTimePostProcessor.Apply(source);
        source = NotesNamePostProcessor.Apply(source);
        source = NotesViewPostProcessor.Apply(source);
        source = NotesViewEntryPostProcessor.Apply(source);
        source = NotesViewEntryCollectionPostProcessor.Apply(source);
        source = NotesViewNavigatorPostProcessor.Apply(source);
        source = NotesViewNavigationPostProcessor.Apply(source);
        source = NotesViewNavigationV2PostProcessor.Apply(source);
        source = NotesViewNavigationV2FixPostProcessor.Apply(source);
        source = NotesViewNavigationV3PostProcessor.Apply(source);
        source = NotesViewNavigationV3FixPostProcessor.Apply(source);
        source = NotesViewNavigatorCachePolicyPostProcessor.Apply(source);
        source = NotesViewNavigatorCachePostProcessor.Apply(source);
        source = NotesViewNavigatorBufferMaxEntriesPostProcessor.Apply(source);
        source = NotesViewNavigatorHistoryCapPostProcessor.Apply(source);
        source = NotesDatabaseCreateCompatibilityPostProcessor.Apply(source);
        source = NotesDocumentRemovePostProcessor.Apply(source);
        source = NotesDocumentLotusScriptSurfacePostProcessor.Apply(source);
        source = NotesDocumentComputeWithFormPostProcessor.Apply(source);
        source = NotesDxlPostProcessor.Apply(source);
        source = NotesThreadLifecyclePostProcessor.Apply(source);

        if (features.RichText)
        {
            source = NotesRichTextMimePostProcessor.Apply(source);
            source = NotesRichTextObjectsPostProcessor.Apply(source);
            source = NotesRichTextNavigatorConstantsPostProcessor.Apply(source);
            source = NotesRichTextRangePostProcessor.Apply(source);
            source = NotesEmbeddedObjectPostProcessor.Apply(source);
            source = NotesRichTextAttachmentInsertPostProcessor.Apply(source);
            source = NotesRichTextAttachmentRemovePostProcessor.Apply(source);
            source = NotesRichTextNavigatorElementPostProcessor.Apply(source);
            source = NotesRichTextLinkedObjectsPostProcessor.Apply(source);
            source = NotesRichTextLinkedObjectsCompatibilityPostProcessor.Apply(source);
            source = NotesRichTextTableSpanPostProcessor.Apply(source);
            source = NotesRichTextNavigatorPositionPostProcessor.Apply(source);
            source = NotesRichTextCdElementModelPostProcessor.Apply(source);
            source = NotesRichTextStructuralSpanPostProcessor.Apply(source);
            source = NotesRichTextLogicalSpanPostProcessor.Apply(source);
            source = NotesRichTextRangeSemanticsPostProcessor.Apply(source);
            source = NotesRichTextCdRewritePostProcessor.Apply(source);
            source = NotesEmbeddedBinaryArrayFixPostProcessor.Apply(source);
            source = NotesRichTextSurfaceAuditPostProcessor.Apply(source);
        }

        NotesViewNavigatorCachePolicyRegression.Validate(source);
        return source;
    }
}
