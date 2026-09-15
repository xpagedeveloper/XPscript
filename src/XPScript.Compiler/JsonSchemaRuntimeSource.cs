namespace XPScript.Compiler;

internal static class JsonSchemaRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptJsonSchema
{
    private readonly System.Text.Json.Nodes.JsonObject _schema;
    public XPScriptJsonSchema() { _schema = new System.Text.Json.Nodes.JsonObject(); }
    private XPScriptJsonSchema(System.Text.Json.Nodes.JsonObject schema) { _schema = (System.Text.Json.Nodes.JsonObject)schema.DeepClone(); XPScriptNativeJson.ValidateBudget(_schema); }

    public static XPScriptJsonSchema Parse(object? value)
    {
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(XPScriptRuntime.CStr(value), documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 });
            if (node is not System.Text.Json.Nodes.JsonObject obj) throw new XPScriptRuntimeException(13, "XPJsonSchema requires a JSON object root.");
            return new XPScriptJsonSchema(obj);
        }
        catch (System.Text.Json.JsonException) { throw new XPScriptRuntimeException(13, "XPJsonSchema input is not valid JSON."); }
    }

    public static XPScriptJsonSchema FromJson(object? value)
    {
        var node = XPScriptNativeJson.ToNode(value);
        if (node is not System.Text.Json.Nodes.JsonObject obj) throw new XPScriptRuntimeException(13, "XPJsonSchema requires a JsonObject or JsonDocument with an object root.");
        return new XPScriptJsonSchema(obj);
    }

    public static XPScriptJsonSchema Infer(object? value) => Infer(value, true);
    public static XPScriptJsonSchema Infer(object? value, object? requiredValue)
    {
        var node = XPScriptNativeJson.ToNode(value);
        if (node is null) throw new XPScriptRuntimeException(13, "XPJsonSchema.Infer requires an XPJson value.");
        XPScriptNativeJson.ValidateBudget(node);
        var schema = InferNode(node, requiredValue is null || XPScriptNullRuntime.IsNull(requiredValue) || XPScriptRuntime.CBool(requiredValue), 0);
        return new XPScriptJsonSchema(schema);
    }

    public static XPScriptJsonSchema FromValue(object? value) => Infer(value);
    public static XPScriptJsonSchema FromValue(object? value, object? requiredValue) => Infer(value, requiredValue);

    public XPScriptJsonDocument Json => new XPScriptJsonDocument(_schema.DeepClone());
    public string Text => _schema.ToJsonString();
    public string Type { get => ReadString("type"); set => SetKeyword("type", value); }
    public string Title { get => ReadString("title"); set => SetKeyword("title", value); }
    public string Description { get => ReadString("description"); set => SetKeyword("description", value); }
    public bool AdditionalProperties { get => _schema["additionalProperties"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<bool>(out var b) ? b : true; set => _schema["additionalProperties"] = value; }

    public XPScriptJsonSchema Set(object? nameValue, object? value) { SetKeyword(ValidateKeyword(nameValue), value); return this; }
    public XPScriptJsonSchema Remove(object? nameValue) { _schema.Remove(ValidateKeyword(nameValue)); return this; }
    public XPScriptJsonSchema AddProperty(object? nameValue, object? schemaValue) => AddProperty(nameValue, schemaValue, false);
    public XPScriptJsonSchema AddProperty(object? nameValue, object? schemaValue, object? requiredValue)
    {
        var name = ValidatePropertyName(nameValue); var schema = ToSchemaObject(schemaValue);
        var properties = _schema["properties"] as System.Text.Json.Nodes.JsonObject;
        if (properties is null) { properties = new System.Text.Json.Nodes.JsonObject(); _schema["properties"] = properties; }
        properties[name] = schema;
        if (requiredValue is not null && !XPScriptNullRuntime.IsNull(requiredValue) && XPScriptRuntime.CBool(requiredValue)) AddRequired(name);
        return this;
    }
    public XPScriptJsonSchema AddJson(object? nameValue, object? value) => AddJson(nameValue, value, false);
    public XPScriptJsonSchema AddJson(object? nameValue, object? value, object? requiredValue) => AddProperty(nameValue, Infer(value, true), requiredValue);
    public XPScriptJsonSchema AddString(object? nameValue) => AddString(nameValue, false);
    public XPScriptJsonSchema AddString(object? nameValue, object? requiredValue) => AddProperty(nameValue, TypeSchema("string"), requiredValue);
    public XPScriptJsonSchema AddInteger(object? nameValue) => AddInteger(nameValue, false);
    public XPScriptJsonSchema AddInteger(object? nameValue, object? requiredValue) => AddProperty(nameValue, TypeSchema("integer"), requiredValue);
    public XPScriptJsonSchema AddNumber(object? nameValue) => AddNumber(nameValue, false);
    public XPScriptJsonSchema AddNumber(object? nameValue, object? requiredValue) => AddProperty(nameValue, TypeSchema("number"), requiredValue);
    public XPScriptJsonSchema AddBoolean(object? nameValue) => AddBoolean(nameValue, false);
    public XPScriptJsonSchema AddBoolean(object? nameValue, object? requiredValue) => AddProperty(nameValue, TypeSchema("boolean"), requiredValue);
    public XPScriptJsonSchema AddObject(object? nameValue) => AddObject(nameValue, false);
    public XPScriptJsonSchema AddObject(object? nameValue, object? requiredValue) => AddProperty(nameValue, TypeSchema("object"), requiredValue);
    public XPScriptJsonSchema AddArray(object? nameValue, object? itemSchema) => AddArray(nameValue, itemSchema, false);
    public XPScriptJsonSchema AddArray(object? nameValue, object? itemSchema, object? requiredValue) => AddProperty(nameValue, new System.Text.Json.Nodes.JsonObject { ["type"] = "array", ["items"] = ToSchemaObject(itemSchema) }, requiredValue);
    public XPScriptJsonSchema Require(object? nameValue) { AddRequired(ValidatePropertyName(nameValue)); return this; }
    public XPScriptJsonSchema Enum(object? values) { var node = XPScriptNativeJson.ToNode(values); if (node is not System.Text.Json.Nodes.JsonArray array) throw new XPScriptRuntimeException(13, "XPJsonSchema.Enum requires an array."); _schema["enum"] = array.DeepClone(); return this; }
    public XPScriptJsonSchema Items(object? schemaValue) { _schema["items"] = ToSchemaObject(schemaValue); return this; }
    public XPScriptJsonSchema Clone() => new XPScriptJsonSchema(_schema);
    public override string ToString() => Text;
    internal System.Text.Json.Nodes.JsonObject ToJsonSchemaObject() => (System.Text.Json.Nodes.JsonObject)_schema.DeepClone();

    private static System.Text.Json.Nodes.JsonObject InferNode(System.Text.Json.Nodes.JsonNode node, bool requiredProperties, int depth)
    {
        if (depth > 32) throw new XPScriptRuntimeException(5, "XPJsonSchema inferred schema nesting exceeds 32 levels.");
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            var properties = new System.Text.Json.Nodes.JsonObject();
            var required = new System.Text.Json.Nodes.JsonArray();
            foreach (var property in obj)
            {
                properties[property.Key] = property.Value is null ? new System.Text.Json.Nodes.JsonObject() : InferNode(property.Value, requiredProperties, depth + 1);
                if (requiredProperties) required.Add(property.Key);
            }
            var result = new System.Text.Json.Nodes.JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
            if (requiredProperties && required.Count > 0) result["required"] = required;
            return result;
        }
        if (node is System.Text.Json.Nodes.JsonArray array)
        {
            System.Text.Json.Nodes.JsonObject items = new();
            foreach (var item in array)
            {
                if (item is null) continue;
                items = InferNode(item, requiredProperties, depth + 1);
                break;
            }
            return new System.Text.Json.Nodes.JsonObject { ["type"] = "array", ["items"] = items };
        }
        if (node is System.Text.Json.Nodes.JsonValue value)
        {
            if (value.TryGetValue<bool>(out _)) return TypeSchema("boolean");
            if (value.TryGetValue<int>(out _) || value.TryGetValue<long>(out _)) return TypeSchema("integer");
            if (value.TryGetValue<double>(out _) || value.TryGetValue<decimal>(out _)) return TypeSchema("number");
            if (value.TryGetValue<string>(out _)) return TypeSchema("string");
        }
        return new System.Text.Json.Nodes.JsonObject();
    }

    private void AddRequired(string name)
    {
        var required = _schema["required"] as System.Text.Json.Nodes.JsonArray;
        if (required is null) { required = new System.Text.Json.Nodes.JsonArray(); _schema["required"] = required; }
        foreach (var item in required) if (item is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var existing) && string.Equals(existing, name, StringComparison.Ordinal)) return;
        required.Add(name);
    }
    private void SetKeyword(string name, object? value)
    {
        if (value is null || XPScriptNullRuntime.IsNull(value)) { _schema.Remove(name); return; }
        _schema[name] = value is XPScriptJsonSchema schema ? schema.ToJsonSchemaObject() : XPScriptNativeJson.ToNode(value);
        XPScriptNativeJson.ValidateBudget(_schema);
    }
    private string ReadString(string name) => _schema[name] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
    private static System.Text.Json.Nodes.JsonObject ToSchemaObject(object? value)
    {
        if (value is XPScriptJsonSchema schema) return schema.ToJsonSchemaObject();
        var node = XPScriptNativeJson.ToNode(value);
        if (node is not System.Text.Json.Nodes.JsonObject obj) throw new XPScriptRuntimeException(13, "XPJsonSchema child schema must be an XPJsonSchema, JsonObject or JsonDocument with an object root.");
        XPScriptNativeJson.ValidateBudget(obj); return (System.Text.Json.Nodes.JsonObject)obj.DeepClone();
    }
    private static System.Text.Json.Nodes.JsonObject TypeSchema(string type) => new() { ["type"] = type };
    private static string ValidateKeyword(object? value) { var name = XPScriptRuntime.CStr(value).Trim(); if (name.Length == 0 || name.Length > 256 || name.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new XPScriptRuntimeException(5, "XPJsonSchema keyword is invalid."); return name; }
    private static string ValidatePropertyName(object? value) { var name = XPScriptRuntime.CStr(value); if (name.Length == 0 || name.Length > 1024 || name.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new XPScriptRuntimeException(5, "XPJsonSchema property name is invalid."); return name; }
}
""";
}
