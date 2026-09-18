namespace XPScript.Compiler;

internal sealed class UIFormAccessibilityPostProcessor
{
    private const string Sentinel = "public string AccessibleName { get; set; }";
    private const string BaseUiRuntimeSentinel = "internal static class XPScriptUI";

    private static readonly string[] FeatureTokens =
    [
        ".AccessibleName", ".AccessibleDescription", ".AccessibleHelpText", ".AccessibleLive",
        ".AccessibilityHidden", ".Focusable", ".IsTabStop", ".TabIndex", ".HasFocus",
        ".AccessKey", ".HotKey", ".InitialFocus", ".FocusedField", ".Focus(",
        ".FocusFirst(", ".FocusFirstInvalid(", ".FocusNext(", ".FocusPrevious(",
        ".ValidationErrors", ".HasValidationErrors", ".SetValidationError(",
        ".ClearValidationError(", ".GetValidationErrors(", ".ValidationSummary",
        ".FocusFirstError", ".AnnounceValidationErrors", ".Announce("
    ];

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(Sentinel, StringComparison.Ordinal)) return generated;
        if (!NeedsAccessibilityRuntime(generated)) return generated;

        generated = ReplaceOnceRequired(
            generated,
            "        Type = type;\n    }",
            Block(
                "        Type = type;",
                "        var interactive = type is not (\"HiddenField\" or \"Separator\" or \"Spacer\" or \"Heading\");",
                "        Focusable = interactive;",
                "        IsTabStop = interactive;",
                "    }"));

        generated = ReplaceOnceRequired(
            generated,
            "    public List<string> Options { get; } = [];\n",
            Block(
                "    public List<string> Options { get; } = [];",
                "    public string AccessibleName { get; set; } = string.Empty;",
                "    public string AccessibleDescription { get; set; } = string.Empty;",
                "    public string AccessibleHelpText { get; set; } = string.Empty;",
                "    public string AccessibleLive { get; set; } = \"Off\";",
                "    public bool AccessibilityHidden { get; set; }",
                "    public bool Focusable { get; set; }",
                "    public bool IsTabStop { get; set; }",
                "    public int TabIndex { get; set; }",
                "    public string AccessKey { get; set; } = string.Empty;",
                "    public string HotKey { get; set; } = string.Empty;",
                "    public string ValidationError { get; internal set; } = string.Empty;",
                "    internal Action<string>? AccessibilityFocusHandler { get; set; }",
                "    internal Func<string, bool>? AccessibilityFocusQuery { get; set; }",
                "    public bool HasFocus => AccessibilityFocusQuery?.Invoke(Name) == true;",
                "    public void Focus() => AccessibilityFocusHandler?.Invoke(Name);",
                string.Empty));

        generated = ReplaceOnceRequired(
            generated,
            "    private readonly List<XPScriptUIField> _fields = [];\n",
            Block(
                "    private readonly List<XPScriptUIField> _fields = [];",
                "    private string _initialFocus = string.Empty;",
                "    private string _announcement = string.Empty;",
                "    private string _announcementPriority = \"Polite\";",
                string.Empty));

        generated = ReplaceOnceRequired(
            generated,
            "    public int FieldCount => _fields.Count;\n",
            Block(
                "    public int FieldCount => _fields.Count;",
                "    public string InitialFocus { get => _initialFocus; set => _initialFocus = NormalizeOptionalFieldName(value); }",
                "    public string FocusedField => XPScriptUIDesktopAdapter.TryGetFocusedField(InstanceId, _initialFocus);",
                "    public bool ValidationSummary { get; set; } = true;",
                "    public bool FocusFirstError { get; set; } = true;",
                "    public bool AnnounceValidationErrors { get; set; } = true;",
                "    public bool HasValidationErrors => _fields.Any(candidate => candidate.ValidationError.Length > 0);",
                "    internal string AccessibilityAnnouncement => _announcement;",
                "    internal string AccessibilityAnnouncementPriority => _announcementPriority;",
                "    public object ValidationErrors",
                "    {",
                "        get",
                "        {",
                "            var result = XPScriptNativeJson.CreateArray();",
                "            foreach (var validationField in _fields.Where(candidate => candidate.ValidationError.Length > 0))",
                "            {",
                "                var error = XPScriptNativeJson.CreateObject();",
                "                error.Set(\"fieldName\", validationField.Name);",
                "                error.Set(\"message\", validationField.ValidationError);",
                "                error.Set(\"code\", \"validation\");",
                "                error.Set(\"severity\", \"Error\");",
                "                result.Add(error);",
                "            }",
                "            return result;",
                "        }",
                "    }",
                string.Empty,
                "    public void Focus(object? name)",
                "    {",
                "        var field = FindField(name);",
                "        if (!field.Focusable) throw new XPScriptRuntimeException(5, $\"UIForm field '{field.Name}' is not focusable.\");",
                "        _initialFocus = field.Name;",
                "        XPScriptUIDesktopAdapter.FocusField(InstanceId, field.Name);",
                "    }",
                string.Empty,
                "    public void FocusFirst()",
                "    {",
                "        var field = OrderedFocusableFields().FirstOrDefault();",
                "        if (field is not null) Focus(field.Name);",
                "    }",
                string.Empty,
                "    public void FocusFirstInvalid()",
                "    {",
                "        var field = OrderedFocusableFields().FirstOrDefault(candidate => candidate.ValidationError.Length > 0);",
                "        if (field is not null) Focus(field.Name);",
                "    }",
                string.Empty,
                "    public void FocusNext() => MoveFocus(1);",
                "    public void FocusPrevious() => MoveFocus(-1);",
                string.Empty,
                "    public void SetValidationError(object? name, object? message)",
                "    {",
                "        var field = FindField(name);",
                "        field.ValidationError = XPScriptRuntime.CStr(message).Trim();",
                "    }",
                string.Empty,
                "    public void ClearValidationError(object? name) => FindField(name).ValidationError = string.Empty;",
                string.Empty,
                "    public object GetValidationErrors(object? name)",
                "    {",
                "        var field = FindField(name);",
                "        var result = XPScriptNativeJson.CreateArray();",
                "        if (field.ValidationError.Length > 0) result.Add(field.ValidationError);",
                "        return result;",
                "    }",
                string.Empty,
                "    public void Announce(object? message) => Announce(message, \"Polite\");",
                "    public void Announce(object? message, object? priority)",
                "    {",
                "        _announcement = XPScriptRuntime.CStr(message);",
                "        _announcementPriority = NormalizeAnnouncementPriority(priority);",
                "        XPScriptUIDesktopAdapter.Announce(InstanceId, _announcement, _announcementPriority);",
                "    }",
                string.Empty,
                "    private IReadOnlyList<XPScriptUIField> OrderedFocusableFields()",
                "        => _fields.Where(field => field.Focusable && field.IsTabStop && !field.AccessibilityHidden)",
                "            .OrderBy(field => field.TabIndex)",
                "            .ThenBy(field => _fields.IndexOf(field))",
                "            .ToArray();",
                string.Empty,
                "    private void MoveFocus(int direction)",
                "    {",
                "        var fields = OrderedFocusableFields();",
                "        if (fields.Count == 0) return;",
                "        var current = FocusedField;",
                "        var index = -1;",
                "        for (var i = 0; i < fields.Count; i++) if (fields[i].Name.Equals(current, StringComparison.OrdinalIgnoreCase)) { index = i; break; }",
                "        index = direction > 0 ? (index + 1 + fields.Count) % fields.Count : (index - 1 + fields.Count) % fields.Count;",
                "        Focus(fields[index].Name);",
                "    }",
                string.Empty,
                "    private static string NormalizeOptionalFieldName(object? value)",
                "    {",
                "        var text = XPScriptRuntime.CStr(value).Trim();",
                "        if (text.Length == 0) return string.Empty;",
                "        return NormalizeFieldName(text);",
                "    }",
                string.Empty,
                "    private static string NormalizeAnnouncementPriority(object? value)",
                "    {",
                "        var text = XPScriptRuntime.CStr(value).Trim();",
                "        if (text.Length == 0 || text.Equals(\"Polite\", StringComparison.OrdinalIgnoreCase)) return \"Polite\";",
                "        if (text.Equals(\"Assertive\", StringComparison.OrdinalIgnoreCase)) return \"Assertive\";",
                "        throw new XPScriptRuntimeException(5, \"UIForm announcement priority must be Polite or Assertive.\");",
                "    }",
                string.Empty));

        generated = ReplaceOnceRequired(
            generated,
            "        var field = new XPScriptUIField(this, fieldName, XPScriptRuntime.CStr(label), type);\n        _fields.Add(field);",
            Block(
                "        var field = new XPScriptUIField(this, fieldName, XPScriptRuntime.CStr(label), type);",
                "        field.AccessibilityFocusHandler = fieldToFocus => Focus(fieldToFocus);",
                "        field.AccessibilityFocusQuery = fieldToQuery => FocusedField.Equals(fieldToQuery, StringComparison.OrdinalIgnoreCase);",
                "        _fields.Add(field);"));

        generated = ReplaceOnceRequired(
            generated,
            "                required = field.Required,\n",
            Block(
                "                required = field.Required,",
                "                accessibleName = field.AccessibleName,",
                "                accessibleDescription = field.AccessibleDescription,",
                "                accessibleHelpText = field.AccessibleHelpText,",
                "                accessibleLive = field.AccessibleLive,",
                "                accessibilityHidden = field.AccessibilityHidden,",
                "                focusable = field.Focusable,",
                "                isTabStop = field.IsTabStop,",
                "                tabIndex = field.TabIndex,",
                "                accessKey = field.AccessKey,",
                "                hotKey = field.HotKey,",
                "                validationError = field.ValidationError,",
                string.Empty));

        generated = ReplaceOnceRequired(
            generated,
            "            resizable = form.Resizable,\n",
            Block(
                "            resizable = form.Resizable,",
                "            initialFocus = form.InitialFocus,",
                "            validationSummary = form.ValidationSummary,",
                "            focusFirstError = form.FocusFirstError,",
                "            announceValidationErrors = form.AnnounceValidationErrors,",
                "            announcement = form.AccessibilityAnnouncement,",
                "            announcementPriority = form.AccessibilityAnnouncementPriority,",
                string.Empty));

        generated = ReplaceOnceRequired(
            generated,
            "    public static bool TryIsVisible(string instanceId, bool fallback)\n    {",
            Block(
                "    public static void FocusField(string instanceId, string fieldName)",
                "    {",
                "        var type = Type.GetType(\"XPScript.UI.Desktop.DesktopAccessibilityHost, XPScript.UI.Desktop\", throwOnError: false, ignoreCase: false);",
                "        var method = type?.GetMethod(\"FocusField\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string), typeof(string)], null);",
                "        try { method?.Invoke(null, [instanceId, fieldName]); } catch { }",
                "    }",
                string.Empty,
                "    public static string TryGetFocusedField(string instanceId, string fallback)",
                "    {",
                "        var type = Type.GetType(\"XPScript.UI.Desktop.DesktopAccessibilityHost, XPScript.UI.Desktop\", throwOnError: false, ignoreCase: false);",
                "        var method = type?.GetMethod(\"GetFocusedField\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string)], null);",
                "        try { return method?.Invoke(null, [instanceId]) as string ?? fallback; } catch { return fallback; }",
                "    }",
                string.Empty,
                "    public static void Announce(string instanceId, string message, string priority)",
                "    {",
                "        var type = Type.GetType(\"XPScript.UI.Desktop.DesktopAccessibilityHost, XPScript.UI.Desktop\", throwOnError: false, ignoreCase: false);",
                "        var method = type?.GetMethod(\"Announce\", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string), typeof(string), typeof(string)], null);",
                "        try { method?.Invoke(null, [instanceId, message, priority]); } catch { }",
                "    }",
                string.Empty,
                "    public static bool TryIsVisible(string instanceId, bool fallback)",
                "    {"));

        generated = ReplaceOnceRequired(
            generated,
            "            var required = field.Required ? \" required\" : string.Empty;",
            Block(
                "            var required = field.Required ? \" required aria-required=\\\"true\\\"\" : string.Empty;",
                "            var accessibility = BuildAccessibilityAttributes(field, name);"));

        generated = AddAccessibilityAttributesToRenderer(generated);
        generated = AddFieldMessagesAndAnnouncement(generated);

        generated = ReplaceOnceRequired(
            generated,
            "    private static string NormalizeFieldName(object? value)\n    {",
            Block(
                "    private string BuildAccessibilityAttributes(XPScriptUIField field, string encodedName)",
                "    {",
                "        var html = new System.Text.StringBuilder();",
                "        if (field.AccessibleName.Length > 0) html.Append(\" aria-label=\\\"\").Append(System.Net.WebUtility.HtmlEncode(field.AccessibleName)).Append(\"\\\"\");",
                "        var describedBy = new List<string>();",
                "        if (field.AccessibleDescription.Length > 0 || field.AccessibleHelpText.Length > 0) describedBy.Add(\"xps_\" + encodedName + \"_help\");",
                "        if (field.ValidationError.Length > 0) describedBy.Add(\"xps_\" + encodedName + \"_error\");",
                "        if (describedBy.Count > 0) html.Append(\" aria-describedby=\\\"\").Append(string.Join(\" \", describedBy)).Append(\"\\\"\");",
                "        if (field.ValidationError.Length > 0) html.Append(\" aria-invalid=\\\"true\\\"\");",
                "        if (field.AccessibilityHidden) html.Append(\" aria-hidden=\\\"true\\\" tabindex=\\\"-1\\\"\");",
                "        else if (!field.IsTabStop || !field.Focusable) html.Append(\" tabindex=\\\"-1\\\"\");",
                "        else html.Append(\" tabindex=\\\"\").Append(field.TabIndex).Append(\"\\\"\");",
                "        if (field.Name.Equals(_initialFocus, StringComparison.OrdinalIgnoreCase)) html.Append(\" autofocus\");",
                "        if (field.AccessibleLive.Equals(\"Polite\", StringComparison.OrdinalIgnoreCase)) html.Append(\" aria-live=\\\"polite\\\"\");",
                "        else if (field.AccessibleLive.Equals(\"Assertive\", StringComparison.OrdinalIgnoreCase)) html.Append(\" aria-live=\\\"assertive\\\"\");",
                "        if (field.AccessKey.Length > 0) html.Append(\" accesskey=\\\"\").Append(System.Net.WebUtility.HtmlEncode(field.AccessKey)).Append(\"\\\"\");",
                "        return html.ToString();",
                "    }",
                string.Empty,
                "    private static string NormalizeFieldName(object? value)",
                "    {"));

        return generated;
    }

    private static bool NeedsAccessibilityRuntime(string generated)
    {
        var runtimeIndex = generated.IndexOf(BaseUiRuntimeSentinel, StringComparison.Ordinal);
        var scriptPart = runtimeIndex >= 0 ? generated[..runtimeIndex] : generated;
        foreach (var token in FeatureTokens)
        {
            if (scriptPart.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static string AddAccessibilityAttributesToRenderer(string generated)
    {
        var start = FindRendererStart(generated);
        var end = generated.IndexOf("        return html.ToString();", start, StringComparison.Ordinal);
        if (end < 0) throw new CompilerException("Unable to install UIForm accessibility runtime support (renderer return).");
        var segment = generated[start..end];
        var replaced = segment.Replace(".Append(required)", ".Append(required).Append(accessibility)", StringComparison.Ordinal);
        if (string.Equals(segment, replaced, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm accessibility runtime support (renderer attributes).");
        return generated[..start] + replaced + generated[end..];
    }

    private static string AddFieldMessagesAndAnnouncement(string generated)
    {
        var start = FindRendererStart(generated);
        var submitIndex = FindSubmitIndex(generated, start);
        if (submitIndex < 0) throw new CompilerException("Unable to install UIForm accessibility runtime support (submit button).");

        const string closeMarker = "            html.Append(\"</div>\");";
        var fieldClose = generated.LastIndexOf(closeMarker, submitIndex, StringComparison.Ordinal);
        if (fieldClose < start) throw new CompilerException("Unable to install UIForm accessibility runtime support (field wrapper).");

        var fieldMessages = Block(
            "            if (field.AccessibleDescription.Length > 0 || field.AccessibleHelpText.Length > 0)",
            "                html.Append(\"<div class=\\\"xpscript-uiform-help\\\" id=\\\"xps_\").Append(name).Append(\"_help\\\">\").Append(System.Net.WebUtility.HtmlEncode(string.Join(\" \", new[] { field.AccessibleDescription, field.AccessibleHelpText }.Where(text => text.Length > 0)))).Append(\"</div>\");",
            "            if (field.ValidationError.Length > 0)",
            "                html.Append(\"<div class=\\\"xpscript-uiform-error\\\" id=\\\"xps_\").Append(name).Append(\"_error\\\" role=\\\"alert\\\">\").Append(System.Net.WebUtility.HtmlEncode(field.ValidationError)).Append(\"</div>\");",
            string.Empty);
        generated = generated.Insert(fieldClose, fieldMessages);

        start = FindRendererStart(generated);
        submitIndex = FindSubmitIndex(generated, start);
        if (submitIndex < 0) throw new CompilerException("Unable to install UIForm accessibility runtime support (submit button after field messages).");
        var submitLineStart = generated.LastIndexOf('\n', Math.Max(start, submitIndex - 1));
        submitLineStart = submitLineStart < 0 ? submitIndex : submitLineStart + 1;
        var announcement = Block(
            "        if (_announcement.Length > 0)",
            "            html.Append(\"<div class=\\\"xpscript-uiform-live\\\" aria-live=\\\"\").Append(_announcementPriority.Equals(\"Assertive\", StringComparison.OrdinalIgnoreCase) ? \"assertive\" : \"polite\").Append(\"\\\">\").Append(System.Net.WebUtility.HtmlEncode(_announcement)).Append(\"</div>\");",
            string.Empty);
        return generated.Insert(submitLineStart, announcement);
    }

    private static int FindSubmitIndex(string generated, int rendererStart)
    {
        var rendererEnd = generated.IndexOf("        return html.ToString();", rendererStart, StringComparison.Ordinal);
        if (rendererEnd < 0) rendererEnd = generated.Length;
        var markerIndex = generated.IndexOf("__xps_uiform_submit", rendererStart, StringComparison.Ordinal);
        if (markerIndex < 0 || markerIndex >= rendererEnd) return -1;
        var lineStart = generated.LastIndexOf('\n', Math.Max(rendererStart, markerIndex - 1));
        lineStart = lineStart < rendererStart ? rendererStart : lineStart + 1;
        var appendIndex = generated.IndexOf("html.Append(", lineStart, StringComparison.Ordinal);
        return appendIndex >= lineStart && appendIndex < markerIndex ? appendIndex : markerIndex;
    }

    private static int FindRendererStart(string generated)
    {
        var start = generated.IndexOf("    private string RenderWebForm(bool modal)\n    {", StringComparison.Ordinal);
        if (start >= 0) return start;
        start = generated.IndexOf("    private string RenderWebForm()\n    {", StringComparison.Ordinal);
        if (start >= 0) return start;
        throw new CompilerException("Unable to install UIForm accessibility runtime support (renderer signature).");
    }

    private static string Block(params string[] lines) => string.Join('\n', lines);

    private static string ReplaceOnceRequired(string source, string oldValue, string newValue)
    {
        var index = source.IndexOf(oldValue, StringComparison.Ordinal);
        if (index < 0)
            throw new CompilerException("Unable to install UIForm accessibility runtime support (marker: " + oldValue.Trim() + ").");
        return source[..index] + newValue + source[(index + oldValue.Length)..];
    }
}