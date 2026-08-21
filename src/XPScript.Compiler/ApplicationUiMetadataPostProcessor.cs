using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ApplicationUiMetadataPostProcessor
{
    private const string NewRequestTitle = """
            title = string.IsNullOrWhiteSpace(XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_title")))
                ? form.Title
                : XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_title")),
            applicationTitle = XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_title")),
            applicationIcon = XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_icon")),
""";

    private const string OldWebWrite = "            XPScriptUIWebAdapter.WriteHtml(RenderWebForm());";
    private const string NewWebWrite = "            XPScriptUIWebAdapter.WriteHtml(XPScriptApplicationMetadataRuntime.WrapWebHtml(RenderWebForm()));";

    private const string RuntimeCode = """

internal static class XPScriptApplicationMetadataRuntime
{
    public static string WrapWebHtml(string html)
    {
        var title = XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_title"));
        var icon = XPScriptRuntime.CStr(XPScriptApplicationRuntime.State.Get("__xps_application_icon"));
        if (title.Length == 0 && icon.Length == 0) return html;

        var prefix = new System.Text.StringBuilder();
        if (title.Length > 0)
            prefix.Append("<title>").Append(System.Net.WebUtility.HtmlEncode(title)).Append("</title>");
        if (icon.Length > 0)
            prefix.Append("<link rel=\"icon\" href=\"").Append(System.Net.WebUtility.HtmlEncode(icon)).Append("\">");
        return prefix.Append(html).ToString();
    }
}
""";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (!generated.Contains("applicationTitle = XPScriptRuntime.CStr", StringComparison.Ordinal))
        {
            var titlePattern = new Regex(
                @"(?m)^(?<indent>\s*)title\s*=\s*form\.Title\s*,\s*$",
                RegexOptions.CultureInvariant);
            var match = titlePattern.Match(generated);
            if (!match.Success)
                throw new CompilerException("Unable to install Application.Title/Application.Icon UI metadata.");

            var indent = match.Groups["indent"].Value;
            var replacement = string.Join(Environment.NewLine,
                NewRequestTitle.Replace("            ", indent, StringComparison.Ordinal).TrimEnd('\r', '\n'));
            generated = titlePattern.Replace(generated, replacement, 1);
        }

        if (generated.Contains(OldWebWrite, StringComparison.Ordinal))
            generated = generated.Replace(OldWebWrite, NewWebWrite, StringComparison.Ordinal);

        if (!generated.Contains("internal static class XPScriptApplicationMetadataRuntime", StringComparison.Ordinal))
            generated += RuntimeCode;

        return generated;
    }
}
