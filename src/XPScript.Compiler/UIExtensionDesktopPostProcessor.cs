using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIExtensionDesktopPostProcessor
{
    private const string InstalledRuntimeSentinel = "internal static class XPScriptUIDesktopAdapter";
    private const string BaseUiRuntimeSentinel = "internal static class XPScriptUI";
    private const string BridgeLookupOld = "    private static Type? BridgeType => Type.GetType(BridgeTypeName, throwOnError: false, ignoreCase: false);";
    private const string BridgeLookupNew = """
    private static Type? BridgeType
    {
        get
        {
            var direct = Type.GetType(BridgeTypeName, throwOnError: false, ignoreCase: false);
            if (direct is not null) return direct;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var candidate = assembly.GetType("XPScript.Web.Runtime.XpsUIWebRuntimeBridge", throwOnError: false, ignoreCase: false);
                if (candidate is not null) return candidate;
            }
            return null;
        }
    }
""";

    private static readonly Regex ShowDialogPattern = new(
        @"(?ms)^    public string ShowDialog\(\)\r?\n    \{\r?\n        if \(!XPScriptUIWebAdapter\.IsAvailable\).*?^    \}\r?\n\r?\n(?=    private XPScriptUIField AddField)",
        RegexOptions.CultureInvariant);

    private static readonly string[] DialogFunctions = ["ShowDialog", "LoadFileDialog", "OpenFileDialog", "SaveFileDialog"];

    private const string Replacement = """
    public string ShowDialog()
    {
        if (XPScriptUIWebAdapter.IsAvailable)
        {
            if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var field in _fields)
                {
                    if (field.Type == "MultiListBox") ApplySubmittedValues(field, XPScriptUIWebAdapter.FormValues(field.Name));
                    else ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
                }
                return "OK";
            }
            XPScriptUIWebAdapter.WriteHtml(RenderWebForm());
            return "Pending";
        }

        if (!XPScriptUIDesktopAdapter.IsAvailable)
            throw new XPScriptRuntimeException(5, "UIForm.ShowDialog requires a configured desktop UI backend or an active XPScript web request.");
        return XPScriptUIDesktopAdapter.ShowDialog(this, _fields, _data, ApplyDesktopValue, ApplyDesktopValues);
    }

    private void ApplyDesktopValue(XPScriptUIField field, string submitted)
    {
        if (field.Type == "CheckBox" && !_data.Contains(field.Name) && submitted.Equals("false", StringComparison.OrdinalIgnoreCase))
            return;
        ApplySubmittedValue(field, submitted);
    }

    private void ApplyDesktopValues(XPScriptUIField field, IReadOnlyList<string> submitted)
        => ApplySubmittedValues(field, submitted);

""";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        generated = NormalizeLineEndings(generated);

        if (generated.Contains(InstalledRuntimeSentinel, StringComparison.Ordinal))
            return generated;

        if (!NeedsUiExtensions(generated))
            return generated;

        var needsListView = NeedsListView(generated);

        var replaced = ShowDialogPattern.Replace(generated, Replacement, 1);
        if (ReferenceEquals(replaced, generated) || string.Equals(replaced, generated, StringComparison.Ordinal))
            throw new CompilerException("Unable to install the desktop UIForm runtime bridge into generated code.");

        replaced = RewriteDialogCalls(replaced);
        replaced = replaced
            + "\n" + UIExtensionDesktopRuntimeSource.Code
            + "\n" + UIDialogRuntimeSource.Code
            + "\n" + UIListViewRuntimeSource.Code
            + "\n";

        replaced = NormalizeLineEndings(replaced);

        replaced = new UIFormLayoutReactivePostProcessor().Transform(replaced);
        replaced = new UIFormActionModelPostProcessor().Transform(replaced);
        replaced = new UIFormEventDispatcherPostProcessor().Transform(replaced);
        replaced = new UIFormDesktopLayoutMetadataPostProcessor().Transform(replaced);
        replaced = new UIFormDesktopReactivePostProcessor().Transform(replaced);

        if (needsListView)
        {
            replaced = new UIListViewDesktopPostProcessor().Transform(replaced);
            replaced = new UIListViewEventPostProcessor().Transform(replaced);
            replaced = new UIListViewLiveUpdatePostProcessor().Transform(replaced);
            replaced = new UIListViewRowActionsPostProcessor().Transform(replaced);
            replaced = new UIListViewRowActionCompatibilityPostProcessor().Transform(replaced);
        }

        replaced = new UIFormWebPartialRefreshPostProcessor().Transform(replaced);
        replaced = new UIFormStructuralElementsPostProcessor().Transform(replaced);
        replaced = new UIFormRegexValidationPostProcessor().Transform(replaced);
        replaced = new UIFormDateRangeValidationPostProcessor().Transform(replaced);
        replaced = new UIFormTemporalRangeValidationPostProcessor().Transform(replaced);
        replaced = new UIWebBootstrapPostProcessor().Transform(replaced);
        return HardenWebBridgeLookup(replaced);
    }

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string HardenWebBridgeLookup(string generated)
    {
        if (generated.Contains(BridgeLookupNew, StringComparison.Ordinal)) return generated;
        if (!generated.Contains(BridgeLookupOld, StringComparison.Ordinal))
            throw new CompilerException("Unable to harden generated XPScript web UI bridge lookup.");
        return generated.Replace(BridgeLookupOld, BridgeLookupNew, StringComparison.Ordinal);
    }

    private static bool NeedsUiExtensions(string generated)
    {
        var runtimeIndex = generated.IndexOf(BaseUiRuntimeSentinel, StringComparison.Ordinal);
        var scriptPart = runtimeIndex >= 0 ? generated[..runtimeIndex] : generated;

        if (scriptPart.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal) ||
            scriptPart.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal))
            return true;

        foreach (var function in DialogFunctions)
        {
            if (Regex.IsMatch(
                    scriptPart,
                    $@"(?<![A-Za-z0-9_\.]){Regex.Escape(function)}\s*\(",
                    RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }

    private static bool NeedsListView(string generated)
    {
        var runtimeIndex = generated.IndexOf(BaseUiRuntimeSentinel, StringComparison.Ordinal);
        var scriptPart = runtimeIndex >= 0 ? generated[..runtimeIndex] : generated;
        return scriptPart.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal) ||
               scriptPart.Contains("XPScriptUIListView", StringComparison.Ordinal);
    }

    private static string RewriteDialogCalls(string source)
    {
        var output = new StringBuilder(source.Length + 128);
        var inString = false;
        var inChar = false;
        var escaped = false;

        for (var i = 0; i < source.Length;)
        {
            var c = source[i];
            if (escaped)
            {
                output.Append(c);
                escaped = false;
                i++;
                continue;
            }
            if ((inString || inChar) && c == '\\')
            {
                output.Append(c);
                escaped = true;
                i++;
                continue;
            }
            if (!inChar && c == '"')
            {
                inString = !inString;
                output.Append(c);
                i++;
                continue;
            }
            if (!inString && c == '\'')
            {
                inChar = !inChar;
                output.Append(c);
                i++;
                continue;
            }

            if (!inString && !inChar)
            {
                var replaced = false;
                foreach (var function in DialogFunctions)
                {
                    if (i + function.Length > source.Length ||
                        !source.AsSpan(i, function.Length).Equals(function, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var beforeOk = i == 0 || !(char.IsLetterOrDigit(source[i - 1]) || source[i - 1] is '_' or '.');
                    var after = i + function.Length;
                    while (after < source.Length && char.IsWhiteSpace(source[after])) after++;
                    if (!beforeOk || after >= source.Length || source[after] != '(' || LooksLikeMethodDeclaration(source, i))
                        continue;

                    output.Append("XPScriptUIDialogRuntime.").Append(function);
                    i += function.Length;
                    replaced = true;
                    break;
                }
                if (replaced) continue;
            }

            output.Append(c);
            i++;
        }

        return output.ToString();
    }

    private static bool LooksLikeMethodDeclaration(string source, int nameIndex)
    {
        var lineStart = source.LastIndexOf('\n', Math.Max(0, nameIndex - 1));
        var prefix = source[(lineStart + 1)..nameIndex].Trim();
        return Regex.IsMatch(
            prefix,
            @"^(?:(?:public|private|protected|internal|static|virtual|override|sealed|async)\s+)*(?:void|string|object|dynamic|bool|byte|short|int|long|float|double|decimal|DateTime|Task(?:<[^>]+>)?)\s+$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
