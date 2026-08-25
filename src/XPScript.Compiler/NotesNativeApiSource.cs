namespace XPScript.Compiler;

internal static class NotesNativeApiSource
{
    public static string Code =>
        NotesNativeApiBaseSource.Code + "\n\n" +
        NotesNativeApiVersionSource.Code + "\n\n" +
        NotesNativeApiTimeSource.Code + "\n\n" +
        NotesNativeApiDocumentSource.Code + "\n\n" +
        NotesNativeApiItemSource.Code + "\n\n" +
        NotesNativeApiSearchSource.Code;
}
