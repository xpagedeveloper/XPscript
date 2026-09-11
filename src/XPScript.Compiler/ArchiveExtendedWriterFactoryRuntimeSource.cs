namespace XPScript.Compiler;

internal static class ArchiveExtendedWriterFactoryRuntimeSource
{
    public static string Code => """
internal static class XPScriptExtendedArchiveWriterFactory
{
    public static object Create(object? value)
    {
        if (value is byte[] || value is LSArray)
            return new XPScriptExtendedMemoryArchiveV4(value);
        return new XPScriptExtendedArchiveV5(value);
    }
}
""" + "\n" + ArchiveCompressedTarRuntimeSource.Code + "\n" + ArchiveDetectedFormatRuntimeSource.Code + "\n" + ArchiveExtendedMemoryRuntimeSource.Code + "\n" + ArchiveExtendedMemoryWriterRuntimeSource.Code + "\n" + ArchiveExtendedMemoryRebuildRuntimeSource.Code + "\n" + ArchiveErrorNormalizationRuntimeSource.Code;
}
