namespace XPScript.Compiler;

internal static class NotesMimeStreamAbiPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldIncludeHeaders = "const uint includeHeaders = 0x00000004u;";
        const string newIncludeHeaders = "const uint includeHeaders = 0x00000010u;";

        if (source.Contains(oldIncludeHeaders, StringComparison.Ordinal))
            source = source.Replace(oldIncludeHeaders, newIncludeHeaders, StringComparison.Ordinal);
        else if (!source.Contains(newIncludeHeaders, StringComparison.Ordinal))
            throw new CompilerException("Unable to normalize MIME_STREAM_MIME_INCLUDE_HEADERS.");

        const string oldItemizeCall = "Check(Resolve<MIMEStreamItemizeDelegate>(\"MIMEStreamItemize\")(note, name.Pointer, checked((ushort)name.Length), 0, stream), \"MIMEStreamItemize\");";
        const string newItemizeCall = "const uint itemizeBody = 0x00000004u;\n                Check(Resolve<MIMEStreamItemizeDelegate>(\"MIMEStreamItemize\")(note, name.Pointer, checked((ushort)name.Length), itemizeBody, stream), \"MIMEStreamItemize\");";

        if (source.Contains(oldItemizeCall, StringComparison.Ordinal))
            source = source.Replace(oldItemizeCall, newItemizeCall, StringComparison.Ordinal);
        else if (!source.Contains(newItemizeCall, StringComparison.Ordinal))
            throw new CompilerException("Unable to normalize MIME_STREAM_ITEMIZE_BODY.");

        return source;
    }
}
