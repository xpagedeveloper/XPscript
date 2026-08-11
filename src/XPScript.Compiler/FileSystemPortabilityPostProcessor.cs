namespace XPScript.Compiler;

internal sealed class FileSystemPortabilityPostProcessor
{
    public string Transform(string generated)
    {
        if (string.IsNullOrEmpty(generated)) return generated;

        // Never let implicit file encoding vary with the target operating system.
        generated = generated.Replace(
            "Encoding.Default",
            "XPScriptFileSystemRuntime.LegacyEncoding",
            StringComparison.Ordinal);

        // Route both core and Charset-aware Open paths through one target-OS resolver.
        generated = generated.Replace(
            "Path.GetFullPath(XPScriptRuntime.CStr(pathValue))",
            "XPScriptFileSystemRuntime.ResolvePath(pathValue)",
            StringComparison.Ordinal);

        return generated;
    }
}
