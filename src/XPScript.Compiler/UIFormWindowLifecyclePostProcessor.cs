namespace XPScript.Compiler;

internal sealed class UIFormWindowLifecyclePostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            "    private readonly List<XPScriptUIField> _fields = [];\n\n    internal XPScriptUIForm(string title, int? width, int? height, bool resizable)",
            "    private readonly List<XPScriptUIField> _fields = [];\n    private readonly string _instanceId = Guid.NewGuid().ToString(\"N\");\n    private bool _visible;\n    private bool _modal;\n\n    internal XPScriptUIForm(string title, int? width, int? height, bool resizable)",
            "form-instance-state");

        generated = ReplaceRequired(generated,
            "    public bool HasExplicitSize => _width.HasValue || _height.HasValue;\n    public object Data => _data;",
            "    public bool HasExplicitSize => _width.HasValue || _height.HasValue;\n    public bool Visible => XPScriptUIDesktopAdapter.TryIsVisible(_instanceId, _visible);\n    public bool Modal => _modal;\n    internal string InstanceId => _instanceId;\n    public object Data => _data;",
            "form-lifecycle-properties");

        generated = ReplaceRequired(generated,
            "    public string ShowDialog()\n    {",
            "    public void Show() => Show(false);\n\n    public void Show(object? modalValue)\n    {\n        var modal = Convert.ToBoolean(modalValue, System.Globalization.CultureInfo.CurrentCulture);\n        if (modal)\n        {\n            _ = ShowDialog();\n            return;\n        }\n\n        _modal = false;\n        _visible = true;\n        if (XPScriptUIDesktopAdapter.IsAvailable)\n        {\n            XPScriptUIDesktopAdapter.Show(this, _fields, _data, ApplyDesktopValue, ApplyDesktopValues);\n            return;\n        }\n        if (!XPScriptUIWebAdapter.IsAvailable)\n            throw new XPScriptRuntimeException(5, \"UIForm.Show requires a configured UI backend or an active XPScript web request.\");\n        XPScriptUIWebAdapter.WriteHtml(RenderWebForm(false));\n    }\n\n    public void Close()\n    {\n        _visible = false;\n        if (XPScriptUIDesktopAdapter.IsAvailable)\n        {\n            XPScriptUIDesktopAdapter.Close(_instanceId);\n            return;\n        }\n        if (XPScriptUIWebAdapter.IsAvailable)\n            XPScriptUIWebAdapter.WriteHtml(RenderWebClose());\n    }\n\n    public string ShowDialog()\n    {",
            "form-show-close-api");

        generated = ReplaceRequired(generated,
            "    private static string NormalizeFieldName(object? value)",
            "    private string RenderWebClose()\n    {\n        var id = System.Net.WebUtility.HtmlEncode(_instanceId);\n        return \"<script>(function(){var e=document.getElementById('xps_uiform_\" + id + \"');if(!e)return;if(window.bootstrap&&bootstrap.Modal){var m=bootstrap.Modal.getInstance(e);if(m)m.hide();}e.remove();})();</script>\";\n    }\n\n    private static string NormalizeFieldName(object? value)",
            "web-close-script");

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
