namespace XPScript.Compiler;

internal static class NotesRuntimeSource
{
    public static string Code => NotesThreadLifecyclePostProcessor.Apply(
        NotesDocumentComputeWithFormPostProcessor.Apply(
            NotesDocumentLotusScriptSurfacePostProcessor.Apply(
                NotesDocumentRemovePostProcessor.Apply(
                    NotesDatabaseCreateCompatibilityPostProcessor.Apply(
                        NotesViewNavigatorCachePostProcessor.Apply(
                            NotesViewNavigatorCachePolicyPostProcessor.Apply(
                                NotesViewNavigationV3FixPostProcessor.Apply(
                                    NotesViewNavigationV3PostProcessor.Apply(
                                        NotesViewNavigationV2FixPostProcessor.Apply(
                                            NotesViewNavigationV2PostProcessor.Apply(
                                                NotesViewNavigationPostProcessor.Apply(
                                                    NotesViewColumnNamesPostProcessor.Apply(
                                                        NotesDatabaseLifecyclePostProcessor.Apply(
                                                            NotesDatabaseLotusScriptSurfacePostProcessor.Apply(
                                                                NotesDocumentCollectionPostProcessor.Apply(
                                                                    NotesRuntimeCoreSource.Code + "\n\n" +
                                                                    NotesRuntimeValueSource.Code + "\n\n" +
                                                                    NotesRuntimeDataSource.Code + "\n\n" +
                                                                    NotesRuntimeItemSource.Code + "\n\n" +
                                                                    NotesRuntimeIndexedValueSource.Code + "\n\n" +
                                                                    NotesNativeApiSource.Code))))))))))))))));
}
