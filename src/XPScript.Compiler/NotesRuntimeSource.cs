namespace XPScript.Compiler;

internal static class NotesRuntimeSource
{
    public static string Code => NotesThreadLifecyclePostProcessor.Apply(
        NotesDocumentComputeWithFormPostProcessor.Apply(
            NotesDocumentLotusScriptSurfacePostProcessor.Apply(
                NotesDocumentRemovePostProcessor.Apply(
                    NotesDatabaseLotusScriptSurfacePostProcessor.Apply(
                        NotesDocumentCollectionPostProcessor.Apply(
                            NotesRuntimeCoreSource.Code + "\n\n" +
                            NotesRuntimeValueSource.Code + "\n\n" +
                            NotesRuntimeDataSource.Code + "\n\n" +
                            NotesRuntimeItemSource.Code + "\n\n" +
                            NotesRuntimeIndexedValueSource.Code + "\n\n" +
                            NotesNativeApiSource.Code))))));
}
