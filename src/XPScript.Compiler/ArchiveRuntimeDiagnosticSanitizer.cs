namespace XPScript.Compiler;

internal static class ArchiveRuntimeDiagnosticSanitizer
{
    public static string Sanitize(string code) => code
        .Replace("SharpCompress ReaderFactory.OpenReader(string, ReaderOptions) was not found.", "Archive reader entry point was not found.", StringComparison.Ordinal)
        .Replace("SharpCompress failed to open the archive reader.", "Archive reader could not be opened.", StringComparison.Ordinal)
        .Replace("Unable to load SharpCompress reader support: ", "Unable to load extended archive reader support: ", StringComparison.Ordinal)
        .Replace("SharpCompress reader entry is unavailable.", "Archive reader entry is unavailable.", StringComparison.Ordinal)
        .Replace("SharpCompress reader OpenEntryStream() was not found.", "Archive reader entry stream is unavailable.", StringComparison.Ordinal)
        .Replace("SharpCompress ArchiveFactory.OpenArchive(string, ReaderOptions) was not found.", "Archive reader entry point was not found.", StringComparison.Ordinal)
        .Replace("SharpCompress failed to open the archive.", "Archive could not be opened.", StringComparison.Ordinal)
        .Replace("Unable to load SharpCompress extended archive support: ", "Unable to load extended archive support: ", StringComparison.Ordinal)
        .Replace("SharpCompress archive entries are unavailable.", "Archive entries are unavailable.", StringComparison.Ordinal)
        .Replace("SharpCompress entry OpenEntryStream() was not found.", "Archive entry stream is unavailable.", StringComparison.Ordinal)
        .Replace("SharpCompress WriterFactory.OpenWriter(Stream, ArchiveType, IWriterOptions) was not found.", "Archive writer entry point was not found.", StringComparison.Ordinal)
        .Replace("SharpCompress failed to create the archive writer.", "Archive writer could not be created.", StringComparison.Ordinal)
        .Replace("SharpCompress writer entry methods were not found.", "Archive writer entry methods are unavailable.", StringComparison.Ordinal)
        .Replace("SharpCompress failed to create the compressed TAR writer.", "Compressed TAR writer could not be created.", StringComparison.Ordinal)
        .Replace("SharpCompress TAR writer entry methods were not found.", "Compressed TAR writer entry methods are unavailable.", StringComparison.Ordinal);
}
