using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIExtensionDesktopPostProcessor
{
    private readonly NotesRuntimeFeatures _notesRuntimeFeatures;

    public UIExtensionDesktopPostProcessor()
        : this(NotesRuntimeFeatures.Full)
    {
    }

    internal UIExtensionDesktopPostProcessor(NotesRuntimeFeatures notesRuntimeFeatures)
    {
        _notesRuntimeFeatures = notesRuntimeFeatures;
    }

    private const string InstalledRuntimeSentinel = "internal static class XPScriptUIDesktopAdapter";
    private const string BaseUiRuntimeSentinel = "internal static class XPScriptUI";
    private const string RuntimeMsgBoxCall = "XPScriptRuntime.MsgBox(";
    private const string DesktopMsgBoxCall = "XPScriptUIDialogRuntime.MsgBox(";
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
        _modal = true;
        _visible = true;
        if (XPScriptUIWebAdapter.IsAvailable)
        {
            if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var field in _fields)
                {
                    if (field.Type == "MultiListBox") ApplySubmittedValues(field, XPScriptUIWebAdapter.FormValues(field.Name));
                    else ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
                }
                _visible = false;
                return "OK";
            }
            XPScriptUIWebAdapter.WriteHtml(RenderWebForm());
            return "Pending";
        }

        try
        {
            if (!XPScriptUIDesktopAdapter.IsAvailable)
                throw new XPScriptRuntimeException(5, "UIForm.ShowDialog requires a configured UI backend or an active XPScript web request.");
            return XPScriptUIDesktopAdapter.ShowDialog(this, _fields, _data, ApplyDesktopValue, ApplyDesktopValues);
        }
        finally { _visible = false; }
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

    internal static string InstallModalBridge(string generated)
    {
        var replaced = ShowDialogPattern.Replace(generated, Replacement, 1);
        if (string.Equals(replaced, generated, StringComparison.Ordinal))
            throw new CompilerException("Unable to install the UIForm modal runtime bridge into generated code.");
        return replaced;
    }

    public string Transform(string generated, bool browserWasm = false, bool browserWasmServer = false)
    {
        ArgumentNullException.ThrowIfNull(generated);

        var hasNotesRuntime = generated.Contains("internal static class XPScriptNotes", StringComparison.Ordinal);
        var needsNotesRuntime = !hasNotesRuntime &&
                                generated.Contains("XPScriptNotes.", StringComparison.Ordinal);
        var hasDesktopRuntime = generated.Contains(InstalledRuntimeSentinel, StringComparison.Ordinal);
        var needsUiExtensions = !hasDesktopRuntime && NeedsUiExtensions(generated);

        // Most programs use neither Notes nor desktop UI. Avoid copying and scanning the
        // complete generated runtime when this postprocessor has no work to perform.
        if (!needsNotesRuntime && !hasNotesRuntime && !needsUiExtensions)
            return generated;

        generated = NormalizeLineEndings(generated);

        if (needsNotesRuntime)
            generated += "\n" + NotesRuntimeSourceBuilder.Build(_notesRuntimeFeatures) + "\n";

        if (hasNotesRuntime || needsNotesRuntime)
            generated = NotesComputeWithFormByRefCallPostProcessor.Transform(generated);

        if (hasDesktopRuntime)
            return generated;

        if (!needsUiExtensions)
            return generated;

        var needsListView = NeedsListView(generated);

        var replaced = generated.Contains("public string ShowDialog()", StringComparison.Ordinal)
            ? InstallModalBridge(generated)
            : generated;

        replaced = RewriteDialogCalls(replaced);
        replaced = replaced
            + "\n" + UIExtensionDesktopRuntimeSource.Code
            + "\n" + (generated.Contains("class XPScriptUIDialogRuntime", StringComparison.Ordinal) ? string.Empty : UIDialogRuntimeSource.Code)
            + "\n" + UIListViewRuntimeSource.Code
            + "\n";

        replaced = NormalizeLineEndings(replaced);
        if (!replaced.Contains("class XPScriptUIForm", StringComparison.Ordinal))
            return replaced;

        // Browser/WASM has a dedicated JavaScript/component renderer. The
        // desktop post-processors below splice methods into the retired
        // desktop renderer and are not valid against the browser runtime's
        // component model.
        if (browserWasm)
        {
            // Browser-WASM uses the JavaScript renderer, but it still needs
            // the generated event dispatcher so a browser callback can invoke
            // local XPscript code and then cross a [ServerSide] boundary.
            return HardenWebBridgeLookup(replaced);
        }

        replaced = new UIFormWindowLifecyclePostProcessor().Transform(replaced);
        replaced = new UIFormLayoutReactivePostProcessor().Transform(replaced);
        replaced = new UIFormActionModelPostProcessor().Transform(replaced);
        replaced = new UIFormCallbackModelPostProcessor().Transform(replaced);
        replaced = new UIFormNavigationCompatibilityPostProcessor().Transform(replaced);
        replaced = new UIFormEventDispatcherPostProcessor().Transform(replaced);
        replaced = new UIFormDesktopLayoutMetadataPostProcessor().Transform(replaced);
        replaced = new UIFormDesktopReactivePostProcessor().Transform(replaced);

        if (needsListView)
        {
            replaced = new UIListViewDesktopPostProcessor().Transform(replaced);
            replaced = new UIListViewEventPostProcessor().Transform(replaced);
            replaced = new UIListViewCallbackRuntimePostProcessor().Transform(replaced);
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
        replaced = new UIFormAdditionalFieldPreparePostProcessor().Transform(replaced);
        replaced = new UIFormAdditionalFieldsPostProcessor().Transform(replaced);
        replaced = new UIFormAdditionalFieldFixupPostProcessor().Transform(replaced);
        replaced = new ApplicationUiMetadataPostProcessor().Transform(replaced);
        if (!browserWasmServer)
            replaced = new UIFormWebWindowLifecyclePostProcessor().Transform(replaced);
        replaced = NormalizeAccessibilityRequestMarkers(replaced);
        replaced = new UIFormAccessibilityPostProcessor().Transform(replaced);
        return HardenWebBridgeLookup(replaced);
    }

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string NormalizeAccessibilityRequestMarkers(string generated)
    {
        generated = Regex.Replace(
            generated,
            @"(?m)^[ \t]*resizable\s*=\s*form\.Resizable\s*,[ \t]*$",
            "            resizable = form.Resizable,",
            RegexOptions.CultureInvariant);
        return generated.Replace(
            "html.Append(\"<button style=\\\"grid-column:1/-1\\\" type=\\\"submit\\\"",
            "html.Append(\"<button type=\\\"submit\\\" style=\\\"grid-column:1/-1\\\"",
            StringComparison.Ordinal);
    }

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

        if (generated.Contains("XPScriptUI.CreateForm(", StringComparison.Ordinal) ||
            generated.Contains("new XPScriptUIForm(", StringComparison.Ordinal) ||
            scriptPart.Contains("XPScriptUIList.CreateListView(", StringComparison.Ordinal) ||
            scriptPart.Contains(RuntimeMsgBoxCall, StringComparison.Ordinal))
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
        source = source.Replace(RuntimeMsgBoxCall, DesktopMsgBoxCall, StringComparison.Ordinal);

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
            @"^(?:(?:public|private|protected|internal|static|virtual|override|sealed|async)\s+)*(?:void|string|object|dynamic|bool|byte|short|int|long|float|double|decimal|DateTime|Task(?:<[^>]+>)?)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
