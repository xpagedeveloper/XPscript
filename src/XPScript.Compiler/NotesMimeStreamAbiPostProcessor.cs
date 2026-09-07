namespace XPScript.Compiler;

internal static class NotesMimeStreamAbiPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        const string oldValue = "const uint includeHeaders = 0x00000004u;";
        const string newValue = "const uint includeHeaders = 0x00000010u;";

        if (source.Contains(oldValue, StringComparison.Ordinal))
            return source.Replace(oldValue, newValue, StringComparison.Ordinal);

        if (source.Contains(newValue, StringComparison.Ordinal))
            return source;

        throw new CompilerException("Unable to normalize MIME_STREAM_MIME_INCLUDE_HEADERS.");
    }
}
