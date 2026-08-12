namespace XPScript.Compiler;

internal static class NativeInteropRuntimeSource
{
    public const string Code = """
internal static class XPNativeInteropRuntime
{
    private static readonly object ResolverGate = new();
    private static readonly HashSet<string> ApplicationLocalLibraries = new(StringComparer.OrdinalIgnoreCase);
    private static bool ResolverInstalled;

    public static void EnsureApplicationLocalLibrary(string library)
    {
        if (string.IsNullOrWhiteSpace(library) ||
            library != Path.GetFileName(library) ||
            library.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            throw new InvalidOperationException("Invalid application-local native library name.");

        lock (ResolverGate)
        {
            ApplicationLocalLibraries.Add(library);
            if (ResolverInstalled) return;

            System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(
                typeof(Script).Assembly,
                ResolveApplicationLocalLibrary);
            ResolverInstalled = true;
        }
    }

    private static IntPtr ResolveApplicationLocalLibrary(
        string libraryName,
        System.Reflection.Assembly assembly,
        System.Runtime.InteropServices.DllImportSearchPath? searchPath)
    {
        lock (ResolverGate)
        {
            if (!ApplicationLocalLibraries.Contains(libraryName)) return IntPtr.Zero;
        }

        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var candidate = Path.GetFullPath(Path.Combine(baseDirectory, libraryName));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var basePrefix = baseDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? baseDirectory
            : baseDirectory + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(basePrefix, comparison))
            throw new DllNotFoundException("Application-local native library path escaped the executable directory.");

        var info = new FileInfo(candidate);
        if (!info.Exists)
            throw new DllNotFoundException("Application-local native library was not found beside the executable.");
        if (info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new DllNotFoundException("Application-local native library may not be a symbolic link or reparse point.");

        return System.Runtime.InteropServices.NativeLibrary.Load(candidate, assembly, searchPath);
    }

    public static Exception LibraryNotFound(string library, string entryPoint, Exception inner)
    {
        _ = inner;
        return new InvalidOperationException(
            $"Unable to load native library '{library}' for entry point '{entryPoint}'. " +
            "Application-local libraries are loaded only from the executable directory; system-library names are resolved by the operating-system loader. " +
            "Check that the library exists for the current OS/architecture and that any dependent native libraries are also available.");
    }

    public static Exception EntryPointNotFound(string library, string entryPoint, Exception inner)
    {
        _ = inner;
        return new InvalidOperationException(
            $"Native library '{library}' was loaded, but entry point '{entryPoint}' was not found. " +
            "Check the selected Alias/OS/architecture-specific Alias and the exported symbol name.");
    }

    public static Exception WrongArchitecture(string library, string entryPoint, Exception inner)
    {
        _ = inner;
        return new InvalidOperationException(
            $"Native library '{library}' could not be loaded for entry point '{entryPoint}' because its binary format or architecture is incompatible with the running program. " +
            $"Current process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}.");
    }
}
""";
}
