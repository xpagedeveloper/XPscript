using System.Text;

namespace XPScript.Compiler;

internal sealed class UIFormAdditionalFieldsPostProcessor
{
    private const string Sentinel = "public XPScriptUIField AddRichTextField(object? name";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(Sentinel, StringComparison.Ordinal)) return generated;
        if (!generated.Contains("internal sealed class XPScriptUIForm", StringComparison.Ordinal)) return generated;

        generated = InjectFieldMetadata(generated);
        generated = InjectFormMethods(generated);
        generated = PatchOptionSupport(generated);
        generated = PatchSubmission(generated);
        generated = PatchValidation(generated);
        generated = PatchWebRendering(generated);
        generated = PatchDesktopBridge(generated);
        generated += "\n" + AdditionalRuntime + "\n";
        return generated;
    }

    private static string InjectFieldMetadata(string generated)
    {
        const string marker = "    public List<string> Options { get; } = [];";
        const string replacement = """
    public List<string> Options { get; } = [];
    public List<XPScriptUIRemoteOption> RemoteOptions { get; } = [];
    public string Accept { get; set; } = "";
    public long MaxFileBytes { get; set; } = 8L * 1024 * 1024;
    public bool Multiple { get; set; }
    public string CurrencyCode { get; set; } = "";
    public string DataSourceUrl { get; set; } = "";
    public string ValueMember { get; set; } = "value";
    public string LabelMember { get; set; } = "label";
""";
        if (!generated.Contains(marker, StringComparison.Ordinal)) throw new CompilerException("Unable to extend UIForm field metadata.");
        return generated.Replace(marker, replacement, StringComparison.Ordinal);
    }

    private static string InjectFormMethods(string generated)
    {
        const string marker = "    public void AddOption(object? name, object? value)";
        const string methods = """
    public XPScriptUIField AddFileField(object? name) => AddField(name, name, "FileField");
    public XPScriptUIField AddFileField(object? name, object? label) => AddField(name, label, "FileField");
    public XPScriptUIField AddMultiSelect(object? name) => AddField(name, name, "MultiSelect");
    public XPScriptUIField AddMultiSelect(object? name, object? label) => AddField(name, label, "MultiSelect");
    public XPScriptUIField AddCheckBoxGroup(object? name) => AddField(name, name, "CheckBoxGroup");
    public XPScriptUIField AddCheckBoxGroup(object? name, object? label) => AddField(name, label, "CheckBoxGroup");
    public XPScriptUIField AddTelField(object? name) => AddField(name, name, "TelField");
    public XPScriptUIField AddTelField(object? name, object? label) => AddField(name, label, "TelField");
    public XPScriptUIField AddWeekField(object? name) => AddField(name, name, "WeekField");
    public XPScriptUIField AddWeekField(object? name, object? label) => AddField(name, label, "WeekField");
    public XPScriptUIField AddDecimalField(object? name) => AddField(name, name, "DecimalField");
    public XPScriptUIField AddDecimalField(object? name, object? label) => AddField(name, label, "DecimalField");
    public XPScriptUIField AddCurrencyField(object? name, object? label, object? currency)
    {
        var field = AddField(name, label, "CurrencyField");
        field.CurrencyCode = XPScriptUIAdditionalFieldRuntime.CurrencyCode(currency);
        return field;
    }
    public XPScriptUIField AddRichTextField(object? name) => AddField(name, name, "RichTextField");
    public XPScriptUIField AddRichTextField(object? name, object? label) => AddField(name, label, "RichTextField");
    public XPScriptUIField AddLookupField(object? name, object? label, object? url)
        => AddRemoteField(name, label, "LookupField", url, "value", "label");
    public XPScriptUIField AddLookupField(object? name, object? label, object? url, object? valueMember, object? labelMember)
        => AddRemoteField(name, label, "LookupField", url, valueMember, labelMember);
    public XPScriptUIField AddAutoCompleteField(object? name, object? label, object? url)
        => AddRemoteField(name, label, "AutoCompleteField", url, "value", "label");
    public XPScriptUIField AddAutoCompleteField(object? name, object? label, object? url, object? valueMember, object? labelMember)
        => AddRemoteField(name, label, "AutoCompleteField", url, valueMember, labelMember);

    public void SetFileOptions(object? name, object? accept, object? maxBytes, object? multiple)
    {
        var field = FindField(name);
        if (field.Type != "FileField") throw new XPScriptRuntimeException(5, "UIForm file options require a FileField.");
        field.Accept = XPScriptRuntime.CStr(accept).Trim();
        var limit = XPScriptRuntime.CLng(maxBytes);
        if (limit < 1 || limit > 64L * 1024 * 1024) throw new XPScriptRuntimeException(5, "UIForm file size limit must be between 1 byte and 64 MiB.");
        field.MaxFileBytes = limit;
        field.Multiple = Convert.ToBoolean(multiple, System.Globalization.CultureInfo.CurrentCulture);
    }

    private XPScriptUIField AddRemoteField(object? name, object? label, string type, object? url, object? valueMember, object? labelMember)
    {
        var field = AddField(name, label, type);
        field.DataSourceUrl = XPScriptUIAdditionalFieldRuntime.AbsoluteHttpUrl(url);
        field.ValueMember = XPScriptUIAdditionalFieldRuntime.MemberName(valueMember, "value member");
        field.LabelMember = XPScriptUIAdditionalFieldRuntime.MemberName(labelMember, "label member");
        foreach (var option in XPScriptUIAdditionalFieldRuntime.LoadOptions(field.DataSourceUrl, field.ValueMember, field.LabelMember))
        {
            field.RemoteOptions.Add(option);
            if (!field.Options.Contains(option.Value, StringComparer.Ordinal)) field.Options.Add(option.Value);
        }
        return field;
    }

""";
        if (!generated.Contains(marker, StringComparison.Ordinal)) throw new CompilerException("Unable to inject extended UIForm methods.");
        return generated.Replace(marker, methods + marker, StringComparison.Ordinal);
    }

    private static string PatchOptionSupport(string generated)
    {
        generated = generated.Replace(
            "field.Type is not (\"Select\" or \"RadioGroup\" or \"ListBox\" or \"MultiListBox\")",
            "field.Type is not (\"Select\" or \"RadioGroup\" or \"ListBox\" or \"MultiListBox\" or \"MultiSelect\" or \"CheckBoxGroup\")",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "UIForm options are only supported for Select, RadioGroup, ListBox and MultiListBox fields.",
            "UIForm options are only supported for Select, RadioGroup, ListBox, MultiListBox, MultiSelect and CheckBoxGroup fields.",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "field.Type is not (\"TextField\" or \"TextArea\" or \"PasswordField\" or \"EmailField\" or \"UrlField\")",
            "field.Type is not (\"TextField\" or \"TextArea\" or \"PasswordField\" or \"EmailField\" or \"UrlField\" or \"TelField\" or \"RichTextField\")",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "field.Type is not (\"NumberField\" or \"RangeField\")",
            "field.Type is not (\"NumberField\" or \"RangeField\" or \"DecimalField\" or \"CurrencyField\")",
            StringComparison.Ordinal);
        return generated;
    }

    private static string PatchSubmission(string generated)
    {
        generated = generated.Replace(
            "if (field.Type == \"MultiListBox\") ApplySubmittedValues(field, XPScriptUIWebAdapter.FormValues(field.Name));",
            "if (field.Type is \"MultiListBox\" or \"MultiSelect\" or \"CheckBoxGroup\") ApplySubmittedValues(field, XPScriptUIWebAdapter.FormValues(field.Name));\n                else if (field.Type == \"FileField\") ApplySubmittedFile(field, XPScriptUIWebAdapter.FileJson(field.Name, field.MaxFileBytes));",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "if (field.Type != \"MultiListBox\")\n            throw new XPScriptRuntimeException(5, \"UIForm multi-value submission is only supported for MultiListBox fields.\");",
            "if (field.Type is not (\"MultiListBox\" or \"MultiSelect\" or \"CheckBoxGroup\"))\n            throw new XPScriptRuntimeException(5, \"UIForm multi-value submission is only supported for multi-value fields.\");",
            StringComparison.Ordinal);

        const string marker = "    private void ApplySubmittedValue(XPScriptUIField field, string submitted)";
        const string method = """
    private void ApplySubmittedFile(XPScriptUIField field, string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            if (field.Required && !_data.Contains(field.Name)) throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' is required.");
            return;
        }
        var document = XPScriptNativeJson.Parse(json);
        var obj = document.Root.AsObject() ?? throw new XPScriptRuntimeException(13, "UIForm uploaded file metadata is invalid.");
        _data.Set(field.Name, obj);
    }

""";
        if (!generated.Contains(marker, StringComparison.Ordinal)) throw new CompilerException("Unable to inject UIForm file submission handling.");
        return generated.Replace(marker, method + marker, StringComparison.Ordinal);
    }

    private static string PatchValidation(string generated)
    {
        generated = generated.Replace(
            "if (field.Type is \"TextField\" or \"TextArea\" or \"PasswordField\" or \"EmailField\" or \"UrlField\")",
            "if (field.Type is \"TextField\" or \"TextArea\" or \"PasswordField\" or \"EmailField\" or \"UrlField\" or \"TelField\" or \"RichTextField\")",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "case \"NumberField\":\n            case \"RangeField\":",
            "case \"NumberField\":\n            case \"RangeField\":\n            case \"DecimalField\":\n            case \"CurrencyField\":",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "case \"MonthField\":",
            "case \"WeekField\":\n                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }\n                if (!XPScriptUIAdditionalFieldRuntime.IsIsoWeek(submitted)) throw new XPScriptRuntimeException(13, $\"UIForm field '{field.Name}' must contain a valid ISO week in yyyy-Www format.\");\n                _data.Set(field.Name, submitted);\n                return;\n            case \"MonthField\":",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "case \"Select\":\n            case \"ListBox\":\n            case \"RadioGroup\":",
            "case \"LookupField\":\n            case \"AutoCompleteField\":\n                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }\n                if (!field.Options.Contains(submitted, StringComparer.Ordinal)) throw new XPScriptRuntimeException(5, $\"UIForm field '{field.Name}' contains an unsupported lookup value.\");\n                _data.Set(field.Name, submitted);\n                return;\n            case \"Select\":\n            case \"ListBox\":\n            case \"RadioGroup\":",
            StringComparison.Ordinal);
        return generated;
    }

    private static string PatchWebRendering(string generated)
    {
        generated = generated.Replace(
            "var value = field.Type == \"MultiListBox\" ? string.Empty : System.Net.WebUtility.HtmlEncode(GetFieldValueString(field.Name));",
            "var value = field.Type is \"MultiListBox\" or \"MultiSelect\" or \"CheckBoxGroup\" or \"FileField\" ? string.Empty : System.Net.WebUtility.HtmlEncode(GetFieldValueString(field.Name));",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "html.Append(\"<form method=\\\"post\\\" class=\\\"xpscript-uiform\\\">\");",
            "html.Append(_fields.Any(f => f.Type == \"FileField\") ? \"<form method=\\\"post\\\" enctype=\\\"multipart/form-data\\\" class=\\\"xpscript-uiform\\\">\" : \"<form method=\\\"post\\\" class=\\\"xpscript-uiform\\\">\");",
            StringComparison.Ordinal);

        const string caseMarker = "                case \"EmailField\":";
        const string cases = """
                case "FileField":
                    html.Append("<input type=\"file\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"");
                    if (field.Accept.Length > 0) html.Append(" accept=\"").Append(System.Net.WebUtility.HtmlEncode(field.Accept)).Append("\"");
                    if (field.Multiple) html.Append(" multiple");
                    html.Append(required).Append(">");
                    break;
                case "TelField": html.Append("<input type=\"tel\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(length).Append(">"); break;
                case "WeekField": html.Append("<input type=\"week\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
                case "DecimalField": html.Append("<input type=\"number\" step=\"any\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(range).Append(">"); break;
                case "CurrencyField": html.Append("<div class=\"input-group\"><span class=\"input-group-text\">").Append(System.Net.WebUtility.HtmlEncode(field.CurrencyCode)).Append("</span><input type=\"number\" step=\"0.01\" class=\"form-control\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(range).Append("></div>"); break;
                case "RichTextField": html.Append("<textarea class=\"xpscript-richtext\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(required).Append(length).Append(">").Append(value).Append("</textarea>"); break;
                case "MultiSelect":
                    var multiSelected = ReadSelectedValues(field.Name);
                    html.Append("<select multiple id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(required).Append(">");
                    foreach (var option in field.Options) { var eo = System.Net.WebUtility.HtmlEncode(option); html.Append("<option value=\"").Append(eo).Append("\""); if (multiSelected.Contains(option, StringComparer.Ordinal)) html.Append(" selected"); html.Append(">").Append(eo).Append("</option>"); }
                    html.Append("</select>"); break;
                case "CheckBoxGroup":
                    var checkedItems = ReadSelectedValues(field.Name);
                    foreach (var option in field.Options) { var eo = System.Net.WebUtility.HtmlEncode(option); html.Append("<label class=\"form-check\"><input class=\"form-check-input\" type=\"checkbox\" name=\"").Append(name).Append("\" value=\"").Append(eo).Append("\""); if (checkedItems.Contains(option, StringComparer.Ordinal)) html.Append(" checked"); html.Append(required).Append("><span class=\"form-check-label\">").Append(eo).Append("</span></label>"); }
                    break;
                case "LookupField":
                    html.Append("<select id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"").Append(required).Append(">"); if (!field.Required) html.Append("<option value=\"\"></option>"); foreach (var option in field.RemoteOptions) { var ev = System.Net.WebUtility.HtmlEncode(option.Value); var el = System.Net.WebUtility.HtmlEncode(option.Label); html.Append("<option value=\"").Append(ev).Append("\""); if (option.Value == GetFieldValueString(field.Name)) html.Append(" selected"); html.Append(">").Append(el).Append("</option>"); } html.Append("</select>"); break;
                case "AutoCompleteField":
                    html.Append("<input list=\"xps_list_").Append(name).Append("\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append("><datalist id=\"xps_list_").Append(name).Append("\">"); foreach (var option in field.RemoteOptions) { html.Append("<option value=\"").Append(System.Net.WebUtility.HtmlEncode(option.Value)).Append("\" label=\"").Append(System.Net.WebUtility.HtmlEncode(option.Label)).Append("\"></option>"); } html.Append("</datalist>"); break;
""";
        if (!generated.Contains(caseMarker, StringComparison.Ordinal)) throw new CompilerException("Unable to extend UIForm web renderer.");
        generated = generated.Replace(caseMarker, cases + caseMarker, StringComparison.Ordinal);

        const string returnMarker = "        html.Append(\"<button type=\\\"submit\\\" name=\\\"__xps_uiform_submit\\\" value=\\\"1\\\">OK</button></form>\");\n        return html.ToString();";
        const string returnReplacement = """
        html.Append("<button type=\"submit\" name=\"__xps_uiform_submit\" value=\"1\">OK</button></form>");
        if (_fields.Any(f => f.Type == "RichTextField"))
        {
            html.Append("<script src=\"https://cdn.tiny.cloud/1/no-api-key/tinymce/7/tinymce.min.js\" referrerpolicy=\"origin\"></script>");
            html.Append("<script>tinymce.init({selector:'textarea.xpscript-richtext',plugins:'lists link table code',toolbar:'undo redo | blocks | bold italic | bullist numlist | link table | code'});</script>");
        }
        return html.ToString();
""";
        if (!generated.Contains(returnMarker, StringComparison.Ordinal)) throw new CompilerException("Unable to install UIForm rich-text renderer.");
        return generated.Replace(returnMarker, returnReplacement, StringComparison.Ordinal);
    }

    private static string PatchDesktopBridge(string generated)
    {
        generated = generated.Replace("type = field.Type,", "type = XPScriptUIAdditionalFieldRuntime.DesktopType(field.Type),", StringComparison.Ordinal);
        generated = generated.Replace(
            "field.Type is \"PasswordField\" or \"MultiListBox\"",
            "field.Type is \"PasswordField\" or \"MultiListBox\" or \"MultiSelect\" or \"CheckBoxGroup\"",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "values = field.Type == \"MultiListBox\" ? ReadValues(data, field.Name) : Array.Empty<string>(),",
            "values = field.Type is \"MultiListBox\" or \"MultiSelect\" or \"CheckBoxGroup\" ? ReadValues(data, field.Name) : Array.Empty<string>(),",
            StringComparison.Ordinal);
        generated = generated.Replace(
            "if (field.Type == \"MultiListBox\")\n            {",
            "if (field.Type is \"MultiListBox\" or \"MultiSelect\" or \"CheckBoxGroup\")\n            {",
            StringComparison.Ordinal);
        return generated;
    }

    private const string AdditionalRuntime = """
internal sealed record XPScriptUIRemoteOption(string Value, string Label);

internal static class XPScriptUIAdditionalFieldRuntime
{
    public static string CurrencyCode(object? value)
    {
        var code = XPScriptRuntime.CStr(value).Trim().ToUpperInvariant();
        if (code.Length != 3 || !code.All(char.IsAsciiLetter)) throw new XPScriptRuntimeException(5, "UIForm currency code must be a three-letter ISO-style code.");
        return code;
    }

    public static string AbsoluteHttpUrl(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new XPScriptRuntimeException(5, "UIForm lookup data source must be an absolute HTTP or HTTPS URL.");
        return text;
    }

    public static string MemberName(object? value, string label)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 128 || name.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '-' or '.')))
            throw new XPScriptRuntimeException(5, "UIForm " + label + " is invalid.");
        return name;
    }

    public static IReadOnlyList<XPScriptUIRemoteOption> LoadOptions(string url, string valueMember, string labelMember)
    {
        using var http = new XPScriptHttpClient();
        var response = http.Get(url);
        if (!response.IsSuccess) throw new XPScriptRuntimeException(5, "UIForm lookup data source returned HTTP " + response.StatusCode + ".");
        var document = XPScriptNativeJson.Parse(response.Body);
        var array = document.Root.AsArray() ?? throw new XPScriptRuntimeException(13, "UIForm lookup data source must return a JSON array.");
        if (array.Count > 5000) throw new XPScriptRuntimeException(5, "UIForm lookup data source returned more than 5000 rows.");
        var result = new List<XPScriptUIRemoteOption>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            var item = array.Get(i) as XPScriptJsonObject ?? throw new XPScriptRuntimeException(13, "UIForm lookup data rows must be JSON objects.");
            var value = XPScriptRuntime.CStr(item.Get(valueMember));
            var label = XPScriptRuntime.CStr(item.Get(labelMember));
            if (value.Length == 0) continue;
            if (!result.Any(x => x.Value.Equals(value, StringComparison.Ordinal))) result.Add(new XPScriptUIRemoteOption(value, label.Length == 0 ? value : label));
        }
        return result;
    }

    public static bool IsIsoWeek(string value)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{4}-W\d{2}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)) return false;
        if (!int.TryParse(value.AsSpan(0, 4), out var year) || !int.TryParse(value.AsSpan(6, 2), out var week)) return false;
        try { _ = System.Globalization.ISOWeek.ToDateTime(year, week, DayOfWeek.Monday); return true; } catch (ArgumentOutOfRangeException) { return false; }
    }

    public static string DesktopType(string type) => type switch
    {
        "MultiSelect" or "CheckBoxGroup" => "MultiListBox",
        "DecimalField" or "CurrencyField" => "NumberField",
        "RichTextField" => "TextArea",
        "LookupField" or "AutoCompleteField" => "Select",
        "FileField" or "TelField" or "WeekField" => "TextField",
        _ => type
    };
}
""";
}
