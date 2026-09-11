namespace XPScript.Compiler;

internal static class ArchiveExtendedWriterFactoryRuntimeSource
{
    public static string Code => """
internal static class XPScriptExtendedArchiveWriterFactory
{
    public static object Create(object? value)
    {
        if (value is byte[] || value is LSArray)
            throw new XPScriptRuntimeException(5, "Extended in-memory archive support is not implemented yet. Use Archive(bytes) for ZIP or a file path with Archive(path, True) for extended formats.");
        return new XPScriptExtendedArchiveV3(value);
    }
}
""" + "\n" + ArchiveCompressedTarRuntimeSource.Code;
}
