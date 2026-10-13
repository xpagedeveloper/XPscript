namespace XPScript.Compiler;

/// <summary>Shared metadata storage; keys are the normal preprocessor's existing contract.</summary>
internal static class ApplicationMetadataRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationExecutableRuntime
{
    public static object? Icon { get => Get("icon"); set => Set("icon", value); }
    public static object? Product { get => Get("executable_product"); set => Set("executable_product", value); }
    public static object? Company { get => Get("executable_company"); set => Set("executable_company", value); }
    public static object? Version { get => Get("executable_version"); set => Set("executable_version", value); }
    public static object? Copyright { get => Get("executable_copyright"); set => Set("executable_copyright", value); }
    public static object? FileDescription { get => Get("executable_file_description"); set => Set("executable_file_description", value); }
    public static object? Comments { get => Get("executable_comments"); set => Set("executable_comments", value); }
    private static object? Get(string name) => XPScriptApplicationRuntime.State.Get("__xps_application_" + name);
    private static void Set(string name, object? value) => XPScriptApplicationRuntime.State.Set("__xps_application_" + name, value);
}
""";
}
