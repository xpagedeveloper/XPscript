namespace XPScript.Compiler;

internal static class NativeInteropRuntimeSource
{
    public const string Code = """
internal static class XPNativeInteropRuntime
{
    public static Exception LibraryNotFound(string library, string entryPoint, Exception inner)
    {
        var location = AppContext.BaseDirectory;
        return new InvalidOperationException(
            $"Unable to load native library '{library}' for entry point '{entryPoint}'. " +
            $"Application-local libraries are expected beside the generated program ('{location}'); system-library names are resolved by the operating-system loader. " +
            "Check that the library exists for the current OS/architecture and that any dependent native libraries are also available.", inner);
    }

    public static Exception EntryPointNotFound(string library, string entryPoint, Exception inner) =>
        new InvalidOperationException(
            $"Native library '{library}' was loaded, but entry point '{entryPoint}' was not found. " +
            "Check the selected Alias/OS/architecture-specific Alias and the exported symbol name.", inner);

    public static Exception WrongArchitecture(string library, string entryPoint, Exception inner) =>
        new InvalidOperationException(
            $"Native library '{library}' could not be loaded for entry point '{entryPoint}' because its binary format or architecture is incompatible with the running program. " +
            $"Current process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}; operating system: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}.", inner);
}
""";
}
