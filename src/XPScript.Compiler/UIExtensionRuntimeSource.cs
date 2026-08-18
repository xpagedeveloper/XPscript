namespace XPScript.Compiler;

internal static class UIExtensionRuntimeSource
{
    public const string Code = """
internal static class XPScriptUI
{
    public static XPScriptUIForm CreateForm()
        => new(string.Empty, null, null, true);

    public static XPScriptUIForm CreateForm(object? title)
        => new(XPScriptRuntime.CStr(title), null, null, true);

    public static XPScriptUIForm CreateForm(object? title, object? width, object? height)
        => new(XPScriptRuntime.CStr(title), ToOptionalPositiveInt(width, "width"), ToOptionalPositiveInt(height, "height"), true);

    public static XPScriptUIForm CreateForm(object? title, object? width, object? height, object? resizable)
        => new(XPScriptRuntime.CStr(title), ToOptionalPositiveInt(width, "width"), ToOptionalPositiveInt(height, "height"), Convert.ToBoolean(resizable, System.Globalization.CultureInfo.CurrentCulture));

    private static int? ToOptionalPositiveInt(object? value, string name)
    {
        if (value is null) return null;
        int converted;
        try
        {
            converted = Convert.ToInt32(value, System.Globalization.CultureInfo.CurrentCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, $"UIForm {name} must be an Integer value.");
        }

        if (converted <= 0)
            throw new XPScriptRuntimeException(5, $"UIForm {name} must be greater than zero.");
        return converted;
    }
}

internal sealed class XPScriptUIField
{
    internal XPScriptUIField(string name, string label, string type)
    {
        Name = name;
        Label = label;
        Type = type;
    }

    public string Name { get; }
    public string Label { get; set; }
    public string Type { get; }
    public bool Required { get; set; }
}

internal sealed class XPScriptUIForm
{
    private string _title;
    private int? _width;
    private int? _height;
    private bool _resizable;
    private XPScriptJsonObject _data = XPScriptNativeJson.CreateObject();
    private readonly List<XPScriptUIField> _fields = [];

    internal XPScriptUIForm(string title, int? width, int? height, bool resizable)
    {
        _title = title;
        _width = width;
        _height = height;
        _resizable = resizable;
    }

    public string Title
    {
        get => _title;
        set => _title = value ?? string.Empty;
    }

    public int Width
    {
        get => _width ?? 0;
        set
        {
            if (value <= 0) throw new XPScriptRuntimeException(5, "UIForm width must be greater than zero.");
            _width = value;
        }
    }

    public int Height
    {
        get => _height ?? 0;
        set
        {
            if (value <= 0) throw new XPScriptRuntimeException(5, "UIForm height must be greater than zero.");
            _height = value;
        }
    }

    public bool Resizable
    {
        get => _resizable;
        set => _resizable = value;
    }

    public bool HasExplicitSize => _width.HasValue || _height.HasValue;
    public object Data => _data;
    public int FieldCount => _fields.Count;

    public void BindData(object? value)
    {
        _data = value switch
        {
            XPScriptJsonObject obj => obj,
            XPScriptJsonDocument document when document.Root.AsObject() is XPScriptJsonObject obj => obj,
            null => XPScriptNativeJson.CreateObject(),
            _ => throw new XPScriptRuntimeException(13, "UIForm.BindData requires a JsonObject or a JsonDocument with an object root.")
        };
    }

    public XPScriptUIField AddTextField(object? name) => AddField(name, name, "TextField");
    public XPScriptUIField AddTextField(object? name, object? label) => AddField(name, label, "TextField");
    public XPScriptUIField AddTextArea(object? name) => AddField(name, name, "TextArea");
    public XPScriptUIField AddTextArea(object? name, object? label) => AddField(name, label, "TextArea");
    public XPScriptUIField AddNumberField(object? name) => AddField(name, name, "NumberField");
    public XPScriptUIField AddNumberField(object? name, object? label) => AddField(name, label, "NumberField");
    public XPScriptUIField AddCheckBox(object? name) => AddField(name, name, "CheckBox");
    public XPScriptUIField AddCheckBox(object? name, object? label) => AddField(name, label, "CheckBox");
    public XPScriptUIField AddDateField(object? name) => AddField(name, name, "DateField");
    public XPScriptUIField AddDateField(object? name, object? label) => AddField(name, label, "DateField");

    public void SetRequired(object? name, object? required)
    {
        var field = FindField(name);
        field.Required = Convert.ToBoolean(required, System.Globalization.CultureInfo.CurrentCulture);
    }

    public object? GetFieldValue(object? name)
    {
        var fieldName = NormalizeFieldName(name);
        return _data.Contains(fieldName) ? _data.Get(fieldName) ?? string.Empty : string.Empty;
    }

    public string GetFieldValueString(object? name)
        => XPScriptRuntime.CStr(GetFieldValue(name));

    public void SetFieldValue(object? name, object? value)
    {
        var fieldName = NormalizeFieldName(name);
        var exists = _data.Contains(fieldName);
        var isEmptyText = value is null || XPScriptRuntime.CStr(value).Length == 0;
        if (!exists && isEmptyText) return;
        _data.Set(fieldName, value ?? string.Empty);
    }

    public string ShowDialog()
    {
        if (!XPScriptUIWebAdapter.IsAvailable)
            throw new XPScriptRuntimeException(5, "UIForm.ShowDialog requires a configured desktop UI backend or an active XPScript web request.");

        if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var field in _fields)
                ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
            return "OK";
        }

        XPScriptUIWebAdapter.WriteHtml(RenderWebForm());
        return "Pending";
    }

    private XPScriptUIField AddField(object? name, object? label, string type)
    {
        var fieldName = NormalizeFieldName(name);
        if (_fields.Any(field => field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, $"UIForm field '{fieldName}' already exists.");
        var field = new XPScriptUIField(fieldName, XPScriptRuntime.CStr(label), type);
        _fields.Add(field);
        return field;
    }

    private XPScriptUIField FindField(object? name)
    {
        var fieldName = NormalizeFieldName(name);
        return _fields.FirstOrDefault(field => field.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            ?? throw new XPScriptRuntimeException(5, $"UIForm field '{fieldName}' does not exist.");
    }

    private void ApplySubmittedValue(XPScriptUIField field, string submitted)
    {
        var exists = _data.Contains(field.Name);
        if (field.Required && submitted.Length == 0)
            throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' is required.");

        switch (field.Type)
        {
            case "NumberField":
                if (submitted.Length == 0)
                {
                    if (exists) _data.Set(field.Name, string.Empty);
                    return;
                }
                if (!decimal.TryParse(submitted, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid number.");
                _data.Set(field.Name, number);
                return;

            case "CheckBox":
                if (submitted.Length == 0)
                {
                    if (exists) _data.Set(field.Name, false);
                    return;
                }
                _data.Set(field.Name, submitted.Equals("1", StringComparison.OrdinalIgnoreCase) || submitted.Equals("true", StringComparison.OrdinalIgnoreCase) || submitted.Equals("on", StringComparison.OrdinalIgnoreCase));
                return;

            case "DateField":
                if (submitted.Length == 0)
                {
                    if (exists) _data.Set(field.Name, string.Empty);
                    return;
                }
                if (!DateTime.TryParseExact(submitted, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid date in yyyy-MM-dd format.");
                _data.Set(field.Name, submitted);
                return;

            default:
                if (!exists && submitted.Length == 0) return;
                _data.Set(field.Name, submitted);
                return;
        }
    }

    private string RenderWebForm()
    {
        var html = new System.Text.StringBuilder();
        html.Append("<form method=\"post\" class=\"xpscript-uiform\">");
        if (_title.Length > 0)
            html.Append("<h1>").Append(System.Net.WebUtility.HtmlEncode(_title)).Append("</h1>");

        foreach (var field in _fields)
        {
            var name = System.Net.WebUtility.HtmlEncode(field.Name);
            var label = System.Net.WebUtility.HtmlEncode(field.Label);
            var value = System.Net.WebUtility.HtmlEncode(GetFieldValueString(field.Name));
            var required = field.Required ? " required" : string.Empty;
            html.Append("<div class=\"xpscript-uiform-field\"><label for=\"xps_")
                .Append(name).Append("\">").Append(label).Append("</label>");

            switch (field.Type)
            {
                case "TextArea":
                    html.Append("<textarea id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\"")
                        .Append(required).Append(">").Append(value).Append("</textarea>");
                    break;
                case "NumberField":
                    html.Append("<input type=\"number\" step=\"any\" id=\"xps_").Append(name).Append("\" name=\"").Append(name)
                        .Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">");
                    break;
                case "CheckBox":
                    var checkedValue = GetFieldValue(field.Name) is bool b && b;
                    html.Append("<input type=\"checkbox\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"1\"");
                    if (checkedValue) html.Append(" checked");
                    html.Append(required).Append(">");
                    break;
                case "DateField":
                    html.Append("<input type=\"date\" id=\"xps_").Append(name).Append("\" name=\"").Append(name)
                        .Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">");
                    break;
                default:
                    html.Append("<input type=\"text\" id=\"xps_").Append(name).Append("\" name=\"").Append(name)
                        .Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">");
                    break;
            }

            html.Append("</div>");
        }

        html.Append("<button type=\"submit\" name=\"__xps_uiform_submit\" value=\"1\">OK</button></form>");
        return html.ToString();
    }

    private static string NormalizeFieldName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 128)
            throw new XPScriptRuntimeException(5, "UIForm field name must contain between 1 and 128 characters.");
        if (name.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '-' or '.')))
            throw new XPScriptRuntimeException(5, "UIForm field name contains unsupported characters.");
        return name;
    }
}

internal static class XPScriptUIWebAdapter
{
    private const string BridgeTypeName = "XPScript.Web.Runtime.XpsUIWebRuntimeBridge, XPScript.Web.Runtime";

    private static Type? BridgeType => Type.GetType(BridgeTypeName, throwOnError: false, ignoreCase: false);

    public static bool IsAvailable
    {
        get
        {
            var type = BridgeType;
            if (type is null) return false;
            var method = type.GetMethod("IsAvailable", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            return method is not null && method.Invoke(null, null) is true;
        }
    }

    public static string Method => InvokeString("Method");
    public static string FormFirst(string name) => InvokeString("FormFirst", name);

    public static void WriteHtml(string html)
    {
        var type = BridgeType ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is unavailable.");
        var method = type.GetMethod("WriteHtml", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is incomplete.");
        method.Invoke(null, [html]);
    }

    private static string InvokeString(string methodName, params object?[] args)
    {
        var type = BridgeType ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is unavailable.");
        var method = type.GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript web UI bridge is incomplete.");
        return Convert.ToString(method.Invoke(null, args), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
""";
}
