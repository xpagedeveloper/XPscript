namespace XPScript.Compiler;

internal static class UIFormValidationRuntimeSource
{
    public const string Code = """
internal sealed partial class XPScriptUIForm
{
    private XPScriptJsonSchema? _validationSchema;
    public bool HasValidationSchema => _validationSchema is not null;
    public void SetValidationSchema(object? value)
    {
        _validationSchema = value switch
        {
            XPScriptJsonSchema schema => schema,
            null => null,
            _ => throw new XPScriptRuntimeException(13, "UIForm.SetValidationSchema requires an XPJsonSchema or Nothing.")
        };
    }
    public XPScriptJsonValidationResult ValidateData()
        => _validationSchema is null
            ? new XPScriptJsonValidationResult(new System.Text.Json.Nodes.JsonArray())
            : _validationSchema.Validate(_data);
    public bool IsDataValid => ValidateData().Valid;
    internal string GetValidationError(object? nameValue)
    {
        var name = XPScriptRuntime.CStr(nameValue);
        var result = ValidateData();
        for (var i = 0; i < result.ErrorCount; i++)
        {
            var error = result.GetError(i);
            if (!ValidationPathMatchesField(XPScriptRuntime.CStr(error.Get("path")), name)) continue;
            return XPScriptRuntime.CStr(error.Get("message"));
        }
        return string.Empty;
    }
    private static bool ValidationPathMatchesField(string path, string fieldName)
    {
        if (path.Length < 3 || !path.StartsWith("$.", StringComparison.Ordinal)) return false;
        var fieldPath = path[2..];
        if (fieldPath.Equals(fieldName, StringComparison.OrdinalIgnoreCase)) return true;
        if (!fieldPath.StartsWith(fieldName, StringComparison.OrdinalIgnoreCase)) return false;
        if (fieldPath.Length == fieldName.Length) return true;
        var boundary = fieldPath[fieldName.Length];
        return boundary is '.' or '[';
    }
}
""";
}
