namespace XPScript.Compiler;

internal sealed class UIFormAdditionalFieldFixupPostProcessor
{
    private const string Sentinel = "internal static class XPScriptUIFieldBridgeRuntime";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(Sentinel, StringComparison.Ordinal)) return generated;
        if (!generated.Contains("AddFileField", StringComparison.Ordinal)) return generated;

        generated = generated.Replace(
            "XPScriptUIWebAdapter.FileJson(field.Name, field.MaxFileBytes)",
            "XPScriptUIFieldBridgeRuntime.FileJson(field.Name, field.MaxFileBytes, field.Multiple)",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "var obj = document.Root.AsObject() ?? throw new XPScriptRuntimeException(13, \"UIForm uploaded file metadata is invalid.\");\n        _data.Set(field.Name, obj);",
            "_data.Set(field.Name, document.Root.Value);",
            StringComparison.Ordinal);
        generated += "\n" + Runtime + "\n";
        return generated;
    }

    private const string Runtime = """
internal static class XPScriptUIFieldBridgeRuntime
{
    private const string BridgeTypeName = "XPScript.Web.Runtime.XpsUIWebRuntimeBridge, XPScript.Web.Runtime";

    public static string FileJson(string name, long maxFileBytes, bool multiple)
    {
        var type = Type.GetType(BridgeTypeName, throwOnError: false, ignoreCase: false);
        if (type is null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("XPScript.Web.Runtime.XpsUIWebRuntimeBridge", throwOnError: false, ignoreCase: false);
                if (type is not null) break;
            }
        }
        if (type is null) return string.Empty;
        var method = type.GetMethod("FileJson", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript web UI file bridge is incomplete.");
        try
        {
            return Convert.ToString(method.Invoke(null, [name, maxFileBytes, multiple]), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIForm file upload failed: " + ex.InnerException.Message);
        }
    }
}
""";
}
