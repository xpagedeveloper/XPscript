namespace XPScript.Compiler;

internal static class NotesMimeEntityAbiPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        const string oldValue = "const uint includeHeaders = 0x00000004u;";
        const string newValue = "const uint includeHeaders = 0x00000010u;";
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesMIMEEntity MIME_STREAM_MIME_INCLUDE_HEADERS ABI constant.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
