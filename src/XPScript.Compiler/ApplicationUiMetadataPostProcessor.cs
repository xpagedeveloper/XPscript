namespace XPScript.Compiler;

internal sealed class ApplicationUiMetadataPostProcessor
{
    private const string OldRequestTitle = "            title = form.Title,";
    private const string NewRequestTitle = """
            title = string.IsNullOrWhiteSpace(XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_title")))
                ? form.Title
                : XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_title")),
            applicationTitle = XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_title")),
            applicationIcon = XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_icon")),
""";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains("applicationTitle = XPScriptRuntime.CStr", StringComparison.Ordinal)) return generated;
        if (!generated.Contains(OldRequestTitle, StringComparison.Ordinal))
            throw new CompilerException("Unable to install Application.Title/Application.Icon UI metadata.");
        return generated.Replace(OldRequestTitle, NewRequestTitle, StringComparison.Ordinal);
    }
}
