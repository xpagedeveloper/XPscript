namespace XPScript.Compiler;

internal sealed class UIFormAdditionalFieldFixupPostProcessor
{
    private const string Sentinel = "internal static class XPScriptUIFieldBridgeRuntime";
    private const string RenderMarker = "    private string RenderWebForm()";
    private const string ReturnMarker = "        return html.ToString();";
    private const string TinyMarker = "tinymce.init({selector:'textarea.xpscript-richtext'";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains("AddFileField", StringComparison.Ordinal)) return generated;

        generated = new UIFormAdditionalFieldValidationRepairPostProcessor().Transform(generated);
        generated = generated.Replace(
            "XPScriptUIWebAdapter.FileJson(field.Name, field.MaxFileBytes)",
            "XPScriptUIFieldBridgeRuntime.FileJson(field.Name, field.MaxFileBytes, field.Multiple)",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "var obj = document.Root.AsObject() ?? throw new XPScriptRuntimeException(13, \"UIForm uploaded file metadata is invalid.\");\n        _data.Set(field.Name, obj);",
            "_data.Set(field.Name, document.Root.Value);",
            StringComparison.Ordinal);

        generated = InstallTinyMceInActualRenderer(generated);

        if (!generated.Contains(Sentinel, StringComparison.Ordinal))
            generated += "\n" + Runtime + "\n";

        generated = new UIFormRemoteSearchPostProcessor().Transform(generated);
        return generated;
    }

    private static string InstallTinyMceInActualRenderer(string generated)
    {
        var renderIndex = generated.IndexOf(RenderMarker, StringComparison.Ordinal);
        if (renderIndex < 0) throw new CompilerException("Unable to locate UIForm web renderer for rich-text finalization.");
        var returnIndex = generated.IndexOf(ReturnMarker, renderIndex, StringComparison.Ordinal);
        if (returnIndex < 0) throw new CompilerException("Unable to locate UIForm web renderer return for rich-text finalization.");

        var renderSegment = generated[renderIndex..returnIndex];
        if (renderSegment.Contains(TinyMarker, StringComparison.Ordinal)) return generated;

        const string hook = """
        if (_fields.Any(f => f.Type == "RichTextField"))
        {
            var tinyMceUrl = XPScriptUIFieldBridgeRuntime.TinyMceScriptUrl();
            html.Append("<script src=\"").Append(System.Net.WebUtility.HtmlEncode(tinyMceUrl)).Append("\" referrerpolicy=\"origin\"></script>");
            html.Append("<script>tinymce.init({selector:'textarea.xpscript-richtext',plugins:'lists link table code',toolbar:'undo redo | blocks | bold italic | bullist numlist | link table | code'});</script>");
        }
""";
        return generated.Insert(returnIndex, hook);
    }

    private const string Runtime = """
internal static class XPScriptUIFieldBridgeRuntime
{
    private const string BridgeTypeName = "XPScript.Web.Runtime.XpsUIWebRuntimeBridge, XPScript.Web.Runtime";
    private const string DefaultTinyMceCloudUrl = "https://cdn.tiny.cloud/1/no-api-key/tinymce/7/tinymce.min.js";

    public static string TinyMceScriptUrl()
    {
        var environmentMode = Environment.GetEnvironmentVariable("XPSCRIPT_TINYMCE_MODE");
        var environmentUrl = Environment.GetEnvironmentVariable("XPSCRIPT_TINYMCE_SCRIPT_URL");
        var configured = ReadTinyMceConfiguration();

        var mode = string.IsNullOrWhiteSpace(environmentMode) ? configured.Mode : environmentMode.Trim();
        var url = string.IsNullOrWhiteSpace(environmentUrl) ? configured.ScriptUrl : environmentUrl.Trim();
        if (string.IsNullOrWhiteSpace(mode)) mode = "cloud";

        if (!mode.Equals("cloud", StringComparison.OrdinalIgnoreCase) &&
            !mode.Equals("local", StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(5, "TinyMCE mode must be 'cloud' or 'local'.");

        if (mode.Equals("cloud", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(url))
            url = DefaultTinyMceCloudUrl;

        if (string.IsNullOrWhiteSpace(url))
            throw new XPScriptRuntimeException(5, "TinyMCE local mode requires tinyMce.scriptUrl in xpscript-ui.json or XPSCRIPT_TINYMCE_SCRIPT_URL.");

        url = url.Trim();
        if (url.StartsWith("/", StringComparison.Ordinal)) return url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new XPScriptRuntimeException(5, "TinyMCE scriptUrl must be an HTTPS URL or an application-relative path beginning with '/'.");
        return url;
    }

    private static (string Mode, string ScriptUrl) ReadTinyMceConfiguration()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.CurrentDirectory, "xpscript-ui.json"),
            Path.Combine(AppContext.BaseDirectory, "xpscript-ui.json")
        };
        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                if (!document.RootElement.TryGetProperty("tinyMce", out var tinyMce) || tinyMce.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return (string.Empty, string.Empty);
                var mode = tinyMce.TryGetProperty("mode", out var modeValue) && modeValue.ValueKind == System.Text.Json.JsonValueKind.String
                    ? modeValue.GetString() ?? string.Empty
                    : string.Empty;
                var scriptUrl = tinyMce.TryGetProperty("scriptUrl", out var urlValue) && urlValue.ValueKind == System.Text.Json.JsonValueKind.String
                    ? urlValue.GetString() ?? string.Empty
                    : string.Empty;
                return (mode.Trim(), scriptUrl.Trim());
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new XPScriptRuntimeException(5, "Unable to parse xpscript-ui.json: " + ex.Message);
            }
            catch (IOException ex)
            {
                throw new XPScriptRuntimeException(5, "Unable to read xpscript-ui.json: " + ex.Message);
            }
        }
        return (string.Empty, string.Empty);
    }

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
