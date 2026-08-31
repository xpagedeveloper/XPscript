namespace XPScript.Compiler;

internal static class NotesExtendedRuntimePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = NotesStreamPostProcessor.ApplyBuiltSurface(source);
        source = NotesAgentPostProcessor.ApplyBuiltSurface(source);
        source = NotesDocumentMetadataPostProcessor.ApplyBuiltSurface(source);
        return source;
    }
}
