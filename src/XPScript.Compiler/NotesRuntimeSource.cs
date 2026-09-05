namespace XPScript.Compiler;

internal static class NotesRuntimeSource
{
    public static string Code => Build(NotesRuntimeFeatures.Full);

    public static string Build(NotesRuntimeFeatures features)
    {
        var source = NotesRuntimeCoreSource.Code + "\n\n" +
                     NotesRuntimeValueSource.Code + "\n\n" +
                     NotesRuntimeDataSource.Code + "\n\n" +
                     NotesRuntimeItemSource.Build(features.RichText) + "\n\n" +
                     NotesRuntimeIndexedValueSource.Code + "\n\n" +
                     NotesNativeApiSource.Code;

        source = NotesDocumentCollectionPostProcessor.Apply(source);
        source = NotesDatabaseLotusScriptSurfacePostProcessor.Apply(source);
        source = NotesDatabaseLifecyclePostProcessor.Apply(source);
        source = NotesViewColumnNamesPostProcessor.Apply(source);
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
