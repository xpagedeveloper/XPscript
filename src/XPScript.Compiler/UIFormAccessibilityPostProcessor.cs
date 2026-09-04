namespace XPScript.Compiler;

internal sealed class UIFormAccessibilityPostProcessor
{
    private const string Sentinel = "public string AccessibleName { get; set; }";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(Sentinel, StringComparison.Ordinal)) return generated;

        generated = ReplaceRequired(
            generated,
            "        Type = type;\n    }",
            Block(
                "        Type = type;",
                "        var interactive = type is not (\"HiddenField\" or \"Separator\" or \"Spacer\" or \"Heading\");",
                "        Focusable = interactive;",
                "        IsTabStop = interactive;",
                "    }"));

        generated = ReplaceRequired(
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

        generated = ReplaceRequired(
            generated,
            "    private readonly List<XPScriptUIField> _fields = [];\n",
            Block(
                "    private readonly List<XPScriptUIField> _fields = [];",
                "    private string _initialFocus = string.Empty;",
                "    private string _announcement = string.Empty;",
                "    private string _announcementPriority = \"Polite\";",
                string.Empty));

        generated = ReplaceRequired(
            generated,
            "    public int FieldCount => _fields.Count;\n",
            Block(
                "    public int FieldCount => _fields.Count;",
                "    public string InitialFocus { get => _initialFocus; set => _initialFocus = NormalizeOptionalFieldName(value); }",
                "    public string FocusedField => XPScriptUIDesktopAdapter.TryGetFocusedField(InstanceId, _initialFocus);",
                "    public bool ValidationSummary { get; set; } = true;",
                "    public bool FocusFirstError { get; set; } = true;",
                "    public bool AnnounceValidationErrors { get; set; } = true;",
                "    public bool HasValidationErrors => _fields.Any(field => field.ValidationError.Length > 0);",
                "    internal string AccessibilityAnnouncement => _announcement;",
                "    internal string AccessibilityAnnouncementPriority => _announcementPriority;",
                "    public object ValidationErrors",
                "    {",
                "        get",
                "        {",
                "            var result = XPScriptNativeJson.CreateArray();",
                "            foreach (var field in _fields.Where(field => field.ValidationError.Length > 0))",
                "            {",
                "                var error = XPScriptNativeJson.CreateObject();",
                "                error.Set(\"fieldName\", field.Name);",
                "                error.Set(\"message\", field.ValidationError);",
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

        generated = ReplaceRequired(
            generated,
            "        var field = new XPScriptUIField(fieldName, XPScriptRuntime.CStr(label), type);\n        _fields.Add(field);",
            Block(
                "        var field = new XPScriptUIField(fieldName, XPScriptRuntime.CStr(label), type);",
                "        field.AccessibilityFocusHandler = fieldToFocus => Focus(fieldToFocus);",
                "        field.AccessibilityFocusQuery = fieldToQuery => FocusedField.Equals(fieldToQuery, StringComparison.OrdinalIgnoreCase);",
                "        _fields.Add(field);"));

        generated = ReplaceRequired(
            generated,
            "                name = field.Name, label = field.Label, type = field.Type, required = field.Required,",
            Block(
                "                name = field.Name, label = field.Label, type = field.Type, required = field.Required,",
                "                accessibleName = field.AccessibleName, accessibleDescription = field.AccessibleDescription, accessibleHelpText = field.AccessibleHelpText,",
                "                accessibleLive = field.AccessibleLive, accessibilityHidden = field.AccessibilityHidden, focusable = field.Focusable,",
                "                isTabStop = field.IsTabStop, tabIndex = field.TabIndex, accessKey = field.AccessKey, hotKey = field.HotKey,",
                "                validationError = field.ValidationError,"));

        generated = ReplaceRequired(
            generated,
            "            resizable = form.Resizable,\n            fields = fields.Select(field => new",
            Block(
                "            resizable = form.Resizable,",
                "            initialFocus = form.InitialFocus,",
                "            validationSummary = form.ValidationSummary,",
                "            focusFirstError = form.FocusFirstError,",
                "            announceValidationErrors = form.AnnounceValidationErrors,",
                "            announcement = form.AccessibilityAnnouncement,",
                "            announcementPriority = form.AccessibilityAnnouncementPriority,",
                "            fields = fields.Select(field => new"));

        generated = ReplaceRequired(
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

        generated = ReplaceRequired(
            generated,
            "            var required = field.Required ? \" required\" : string.Empty;",
            Block(
                "            var required = field.Required ? \" required aria-required=\\\"true\\\"\" : string.Empty;",
                "            var accessibility = BuildAccessibilityAttributes(field, name);"));

        generated = generated.Replace(".Append(required)", ".Append(required).Append(accessibility)", StringComparison.Ordinal);

        generated = ReplaceRequired(
            generated,
            "            html.Append(\"</div>\");\n        }\n        html.Append(\"<button type=\\\"submit\\\"",
            Block(
                "            if (field.AccessibleDescription.Length > 0 || field.AccessibleHelpText.Length > 0)",
                "                html.Append(\"<div class=\\\"xpscript-uiform-help\\\" id=\\\"xps_\").Append(name).Append(\"_help\\\">\").Append(System.Net.WebUtility.HtmlEncode(string.Join(\" \", new[] { field.AccessibleDescription, field.AccessibleHelpText }.Where(text => text.Length > 0)))).Append(\"</div>\");",
                "            if (field.ValidationError.Length > 0)",
                "                html.Append(\"<div class=\\\"xpscript-uiform-error\\\" id=\\\"xps_\").Append(name).Append(\"_error\\\" role=\\\"alert\\\">\").Append(System.Net.WebUtility.HtmlEncode(field.ValidationError)).Append(\"</div>\");",
                "            html.Append(\"</div>\");",
                "        }",
                "        if (_announcement.Length > 0)",
                "            html.Append(\"<div class=\\\"xpscript-uiform-live\\\" aria-live=\\\"\").Append(_announcementPriority.Equals(\"Assertive\", StringComparison.OrdinalIgnoreCase) ? \"assertive\" : \"polite\").Append(\"\\\">\").Append(System.Net.WebUtility.HtmlEncode(_announcement)).Append(\"</div>\");",
                "        html.Append(\"<button type=\\\"submit\\\""));

        generated = ReplaceRequired(
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

    private static string Block(params string[] lines) => string.Join('\n', lines);

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm accessibility runtime support.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
