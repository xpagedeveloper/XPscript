namespace XPScript.Compiler;

internal sealed class UIFormWindowLifecyclePostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        const string marker = "internal sealed partial class XPScriptUIForm";
        var classStart = generated.IndexOf(marker, StringComparison.Ordinal);
        if (classStart < 0) return generated;
        var bodyStart = generated.IndexOf('{', classStart);
        var classEnd = FindClassEnd(generated, bodyStart);
        if (bodyStart < 0 || classEnd < 0) return generated;
        var formSource = generated[classStart..classEnd];
        var transformed = TransformForm(formSource);
        return generated[..classStart] + transformed + generated[classEnd..];
    }

    private static int FindClassEnd(string source, int openingBrace)
    {
        if (openingBrace < 0) return -1;
        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = openingBrace; i < source.Length; i++)
        {
            var c = source[i];
            if (inString)
            {
                if (c == '"' && !escaped) inString = false;
                escaped = c == '\\' && !escaped;
                if (c != '\\') escaped = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return i + 1;
        }
        return -1;
    }

    private string TransformForm(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (!generated.Contains("private readonly string _instanceId", StringComparison.Ordinal))
            generated = ReplaceRequired(generated,
                "    private readonly List<XPScriptUIField> _fields = [];\n    private readonly List<(string Name, string Command)> _mediaCommands = [];\n\n    internal XPScriptUIForm(string title, int? width, int? height, bool resizable)",
                "    private readonly List<XPScriptUIField> _fields = [];\n    private readonly List<(string Name, string Command)> _mediaCommands = [];\n    private readonly string _instanceId = Guid.NewGuid().ToString(\"N\");\n    private bool _visible;\n    private bool _modal;\n\n    internal XPScriptUIForm(string title, int? width, int? height, bool resizable)",
                "form-instance-state");

        if (!generated.Contains("internal string InstanceId", StringComparison.Ordinal))
            generated = ReplaceRequired(generated,
                "    public bool HasExplicitSize => _width.HasValue || _height.HasValue;\n    public object Data => _data;",
                "    public bool HasExplicitSize => _width.HasValue || _height.HasValue;\n    public bool Visible => XPScriptUIDesktopAdapter.TryIsVisible(_instanceId, _visible);\n    public bool Modal => _modal;\n    internal string InstanceId => _instanceId;\n    public object Data => _data;",
                "form-lifecycle-properties");

        if (!generated.Contains("private void ApplyDesktopValue(", StringComparison.Ordinal))
            generated = ReplaceRequired(generated,
                "    public string ShowDialog()\n    {",
                "    private void ApplyDesktopValue(XPScriptUIField field, string submitted) => ApplySubmittedValue(field, submitted);\n    private void ApplyDesktopValues(XPScriptUIField field, IReadOnlyList<string> submitted) => ApplySubmittedValues(field, submitted);\n    private string RenderWebForm(bool includeButtons) => RenderWebForm();\n\n    public string ShowDialog()\n    {",
                "form-desktop-helpers");
        if (!generated.Contains("private string RenderWebForm(bool", StringComparison.Ordinal))
            generated = ReplaceRequired(generated,
                "    public string ShowDialog()\n    {",
                "    private string RenderWebForm(bool includeButtons) => RenderWebForm();\n\n    public string ShowDialog()\n    {",
                "form-web-render-overload");

        if (!generated.Contains("public void Show()", StringComparison.Ordinal))
            generated = ReplaceRequired(generated,
                "    public string ShowDialog()\n    {",
                "    public void Show() => Show(false);\n\n    public void Show(object? modalValue)\n    {\n        var modal = Convert.ToBoolean(modalValue, System.Globalization.CultureInfo.CurrentCulture);\n        if (modal)\n        {\n            _ = ShowDialog();\n            return;\n        }\n\n        _modal = false;\n        _visible = true;\n        if (XPScriptUIDesktopAdapter.IsAvailable)\n        {\n            XPScriptUIDesktopAdapter.Show(this, _fields, _data, ApplyDesktopValue, ApplyDesktopValues);\n            return;\n        }\n        if (!XPScriptUIWebAdapter.IsAvailable)\n            throw new XPScriptRuntimeException(5, \"UIForm.Show requires a configured UI backend or an active XPScript web request.\");\n        XPScriptUIWebAdapter.WriteHtml(RenderWebForm(false));\n    }\n\n    public void Close()\n    {\n        _visible = false;\n        if (XPScriptUIDesktopAdapter.IsAvailable)\n        {\n            XPScriptUIDesktopAdapter.Close(_instanceId);\n            return;\n        }\n        if (XPScriptUIWebAdapter.IsAvailable)\n            XPScriptUIWebAdapter.WriteHtml(RenderWebClose());\n    }\n\n    public void Hide() => Close();\n\n    public string ShowDialog()\n    {",
                "form-show-close-api");

        if (!generated.Contains("private string RenderWebClose()", StringComparison.Ordinal))
            generated = ReplaceRequired(generated,
                "    private static string NormalizeFieldName(object? value)",
                "    private string RenderWebClose()\n    {\n        var id = System.Net.WebUtility.HtmlEncode(_instanceId);\n        return \"<script>(function(){var e=document.getElementById('xps_uiform_\" + id + \"');if(!e)return;if(window.bootstrap&&bootstrap.Modal){var m=bootstrap.Modal.getInstance(e);if(m)m.hide();}e.remove();})();</script>\";\n    }\n\n    private static string NormalizeFieldName(object? value)",
                "web-close-script");

        if (!generated.Contains("public void RefreshForm()", StringComparison.Ordinal))
        {
            const string marker = "    public void Close()\n";
            const string refresh = "    public void RefreshForm()\n    {\n        XPScriptUI.Activate(this);\n        if (XPScriptUIDesktopAdapter.IsAvailable)\n        {\n            XPScriptUIDesktopAdapter.Show(this, _fields, _data, ApplyDesktopValue, ApplyDesktopValues);\n            return;\n        }\n        if (XPScriptUIWebAdapter.IsAvailable)\n            XPScriptUIWebAdapter.WriteHtml(RenderWebForm(false));\n    }\n\n";
            if (!generated.Contains(marker, StringComparison.Ordinal))
                throw new CompilerException("Unable to install UIForm window lifecycle (form-refresh-api).");
            generated = generated.Replace(marker, refresh + marker, StringComparison.Ordinal);
        }

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        var index = source.IndexOf(oldValue, StringComparison.Ordinal);
        if (index < 0)
            throw new CompilerException("Unable to install UIForm window lifecycle (" + stage + ").");
        return source[..index] + newValue + source[(index + oldValue.Length)..];
    }
}
