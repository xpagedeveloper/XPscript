namespace XPScript.Compiler;

internal static class NotesExtendedRuntimePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = NotesStreamPostProcessor.ApplyBuiltSurface(source);
        source = NotesDocumentMetadataPostProcessor.ApplyBuiltSurface(source);
        source = NotesDocumentAuthorsPostProcessor.ApplyBuiltSurface(source);
        source = NotesSigningPostProcessor.ApplyBuiltSurface(source);
        return source;
    }
}
