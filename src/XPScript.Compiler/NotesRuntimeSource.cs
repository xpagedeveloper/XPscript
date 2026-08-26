namespace XPScript.Compiler;

internal static class NotesRuntimeSource
{
    public static string Code =>
        NotesRuntimeCoreSource.Code + "\n\n" +
        NotesRuntimeValueSource.Code + "\n\n" +
        NotesRuntimeDataSource.Code + "\n\n" +
        NotesRuntimeItemSource.Code + "\n\n" +
        NotesRuntimeIndexedValueSource.Code + "\n\n" +
        NotesNativeApiSource.Code;
}
