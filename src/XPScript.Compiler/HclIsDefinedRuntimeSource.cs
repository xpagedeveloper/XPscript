namespace XPScript.Compiler;

internal static class HclIsDefinedRuntimeSource
{
    public static readonly string Code = """
internal static class LSHclPlatformConstantRuntime
{
    public static bool IsDefined(object? value)
    {
        if (XPScriptNullRuntime.IsNull(value)) return false;
        var name = XPScriptRuntime.CStr(value).Trim().ToUpperInvariant();
        if (name.Length == 0) return false;

        return name switch
        {
            "WINDOWS" => OperatingSystem.IsWindows(),
            "WIN32" => OperatingSystem.IsWindows(),
            "WIN16" => false,
            "UNIX" => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD(),
            "LINUX" => OperatingSystem.IsLinux(),
            "MAC" => false,
            "MAC68K" => false,
            "MACPPC" => false,
            "OLE" => OperatingSystem.IsWindows(),
            _ => false
        };
    }
}
""" + "\n\n" + NotesRuntimeSource.Code;
}
