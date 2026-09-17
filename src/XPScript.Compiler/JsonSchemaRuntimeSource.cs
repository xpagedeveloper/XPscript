namespace XPScript.Compiler;

internal static class JsonSchemaRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptJsonSchema : IXPScriptJsonNodeConvertible
{
    private readonly System.Text.Json.Nodes.JsonObject _schema;
    public XPScriptJsonSchema() { _schema = new System.Text.Json.Nodes.JsonObject(); }
    private XPScriptJsonSchema(System.Text.Json.Nodes.JsonObject schema) { _schema = (System.Text.Json.Nodes.JsonObject)schema.DeepClone(); XPScriptNativeJson.ValidateBudget(_schema); }

    public static XPScriptJsonSchema Parse(object? value)
    {
        try { var node = System.Text.Json.Nodes.JsonNode.Parse(XPScriptRuntime.CStr(value), documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 }); if (node is not System.Text.Json.Nodes.JsonObject obj) throw new XPScriptRuntimeException(13, "XPJsonSchema requires a JSON object root."); return new XPScriptJsonSchema(obj); }
        catch (System.Text.Json.JsonException) { throw new XPScriptRuntimeException(13, "XPJsonSchema input is not valid JSON."); }
    }
    public static XPScriptJsonSchema FromJson(object? value) => InferSchema(value, true);

    public XPScriptJsonDocument Json => new XPScriptJsonDocument(_schema.DeepClone());
    public string Text => _schema.ToJsonString();
    public string Type { get => ReadString("type"); set => SetKeyword("type", value); }
    public string Title { get => ReadString("title"); set => SetKeyword("title", value); }
    public string Description { get => ReadString("description"); set => SetKeyword("description", value); }
    public bool AdditionalProperties { get => _schema["additionalProperties"] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<bool>(out var b) ? b : true; set => _schema["additionalProperties"] = value; }
    public XPScriptJsonValidationResult Validate(object? value) => XPScriptJsonSchemaValidator.Validate(_schema, value);
    public bool IsValid(object? value) => Validate(value).Valid;

    public XPScriptJsonSchema Set(object? nameValue, object? value) { SetKeyword(ValidateKeyword(nameValue), value); return this; }
    public XPScriptJsonSchema Remove(object? nameValue) { _schema.Remove(ValidateKeyword(nameValue)); return this; }
    public XPScriptJsonSchema AddProperty(object? nameValue, object? schemaValue) => AddProperty(nameValue, schemaValue, false);
    public XPScriptJsonSchema AddProperty(object? nameValue, object? schemaValue, object? requiredValue) { var name = ValidatePropertyName(nameValue); var schema = ToSchemaObject(schemaValue); var properties = _schema["properties"] as System.Text.Json.Nodes.JsonObject; if (properties is null) { properties = new System.Text.Json.Nodes.JsonObject(); _schema["properties"] = properties; } properties[name] = schema; if (requiredValue is not null && !XPScriptNullRuntime.IsNull(requiredValue) && XPScriptRuntime.CBool(requiredValue)) AddRequired(name); return this; }
    public XPScriptJsonSchema AddJson(object? nameValue, object? value) => AddJson(nameValue, value, false);
    public XPScriptJsonSchema AddJson(object? nameValue, object? value, object? requiredValue) => AddProperty(nameValue, InferSchema(value, true), requiredValue);
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
    System.Text.Json.Nodes.JsonNode? IXPScriptJsonNodeConvertible.ToJsonNode() => ToJsonSchemaObject();
    internal System.Text.Json.Nodes.JsonObject ToJsonSchemaObject() => (System.Text.Json.Nodes.JsonObject)_schema.DeepClone();

    private static XPScriptJsonSchema InferSchema(object? value, bool requiredProperties) { var node = XPScriptNativeJson.ToNode(value); if (node is null) throw new XPScriptRuntimeException(13, "XPJsonSchema.FromJson requires an XPJson value."); XPScriptNativeJson.ValidateBudget(node); return new XPScriptJsonSchema(InferNode(node, requiredProperties, 0)); }
    private static System.Text.Json.Nodes.JsonObject InferNode(System.Text.Json.Nodes.JsonNode node, bool requiredProperties, int depth) { if (depth > 32) throw new XPScriptRuntimeException(5, "XPJsonSchema inferred schema nesting exceeds 32 levels."); if (node is System.Text.Json.Nodes.JsonObject obj) { var properties = new System.Text.Json.Nodes.JsonObject(); var required = new System.Text.Json.Nodes.JsonArray(); foreach (var property in obj) { properties[property.Key] = property.Value is null ? new System.Text.Json.Nodes.JsonObject() : InferNode(property.Value, requiredProperties, depth + 1); if (requiredProperties) required.Add(property.Key); } var result = new System.Text.Json.Nodes.JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false }; if (requiredProperties && required.Count > 0) result["required"] = required; return result; } if (node is System.Text.Json.Nodes.JsonArray array) { System.Text.Json.Nodes.JsonObject items = new(); foreach (var item in array) { if (item is null) continue; items = InferNode(item, requiredProperties, depth + 1); break; } return new System.Text.Json.Nodes.JsonObject { ["type"] = "array", ["items"] = items }; } if (node is System.Text.Json.Nodes.JsonValue value) { if (value.TryGetValue<bool>(out _)) return TypeSchema("boolean"); if (value.TryGetValue<int>(out _) || value.TryGetValue<long>(out _)) return TypeSchema("integer"); if (value.TryGetValue<double>(out _) || value.TryGetValue<decimal>(out _)) return TypeSchema("number"); if (value.TryGetValue<string>(out _)) return TypeSchema("string"); } return new System.Text.Json.Nodes.JsonObject(); }
    private void AddRequired(string name) { var required = _schema["required"] as System.Text.Json.Nodes.JsonArray; if (required is null) { required = new System.Text.Json.Nodes.JsonArray(); _schema["required"] = required; } foreach (var item in required) if (item is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var existing) && string.Equals(existing, name, StringComparison.Ordinal)) return; required.Add(name); }
    private void SetKeyword(string name, object? value) { if (value is null || XPScriptNullRuntime.IsNull(value)) { _schema.Remove(name); return; } _schema[name] = value is XPScriptJsonSchema schema ? schema.ToJsonSchemaObject() : XPScriptNativeJson.ToNode(value); XPScriptNativeJson.ValidateBudget(_schema); }
    private string ReadString(string name) => _schema[name] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
    private static System.Text.Json.Nodes.JsonObject ToSchemaObject(object? value) { if (value is XPScriptJsonSchema schema) return schema.ToJsonSchemaObject(); var node = XPScriptNativeJson.ToNode(value); if (node is not System.Text.Json.Nodes.JsonObject obj) throw new XPScriptRuntimeException(13, "XPJsonSchema child schema must be an XPJsonSchema, JsonObject or JsonDocument with an object root."); XPScriptNativeJson.ValidateBudget(obj); return (System.Text.Json.Nodes.JsonObject)obj.DeepClone(); }
    private static System.Text.Json.Nodes.JsonObject TypeSchema(string type) => new() { ["type"] = type };
    private static string ValidateKeyword(object? value) { var name = XPScriptRuntime.CStr(value).Trim(); if (name.Length == 0 || name.Length > 256 || name.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new XPScriptRuntimeException(5, "XPJsonSchema keyword is invalid."); return name; }
    private static string ValidatePropertyName(object? value) { var name = XPScriptRuntime.CStr(value); if (name.Length == 0 || name.Length > 1024 || name.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new XPScriptRuntimeException(5, "XPJsonSchema property name is invalid."); return name; }
}

internal sealed class XPScriptJsonValidationResult
{
    private readonly System.Text.Json.Nodes.JsonArray _errors;
    internal XPScriptJsonValidationResult(System.Text.Json.Nodes.JsonArray errors) { _errors = errors; }
    public bool Valid => _errors.Count == 0;
    public int ErrorCount => _errors.Count;
    public XPScriptJsonArray Errors => new XPScriptJsonArray((System.Text.Json.Nodes.JsonArray)_errors.DeepClone());
    public XPScriptJsonDocument Json => new XPScriptJsonDocument(new System.Text.Json.Nodes.JsonObject { ["valid"] = Valid, ["errorCount"] = ErrorCount, ["errors"] = _errors.DeepClone() });
}

internal static class XPScriptJsonSchemaValidator
{
    public static XPScriptJsonValidationResult Validate(System.Text.Json.Nodes.JsonObject schema, object? value)
    {
        var errors = new System.Text.Json.Nodes.JsonArray();
        var node = XPScriptNativeJson.ToNode(value);
        ValidateNode(schema, node, "$", errors, 0);
        return new XPScriptJsonValidationResult(errors);
    }

    private static void ValidateNode(System.Text.Json.Nodes.JsonObject schema, System.Text.Json.Nodes.JsonNode? node, string path, System.Text.Json.Nodes.JsonArray errors, int depth)
    {
        if (depth > 64) { Add(errors, path, "depth", "Validation nesting exceeds 64 levels.", "<= 64", depth.ToString()); return; }
        var expectedType = ReadString(schema["type"]);
        if (expectedType.Length > 0 && !MatchesType(node, expectedType)) { Add(errors, path, "type", "Value does not match the required JSON type.", expectedType, ActualType(node)); return; }

        if (schema["allOf"] is System.Text.Json.Nodes.JsonArray allOf)
        {
            for (var i = 0; i < allOf.Count; i++)
                if (allOf[i] is System.Text.Json.Nodes.JsonObject childSchema && !IsValidAgainst(childSchema, node, path, depth + 1))
                    Add(errors, path, "allOf", "Value does not satisfy every allOf schema.", "all schemas", "schema " + i + " failed");
        }
        if (schema["anyOf"] is System.Text.Json.Nodes.JsonArray anyOf)
        {
            var matches = 0;
            foreach (var child in anyOf) if (child is System.Text.Json.Nodes.JsonObject childSchema && IsValidAgainst(childSchema, node, path, depth + 1)) matches++;
            if (matches == 0) Add(errors, path, "anyOf", "Value does not satisfy any anyOf schema.", "at least one schema", "0 schemas");
        }
        if (schema["oneOf"] is System.Text.Json.Nodes.JsonArray oneOf)
        {
            var matches = 0;
            foreach (var child in oneOf) if (child is System.Text.Json.Nodes.JsonObject childSchema && IsValidAgainst(childSchema, node, path, depth + 1)) matches++;
            if (matches != 1) Add(errors, path, "oneOf", "Value must satisfy exactly one oneOf schema.", "1 schema", matches + " schemas");
        }
        if (schema["not"] is System.Text.Json.Nodes.JsonObject notSchema && IsValidAgainst(notSchema, node, path, depth + 1))
            Add(errors, path, "not", "Value satisfies a schema that must not match.", "schema mismatch", "schema matched");

        if (schema["const"] is System.Text.Json.Nodes.JsonNode constNode && !System.Text.Json.Nodes.JsonNode.DeepEquals(node, constNode)) Add(errors, path, "const", "Value does not match const.", constNode.ToJsonString(), Display(node));
        if (schema["enum"] is System.Text.Json.Nodes.JsonArray enumValues && !enumValues.Any(x => System.Text.Json.Nodes.JsonNode.DeepEquals(node, x))) Add(errors, path, "enum", "Value is not one of the allowed values.", enumValues.ToJsonString(), Display(node));

        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            var properties = schema["properties"] as System.Text.Json.Nodes.JsonObject;
            if (schema["minProperties"] is System.Text.Json.Nodes.JsonValue minProperties && minProperties.TryGetValue<int>(out var min) && obj.Count < min) Add(errors, path, "minProperties", "Object has too few properties.", min.ToString(), obj.Count.ToString());
            if (schema["maxProperties"] is System.Text.Json.Nodes.JsonValue maxProperties && maxProperties.TryGetValue<int>(out var max) && obj.Count > max) Add(errors, path, "maxProperties", "Object has too many properties.", max.ToString(), obj.Count.ToString());
            if (schema["required"] is System.Text.Json.Nodes.JsonArray required)
                foreach (var item in required) { var name = ReadString(item); if (name.Length > 0 && !obj.ContainsKey(name)) Add(errors, Child(path, name), "required", "Required property is missing.", "present", "missing"); }
            if (properties is not null)
                foreach (var property in properties) if (property.Value is System.Text.Json.Nodes.JsonObject childSchema && obj.TryGetPropertyValue(property.Key, out var child)) ValidateNode(childSchema, child, Child(path, property.Key), errors, depth + 1);
            if (schema["additionalProperties"] is System.Text.Json.Nodes.JsonValue ap && ap.TryGetValue<bool>(out var allow) && !allow)
                foreach (var property in obj) if (properties is null || !properties.ContainsKey(property.Key)) Add(errors, Child(path, property.Key), "additionalProperties", "Additional property is not allowed.", "declared property", property.Key);
        }
        else if (node is System.Text.Json.Nodes.JsonArray array)
        {
            if (schema["minItems"] is System.Text.Json.Nodes.JsonValue minItems && minItems.TryGetValue<int>(out var min) && array.Count < min) Add(errors, path, "minItems", "Array has too few items.", min.ToString(), array.Count.ToString());
            if (schema["maxItems"] is System.Text.Json.Nodes.JsonValue maxItems && maxItems.TryGetValue<int>(out var max) && array.Count > max) Add(errors, path, "maxItems", "Array has too many items.", max.ToString(), array.Count.ToString());
            if (schema["uniqueItems"] is System.Text.Json.Nodes.JsonValue uniqueItems && uniqueItems.TryGetValue<bool>(out var requireUnique) && requireUnique)
            {
                var duplicate = false;
                for (var i = 0; i < array.Count && !duplicate; i++)
                    for (var j = i + 1; j < array.Count; j++)
                        if (System.Text.Json.Nodes.JsonNode.DeepEquals(array[i], array[j])) { duplicate = true; break; }
                if (duplicate) Add(errors, path, "uniqueItems", "Array items must be unique.", "unique items", "duplicate items");
            }
            if (schema["items"] is System.Text.Json.Nodes.JsonObject itemSchema) for (var i = 0; i < array.Count; i++) ValidateNode(itemSchema, array[i], path + "[" + i + "]", errors, depth + 1);
        }
        else if (node is System.Text.Json.Nodes.JsonValue scalar)
        {
            if (scalar.TryGetValue<string>(out var text))
            {
                if (schema["minLength"] is System.Text.Json.Nodes.JsonValue minLength && minLength.TryGetValue<int>(out var min) && text.Length < min) Add(errors, path, "minLength", "String is shorter than allowed.", min.ToString(), text.Length.ToString());
                if (schema["maxLength"] is System.Text.Json.Nodes.JsonValue maxLength && maxLength.TryGetValue<int>(out var max) && text.Length > max) Add(errors, path, "maxLength", "String is longer than allowed.", max.ToString(), text.Length.ToString());
                if (schema["pattern"] is System.Text.Json.Nodes.JsonValue pattern && pattern.TryGetValue<string>(out var regex)) { try { if (!System.Text.RegularExpressions.Regex.IsMatch(text, regex, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))) Add(errors, path, "pattern", "String does not match the required pattern.", regex, text); } catch (ArgumentException) { Add(errors, path, "pattern", "Schema contains an invalid regular expression.", "valid regex", regex); } }
            }
            if (TryNumber(scalar, out var number))
            {
                if (TryNumber(schema["minimum"], out var minimum) && number < minimum) Add(errors, path, "minimum", "Number is below the minimum.", minimum.ToString(System.Globalization.CultureInfo.InvariantCulture), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (TryNumber(schema["maximum"], out var maximum) && number > maximum) Add(errors, path, "maximum", "Number is above the maximum.", maximum.ToString(System.Globalization.CultureInfo.InvariantCulture), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    private static bool IsValidAgainst(System.Text.Json.Nodes.JsonObject schema, System.Text.Json.Nodes.JsonNode? node, string path, int depth)
    {
        var branchErrors = new System.Text.Json.Nodes.JsonArray();
        ValidateNode(schema, node, path, branchErrors, depth);
        return branchErrors.Count == 0;
    }
    private static bool MatchesType(System.Text.Json.Nodes.JsonNode? node, string type) => type.ToLowerInvariant() switch { "null" => node is null, "object" => node is System.Text.Json.Nodes.JsonObject, "array" => node is System.Text.Json.Nodes.JsonArray, "boolean" => node is System.Text.Json.Nodes.JsonValue b && b.TryGetValue<bool>(out _), "string" => node is System.Text.Json.Nodes.JsonValue s && s.TryGetValue<string>(out _), "integer" => node is System.Text.Json.Nodes.JsonValue i && (i.TryGetValue<int>(out _) || i.TryGetValue<long>(out _)), "number" => node is System.Text.Json.Nodes.JsonValue n && TryNumber(n, out _), _ => true };
    private static bool TryNumber(System.Text.Json.Nodes.JsonNode? node, out decimal value) { value = 0; if (node is not System.Text.Json.Nodes.JsonValue v || v.TryGetValue<bool>(out _)) return false; if (v.TryGetValue<decimal>(out value)) return true; if (v.TryGetValue<long>(out var l)) { value = l; return true; } if (v.TryGetValue<double>(out var d) && double.IsFinite(d)) { try { value = (decimal)d; return true; } catch (OverflowException) { return false; } } return false; }
    private static string ActualType(System.Text.Json.Nodes.JsonNode? node) { if (node is null) return "null"; if (node is System.Text.Json.Nodes.JsonObject) return "object"; if (node is System.Text.Json.Nodes.JsonArray) return "array"; if (node is System.Text.Json.Nodes.JsonValue v) { if (v.TryGetValue<bool>(out _)) return "boolean"; if (v.TryGetValue<string>(out _)) return "string"; if (v.TryGetValue<int>(out _) || v.TryGetValue<long>(out _)) return "integer"; if (TryNumber(v, out _)) return "number"; } return "unknown"; }
    private static string ReadString(System.Text.Json.Nodes.JsonNode? node) => node is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
    private static string Child(string path, string name) => path + "." + name;
    private static string Display(System.Text.Json.Nodes.JsonNode? node) => node?.ToJsonString() ?? "null";
    private static void Add(System.Text.Json.Nodes.JsonArray errors, string path, string keyword, string message, string expected, string actual) => errors.Add(new System.Text.Json.Nodes.JsonObject { ["path"] = path, ["keyword"] = keyword, ["message"] = message, ["expected"] = expected, ["actual"] = actual });
}
""";
}
