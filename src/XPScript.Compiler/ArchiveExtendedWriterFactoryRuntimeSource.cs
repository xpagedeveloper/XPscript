namespace XPScript.Compiler;

internal static class ArchiveExtendedWriterFactoryRuntimeSource
{
    private static string ExtendedMemoryWriterCode => ArchiveExtendedMemoryWriterRuntimeSource.Code.Replace(
        "SharpCompress.Writers.IWriterOptions",
        "SharpCompress.Common.Options.IWriterOptions",
        StringComparison.Ordinal);

    public static string Code => """
internal static class XPScriptExtendedArchiveWriterFactory
{
    // Keep the package a real compile/runtime dependency; the archive
    // implementation also uses reflection for version-tolerant APIs.
    private static readonly Type SharpCompressAnchor = typeof(SharpCompress.Archives.ArchiveFactory);

    public static object Create(object? value)
    {
        // An unallocated Byte() argument is represented as null by the AST
        // runtime. It is still a memory archive constructor, not a path.
        if (value is null || value is byte[] || value is LSArray || value is System.Array)
            return new XPScriptExtendedMemoryArchiveV4(value);
        return new XPScriptExtendedArchiveV5(value);
    }
}
""" + "\n" + ArchiveCompressedTarRuntimeSource.Code + "\n" + ArchiveDetectedFormatRuntimeSource.Code + "\n" + ArchiveExtendedMemoryRuntimeSource.Code + "\n" + ExtendedMemoryWriterCode + "\n" + ArchiveExtendedMemoryRebuildRuntimeSource.Code + "\n" + ArchiveErrorNormalizationRuntimeSource.Code;
}
