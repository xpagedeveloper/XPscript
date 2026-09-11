namespace XPScript.Compiler;

internal static class ArchiveExtendedWriterFactoryRuntimeSource
{
    public static string Code => """
internal static class XPScriptExtendedArchiveWriterFactory
{
    public static object Create(object? value)
    {
        if (value is byte[] || value is LSArray)
            return new XPScriptExtendedMemoryArchiveV2(value);
        return new XPScriptExtendedArchiveV3(value);
    }
}
""" + "\n" + ArchiveCompressedTarRuntimeSource.Code + "\n" + ArchiveExtendedMemoryRuntimeSource.Code + "\n" + ArchiveExtendedMemoryWriterRuntimeSource.Code;
}
