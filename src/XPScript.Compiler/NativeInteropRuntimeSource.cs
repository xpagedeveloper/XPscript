namespace XPScript.Compiler;

internal static class NativeInteropRuntimeSource
{
    public const string Code = """
internal static class XPNativeInteropRuntime
{
    private const string ApplicationLocalMarker = "__XPSCRIPT_APPLOCAL__";
    private const uint LoadLibrarySearchDllLoadDir = 0x00000100;
    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;
    private static readonly object ResolverGate = new();
    private static readonly HashSet<string> ApplicationLocalLibraries = new(StringComparer.OrdinalIgnoreCase);
    private static bool ResolverInstalled;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string lpLibFileName, IntPtr hFile, uint dwFlags);

    public static void Initialize()
    {
        lock (ResolverGate)
        {
            if (ResolverInstalled) return;
            System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(
                typeof(Script).Assembly,
                ResolveApplicationLocalLibrary);
            ResolverInstalled = true;
        }
    }

    public static void EnsureApplicationLocalLibrary(string library)
    {
        library = NormalizeApplicationLocalName(library);
        ValidateApplicationLocalName(library);

        lock (ResolverGate)
        {
            ApplicationLocalLibraries.Add(library);
        }
        Initialize();
    }

    private static IntPtr ResolveApplicationLocalLibrary(
        string libraryName,
        System.Reflection.Assembly assembly,
        System.Runtime.InteropServices.DllImportSearchPath? searchPath)
    {
        var marked = libraryName.StartsWith(ApplicationLocalMarker, StringComparison.Ordinal);
        var normalizedName = marked ? NormalizeApplicationLocalName(libraryName) : libraryName;

        if (!marked)
        {
            lock (ResolverGate)
            {
                if (!ApplicationLocalLibraries.Contains(normalizedName)) return IntPtr.Zero;
            }
        }

        ValidateApplicationLocalName(normalizedName);
        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        var candidate = Path.GetFullPath(Path.Combine(baseDirectory, normalizedName));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var directorySeparator = Path.DirectorySeparatorChar.ToString();
        var basePrefix = baseDirectory.EndsWith(directorySeparator, StringComparison.Ordinal)
            ? baseDirectory
            : baseDirectory + directorySeparator;

        if (!candidate.StartsWith(basePrefix, comparison))
            throw new DllNotFoundException("Application-local native library path escaped the executable directory.");

        var info = new FileInfo(candidate);
        if (!info.Exists)
            throw new DllNotFoundException("Application-local native library was not found beside the executable.");
        if (info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new DllNotFoundException("Application-local native library may not be a symbolic link or reparse point.");

        if (OperatingSystem.IsWindows())
        {
            var handle = LoadLibraryExW(
                candidate,
                IntPtr.Zero,
                LoadLibrarySearchDllLoadDir | LoadLibrarySearchDefaultDirs);
            if (handle == IntPtr.Zero)
                throw new DllNotFoundException("Application-local native library or one of its dependencies could not be loaded from approved Windows search locations.");
            return handle;
        }

        return System.Runtime.InteropServices.NativeLibrary.Load(candidate, assembly, searchPath);
    }

    private static string NormalizeApplicationLocalName(string library)
    {
        return library.StartsWith(ApplicationLocalMarker, StringComparison.Ordinal)
            ? library[ApplicationLocalMarker.Length..]
            : library;
    }

    private static void ValidateApplicationLocalName(string library)
    {
        var directorySeparator = Path.DirectorySeparatorChar.ToString();
        var alternateSeparator = Path.AltDirectorySeparatorChar.ToString();
        if (string.IsNullOrWhiteSpace(library) ||
            library != Path.GetFileName(library) ||
            library.Contains(directorySeparator, StringComparison.Ordinal) ||
            library.Contains(alternateSeparator, StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid application-local native library name.");
    }

    public static Exception LibraryNotFound(string library, string entryPoint, Exception inner)
    {
        _ = inner;
        library = NormalizeApplicationLocalName(library);
        return new InvalidOperationException(
            $"Unable to load native library '{library}' for entry point '{entryPoint}'. " +
            "Application-local libraries are loaded only from the executable directory; system-library names are resolved by the operating-system loader. " +
            "Check that the library exists for the current OS/architecture and that any dependent native libraries are also available.");
    }

    public static Exception EntryPointNotFound(string library, string entryPoint, Exception inner)
    {
        _ = inner;
        library = NormalizeApplicationLocalName(library);
        return new InvalidOperationException(
            $"Native library '{library}' was loaded, but entry point '{entryPoint}' was not found. " +
            "Check the selected Alias/OS/architecture-specific Alias and the exported symbol name.");
    }

    public static Exception WrongArchitecture(string library, string entryPoint, Exception inner)
    {
        _ = inner;
        library = NormalizeApplicationLocalName(library);
        return new InvalidOperationException(
            $"Native library '{library}' could not be loaded for entry point '{entryPoint}' because its binary format or architecture is incompatible with the running program. " +
            $"Current process architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}.");
    }
}
""";
}
