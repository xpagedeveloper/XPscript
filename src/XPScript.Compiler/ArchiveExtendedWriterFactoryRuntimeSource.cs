namespace XPScript.Compiler;

internal static class ArchiveExtendedWriterFactoryRuntimeSource
{
    public static string Code => """
internal static class XPScriptExtendedArchiveWriterFactory
{
    public static object Create(object? value)
    {
        if (value is byte[] || value is LSArray)
            return new XPScriptExtendedMemoryArchiveV3(value);
        return new XPScriptExtendedArchiveV4(value);
    }
}
""" + "\n" + ArchiveCompressedTarRuntimeSource.Code + "\n" + ArchiveDetectedFormatRuntimeSource.Code + "\n" + ArchiveExtendedMemoryRuntimeSource.Code + "\n" + ArchiveExtendedMemoryWriterRuntimeSource.Code + "\n" + ArchiveExtendedMemoryRebuildRuntimeSource.Code;
}
