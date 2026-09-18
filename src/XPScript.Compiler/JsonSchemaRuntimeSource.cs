namespace XPScript.Compiler;

internal static class JsonSchemaRuntimeSource
{
    public const string Code = """
public sealed class XPScriptJsonSchema : IXPScriptJsonNodeConvertible
{
    private readonly System.Text.Json.Nodes.JsonObject _schema;
    public XPScriptJsonSchema() { _schema = new System.Text.Json.Nodes.JsonObject(); }
    private XPScriptJsonSchema(System.Text.Json.Nodes.JsonObject schema) { _schema = (System.Text.Json.Nodes.JsonObject)schema.DeepClone(); XPScriptNativeJson.ValidateBudget(_schema); }

    public static XPScriptJsonSchema Parse(object? value)
    {
        try { var node = System.Text.Json.Nodes.JsonNode.Parse(XPScriptRuntime.CStr(value), documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 }); if (!IsSchemaNode(node)) throw new XPScriptRuntimeException(13, "XPJsonSchema requires a JSON object or boolean schema root."); return new XPScriptJsonSchema(NormalizeSchemaNode(node!)); }
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
    private void SetKeyword(string name, object? value) { if (value is null || XPScriptNullRuntime.IsNull(value)) { _schema.Remove(name); return; } var node = value is XPScriptJsonSchema schema ? schema.ToJsonSchemaObject() : XPScriptNativeJson.ToNode(value); node = NormalizeKeywordValue(name, node); _schema[name] = node; XPScriptNativeJson.ValidateBudget(_schema); }
    private static System.Text.Json.Nodes.JsonNode? NormalizeKeywordValue(string name, System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null) return null;
        if (IsDirectSchemaKeyword(name) && IsSchemaNode(node)) return NormalizeSchemaNode(node);
        if (IsSchemaMapKeyword(name) && node is System.Text.Json.Nodes.JsonObject map) { var wrapper = new System.Text.Json.Nodes.JsonObject { [name] = map.DeepClone() }; return NormalizeSchemaNode(wrapper)[name]?.DeepClone(); }
        if (IsSchemaArrayKeyword(name) && node is System.Text.Json.Nodes.JsonArray array) { var wrapper = new System.Text.Json.Nodes.JsonObject { [name] = array.DeepClone() }; return NormalizeSchemaNode(wrapper)[name]?.DeepClone(); }
        return node;
    }
    private static bool IsDirectSchemaKeyword(string name) => name is "additionalProperties" or "contains" or "propertyNames" or "not" or "if" or "then" or "else" or "items";
    private static bool IsSchemaMapKeyword(string name) => name is "properties" or "patternProperties" or "dependentSchemas" or "$defs";
    private static bool IsSchemaArrayKeyword(string name) => name is "allOf" or "anyOf" or "oneOf" or "prefixItems";
    private string ReadString(string name) => _schema[name] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
    private static System.Text.Json.Nodes.JsonObject ToSchemaObject(object? value) { if (value is XPScriptJsonSchema schema) return schema.ToJsonSchemaObject(); var node = XPScriptNativeJson.ToNode(value); if (!IsSchemaNode(node)) throw new XPScriptRuntimeException(13, "XPJsonSchema child schema must be an XPJsonSchema, JSON object or boolean schema."); XPScriptNativeJson.ValidateBudget(node!); return NormalizeSchemaNode(node!); }
    private static bool IsSchemaNode(System.Text.Json.Nodes.JsonNode? node) => node is System.Text.Json.Nodes.JsonObject || (node is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<bool>(out _));
    private static System.Text.Json.Nodes.JsonObject NormalizeSchemaNode(System.Text.Json.Nodes.JsonNode node)
    {
        if (node is System.Text.Json.Nodes.JsonValue booleanValue && booleanValue.TryGetValue<bool>(out var allowed))
            return allowed ? new System.Text.Json.Nodes.JsonObject() : new System.Text.Json.Nodes.JsonObject { ["not"] = new System.Text.Json.Nodes.JsonObject() };
        if (node is not System.Text.Json.Nodes.JsonObject source) throw new XPScriptRuntimeException(13, "JSON Schema nodes must be objects or booleans.");
        var result = (System.Text.Json.Nodes.JsonObject)source.DeepClone();
        foreach (var name in new[] { "additionalProperties", "contains", "propertyNames", "not", "if", "then", "else", "items" })
            if (result[name] is System.Text.Json.Nodes.JsonNode child && IsSchemaNode(child)) result[name] = NormalizeSchemaNode(child);
        foreach (var name in new[] { "properties", "patternProperties", "dependentSchemas", "$defs" })
            if (result[name] is System.Text.Json.Nodes.JsonObject map)
                foreach (var property in map.ToList())
                    if (property.Value is System.Text.Json.Nodes.JsonNode child && IsSchemaNode(child)) map[property.Key] = NormalizeSchemaNode(child);
        foreach (var name in new[] { "allOf", "anyOf", "oneOf", "prefixItems" })
            if (result[name] is System.Text.Json.Nodes.JsonArray array)
                for (var i = 0; i < array.Count; i++)
                    if (array[i] is System.Text.Json.Nodes.JsonNode child && IsSchemaNode(child)) array[i] = NormalizeSchemaNode(child);
        return result;
    }
    private static System.Text.Json.Nodes.JsonObject TypeSchema(string type) => new() { ["type"] = type };
    private static string ValidateKeyword(object? value) { var name = XPScriptRuntime.CStr(value).Trim(); if (name.Length == 0 || name.Length > 256 || name.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new XPScriptRuntimeException(5, "XPJsonSchema keyword is invalid."); return name; }
    private static string ValidatePropertyName(object? value) { var name = XPScriptRuntime.CStr(value); if (name.Length == 0 || name.Length > 1024 || name.IndexOfAny(['\r', '\n', '\0']) >= 0) throw new XPScriptRuntimeException(5, "XPJsonSchema property name is invalid."); return name; }
}

public sealed class XPScriptJsonValidationResult
{
    private readonly System.Text.Json.Nodes.JsonArray _errors;
    public XPScriptJsonValidationResult(System.Text.Json.Nodes.JsonArray errors) { _errors = errors; }
    public bool Valid => _errors.Count == 0;
    public bool IsValid => Valid;
    public int ErrorCount => _errors.Count;
    public XPScriptJsonArray Errors => new XPScriptJsonArray((System.Text.Json.Nodes.JsonArray)_errors.DeepClone());
    public XPScriptJsonArray FailedPaths
    {
        get
        {
            var paths = new System.Text.Json.Nodes.JsonArray();
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var error in _errors)
            {
                if (error is not System.Text.Json.Nodes.JsonObject obj || obj["path"] is not System.Text.Json.Nodes.JsonValue value || !value.TryGetValue<string>(out var path) || !seen.Add(path)) continue;
                paths.Add(path);
            }
            return new XPScriptJsonArray(paths);
        }
    }
    public XPScriptJsonObject GetError(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _errors.Count) throw new XPScriptRuntimeException(9, "JSON Schema validation error index out of range.");
        return new XPScriptJsonObject((System.Text.Json.Nodes.JsonObject)_errors[index]!.DeepClone());
    }
    public XPScriptJsonDocument Json => new XPScriptJsonDocument(new System.Text.Json.Nodes.JsonObject { ["valid"] = Valid, ["errorCount"] = ErrorCount, ["errors"] = _errors.DeepClone() });
}

public static class XPScriptJsonSchemaValidator
{
    public static XPScriptJsonValidationResult Validate(System.Text.Json.Nodes.JsonObject schema, object? value)
    {
        var errors = new System.Text.Json.Nodes.JsonArray();
        var node = XPScriptNativeJson.ToNode(value);
        ValidateNode(schema, node, "$", "$", errors, 0);
        return new XPScriptJsonValidationResult(errors);
    }

    private static void ValidateNode(System.Text.Json.Nodes.JsonObject schema, System.Text.Json.Nodes.JsonNode? node, string path, string schemaPath, System.Text.Json.Nodes.JsonArray errors, int depth)
    {
        if (depth > 64) { Add(errors, path, SchemaChild(schemaPath, "depth"), "depth", "Validation nesting exceeds 64 levels.", "<= 64", depth.ToString()); return; }
        if (!MatchesTypeKeyword(node, schema["type"], out var expectedType)) { Add(errors, path, SchemaChild(schemaPath, "type"), "type", "Value does not match the required JSON type.", expectedType, ActualType(node)); return; }

        if (schema["allOf"] is System.Text.Json.Nodes.JsonArray allOf)
        {
            for (var i = 0; i < allOf.Count; i++) if (allOf[i] is System.Text.Json.Nodes.JsonObject childSchema && !IsValidAgainst(childSchema, node, path, depth + 1)) Add(errors, path, SchemaChild(schemaPath, "allOf"), "allOf", "Value does not satisfy every allOf schema.", "all schemas", "schema " + i + " failed");
        }
        if (schema["anyOf"] is System.Text.Json.Nodes.JsonArray anyOf)
        {
            var matches = 0; foreach (var child in anyOf) if (child is System.Text.Json.Nodes.JsonObject childSchema && IsValidAgainst(childSchema, node, path, depth + 1)) matches++;
            if (matches == 0) Add(errors, path, SchemaChild(schemaPath, "anyOf"), "anyOf", "Value does not satisfy any anyOf schema.", "at least one schema", "0 schemas");
        }
        if (schema["oneOf"] is System.Text.Json.Nodes.JsonArray oneOf)
        {
            var matches = 0; foreach (var child in oneOf) if (child is System.Text.Json.Nodes.JsonObject childSchema && IsValidAgainst(childSchema, node, path, depth + 1)) matches++;
            if (matches != 1) Add(errors, path, SchemaChild(schemaPath, "oneOf"), "oneOf", "Value must satisfy exactly one oneOf schema.", "1 schema", matches + " schemas");
        }
        if (schema["not"] is System.Text.Json.Nodes.JsonObject notSchema && IsValidAgainst(notSchema, node, path, depth + 1)) Add(errors, path, SchemaChild(schemaPath, "not"), "not", "Value satisfies a schema that must not match.", "schema mismatch", "schema matched");
        if (schema["if"] is System.Text.Json.Nodes.JsonObject ifSchema)
        {
            var conditionMatches = IsValidAgainst(ifSchema, node, path, depth + 1);
            if (conditionMatches && schema["then"] is System.Text.Json.Nodes.JsonObject thenSchema) ValidateNode(thenSchema, node, path, SchemaChild(schemaPath, "then"), errors, depth + 1);
            else if (!conditionMatches && schema["else"] is System.Text.Json.Nodes.JsonObject elseSchema) ValidateNode(elseSchema, node, path, SchemaChild(schemaPath, "else"), errors, depth + 1);
        }

        if (schema["const"] is System.Text.Json.Nodes.JsonNode constNode && !System.Text.Json.Nodes.JsonNode.DeepEquals(node, constNode)) Add(errors, path, SchemaChild(schemaPath, "const"), "const", "Value does not match const.", constNode.ToJsonString(), Display(node));
        if (schema["enum"] is System.Text.Json.Nodes.JsonArray enumValues && !enumValues.Any(x => System.Text.Json.Nodes.JsonNode.DeepEquals(node, x))) Add(errors, path, SchemaChild(schemaPath, "enum"), "enum", "Value is not one of the allowed values.", enumValues.ToJsonString(), Display(node));

        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            var properties = schema["properties"] as System.Text.Json.Nodes.JsonObject;
            var patternProperties = schema["patternProperties"] as System.Text.Json.Nodes.JsonObject;
            if (schema["minProperties"] is System.Text.Json.Nodes.JsonValue minProperties && minProperties.TryGetValue<int>(out var min) && obj.Count < min) Add(errors, path, SchemaChild(schemaPath, "minProperties"), "minProperties", "Object has too few properties.", min.ToString(), obj.Count.ToString());
            if (schema["maxProperties"] is System.Text.Json.Nodes.JsonValue maxProperties && maxProperties.TryGetValue<int>(out var max) && obj.Count > max) Add(errors, path, SchemaChild(schemaPath, "maxProperties"), "maxProperties", "Object has too many properties.", max.ToString(), obj.Count.ToString());
            if (schema["required"] is System.Text.Json.Nodes.JsonArray required) foreach (var item in required) { var name = ReadString(item); if (name.Length > 0 && !obj.ContainsKey(name)) Add(errors, Child(path, name), SchemaChild(schemaPath, "required"), "required", "Required property is missing.", "present", "missing"); }
            if (schema["propertyNames"] is System.Text.Json.Nodes.JsonObject propertyNamesSchema)
                foreach (var property in obj)
                    if (!IsValidAgainst(propertyNamesSchema, System.Text.Json.Nodes.JsonValue.Create(property.Key), Child(path, property.Key), depth + 1)) Add(errors, Child(path, property.Key), SchemaChild(schemaPath, "propertyNames"), "propertyNames", "Property name does not satisfy propertyNames schema.", "matching property name", property.Key);
            if (schema["dependentRequired"] is System.Text.Json.Nodes.JsonObject dependentRequired)
                foreach (var dependency in dependentRequired)
                    if (obj.ContainsKey(dependency.Key) && dependency.Value is System.Text.Json.Nodes.JsonArray names)
                        foreach (var item in names) { var name = ReadString(item); if (name.Length > 0 && !obj.ContainsKey(name)) Add(errors, Child(path, name), SchemaChild(schemaPath, "dependentRequired"), "dependentRequired", "Dependent property is missing.", "present when " + dependency.Key + " is present", "missing"); }
            if (schema["dependentSchemas"] is System.Text.Json.Nodes.JsonObject dependentSchemas)
                foreach (var dependency in dependentSchemas)
                    if (obj.ContainsKey(dependency.Key) && dependency.Value is System.Text.Json.Nodes.JsonObject dependentSchema)
                        ValidateNode(dependentSchema, obj, path, SchemaChild(SchemaChild(schemaPath, "dependentSchemas"), dependency.Key), errors, depth + 1);
            if (properties is not null) foreach (var property in properties) if (property.Value is System.Text.Json.Nodes.JsonObject childSchema && obj.TryGetPropertyValue(property.Key, out var child)) ValidateNode(childSchema, child, Child(path, property.Key), SchemaChild(SchemaChild(schemaPath, "properties"), property.Key), errors, depth + 1);
            if (patternProperties is not null)
                foreach (var pattern in patternProperties)
                    if (pattern.Value is System.Text.Json.Nodes.JsonObject patternSchema)
                    {
                        try { foreach (var property in obj) if (System.Text.RegularExpressions.Regex.IsMatch(property.Key, pattern.Key, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))) ValidateNode(patternSchema, property.Value, Child(path, property.Key), SchemaChild(SchemaChild(schemaPath, "patternProperties"), pattern.Key), errors, depth + 1); }
                        catch (ArgumentException) { Add(errors, path, SchemaChild(schemaPath, "patternProperties"), "patternProperties", "Schema contains an invalid property-name regular expression.", "valid regex", pattern.Key); }
                    }
            foreach (var property in obj)
            {
                var declared = properties is not null && properties.ContainsKey(property.Key);
                var patternMatched = false;
                if (!declared && patternProperties is not null)
                    foreach (var pattern in patternProperties)
                        try { if (System.Text.RegularExpressions.Regex.IsMatch(property.Key, pattern.Key, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))) { patternMatched = true; break; } } catch (ArgumentException) { }
                if (declared || patternMatched) continue;
                if (schema["additionalProperties"] is System.Text.Json.Nodes.JsonValue ap && ap.TryGetValue<bool>(out var allow) && !allow)
                    Add(errors, Child(path, property.Key), SchemaChild(schemaPath, "additionalProperties"), "additionalProperties", "Additional property is not allowed.", "declared or pattern-matched property", property.Key);
                else if (schema["additionalProperties"] is System.Text.Json.Nodes.JsonObject additionalSchema)
                    ValidateNode(additionalSchema, property.Value, Child(path, property.Key), SchemaChild(schemaPath, "additionalProperties"), errors, depth + 1);
            }
        }
        else if (node is System.Text.Json.Nodes.JsonArray array)
        {
            if (schema["minItems"] is System.Text.Json.Nodes.JsonValue minItems && minItems.TryGetValue<int>(out var min) && array.Count < min) Add(errors, path, SchemaChild(schemaPath, "minItems"), "minItems", "Array has too few items.", min.ToString(), array.Count.ToString());
            if (schema["maxItems"] is System.Text.Json.Nodes.JsonValue maxItems && maxItems.TryGetValue<int>(out var max) && array.Count > max) Add(errors, path, SchemaChild(schemaPath, "maxItems"), "maxItems", "Array has too many items.", max.ToString(), array.Count.ToString());
            if (schema["uniqueItems"] is System.Text.Json.Nodes.JsonValue uniqueItems && uniqueItems.TryGetValue<bool>(out var requireUnique) && requireUnique)
            {
                var duplicate = false; for (var i = 0; i < array.Count && !duplicate; i++) for (var j = i + 1; j < array.Count; j++) if (System.Text.Json.Nodes.JsonNode.DeepEquals(array[i], array[j])) { duplicate = true; break; }
                if (duplicate) Add(errors, path, SchemaChild(schemaPath, "uniqueItems"), "uniqueItems", "Array items must be unique.", "unique items", "duplicate items");
            }
            if (schema["contains"] is System.Text.Json.Nodes.JsonObject containsSchema)
            {
                var matches = 0; for (var i = 0; i < array.Count; i++) if (IsValidAgainst(containsSchema, array[i], path + "[" + i + "]", depth + 1)) matches++;
                var minContains = 1; if (schema["minContains"] is System.Text.Json.Nodes.JsonValue minContainsValue && minContainsValue.TryGetValue<int>(out var configuredMin) && configuredMin >= 0) minContains = configuredMin;
                int? maxContains = null; if (schema["maxContains"] is System.Text.Json.Nodes.JsonValue maxContainsValue && maxContainsValue.TryGetValue<int>(out var configuredMax) && configuredMax >= 0) maxContains = configuredMax;
                if (matches < minContains) Add(errors, path, SchemaChild(schemaPath, "contains"), "contains", "Array has too few items matching contains.", "at least " + minContains + " matching items", matches + " matching items");
                if (maxContains.HasValue && matches > maxContains.Value) Add(errors, path, SchemaChild(schemaPath, "maxContains"), "maxContains", "Array has too many items matching contains.", "at most " + maxContains.Value + " matching items", matches + " matching items");
            }
            var prefixCount = 0;
            if (schema["prefixItems"] is System.Text.Json.Nodes.JsonArray prefixItems)
            {
                prefixCount = prefixItems.Count;
                for (var i = 0; i < prefixItems.Count && i < array.Count; i++) if (prefixItems[i] is System.Text.Json.Nodes.JsonObject prefixSchema) ValidateNode(prefixSchema, array[i], path + "[" + i + "]", SchemaChild(SchemaChild(schemaPath, "prefixItems"), i.ToString()), errors, depth + 1);
            }
            if (schema["items"] is System.Text.Json.Nodes.JsonObject itemSchema) for (var i = prefixCount; i < array.Count; i++) ValidateNode(itemSchema, array[i], path + "[" + i + "]", SchemaChild(schemaPath, "items"), errors, depth + 1);
        }
        else if (node is System.Text.Json.Nodes.JsonValue scalar)
        {
            if (scalar.TryGetValue<string>(out var text))
            {
                if (schema["minLength"] is System.Text.Json.Nodes.JsonValue minLength && minLength.TryGetValue<int>(out var min) && text.Length < min) Add(errors, path, SchemaChild(schemaPath, "minLength"), "minLength", "String is shorter than allowed.", min.ToString(), text.Length.ToString());
                if (schema["maxLength"] is System.Text.Json.Nodes.JsonValue maxLength && maxLength.TryGetValue<int>(out var max) && text.Length > max) Add(errors, path, SchemaChild(schemaPath, "maxLength"), "maxLength", "String is longer than allowed.", max.ToString(), text.Length.ToString());
                if (schema["pattern"] is System.Text.Json.Nodes.JsonValue pattern && pattern.TryGetValue<string>(out var regex)) { try { if (!System.Text.RegularExpressions.Regex.IsMatch(text, regex, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250))) Add(errors, path, SchemaChild(schemaPath, "pattern"), "pattern", "String does not match the required pattern.", regex, text); } catch (ArgumentException) { Add(errors, path, SchemaChild(schemaPath, "pattern"), "pattern", "Schema contains an invalid regular expression.", "valid regex", regex); } }
            }
            if (TryNumber(scalar, out var number))
            {
                if (TryNumber(schema["minimum"], out var minimum) && number < minimum) Add(errors, path, SchemaChild(schemaPath, "minimum"), "minimum", "Number is below the minimum.", minimum.ToString(System.Globalization.CultureInfo.InvariantCulture), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (TryNumber(schema["maximum"], out var maximum) && number > maximum) Add(errors, path, SchemaChild(schemaPath, "maximum"), "maximum", "Number is above the maximum.", maximum.ToString(System.Globalization.CultureInfo.InvariantCulture), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (TryNumber(schema["exclusiveMinimum"], out var exclusiveMinimum) && number <= exclusiveMinimum) Add(errors, path, SchemaChild(schemaPath, "exclusiveMinimum"), "exclusiveMinimum", "Number must be greater than the exclusive minimum.", exclusiveMinimum.ToString(System.Globalization.CultureInfo.InvariantCulture), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (TryNumber(schema["exclusiveMaximum"], out var exclusiveMaximum) && number >= exclusiveMaximum) Add(errors, path, SchemaChild(schemaPath, "exclusiveMaximum"), "exclusiveMaximum", "Number must be less than the exclusive maximum.", exclusiveMaximum.ToString(System.Globalization.CultureInfo.InvariantCulture), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (TryNumber(schema["multipleOf"], out var multipleOf) && multipleOf > 0 && number % multipleOf != 0) Add(errors, path, SchemaChild(schemaPath, "multipleOf"), "multipleOf", "Number is not a multiple of the required value.", multipleOf.ToString(System.Globalization.CultureInfo.InvariantCulture), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    private static bool IsValidAgainst(System.Text.Json.Nodes.JsonObject schema, System.Text.Json.Nodes.JsonNode? node, string path, int depth) { var branchErrors = new System.Text.Json.Nodes.JsonArray(); ValidateNode(schema, node, path, "$", branchErrors, depth); return branchErrors.Count == 0; }
    private static bool MatchesTypeKeyword(System.Text.Json.Nodes.JsonNode? node, System.Text.Json.Nodes.JsonNode? typeNode, out string expected) { if (typeNode is System.Text.Json.Nodes.JsonValue value && value.TryGetValue<string>(out var single)) { expected = single; return MatchesType(node, single); } if (typeNode is System.Text.Json.Nodes.JsonArray types) { var names = new System.Collections.Generic.List<string>(); foreach (var item in types) { var name = ReadString(item); if (name.Length == 0) continue; names.Add(name); if (MatchesType(node, name)) { expected = types.ToJsonString(); return true; } } expected = types.ToJsonString(); return names.Count == 0; } expected = string.Empty; return true; }
    private static bool MatchesType(System.Text.Json.Nodes.JsonNode? node, string type) => type.ToLowerInvariant() switch { "null" => node is null, "object" => node is System.Text.Json.Nodes.JsonObject, "array" => node is System.Text.Json.Nodes.JsonArray, "boolean" => node is System.Text.Json.Nodes.JsonValue b && b.TryGetValue<bool>(out _), "string" => node is System.Text.Json.Nodes.JsonValue s && s.TryGetValue<string>(out _), "integer" => IsInteger(node), "number" => node is System.Text.Json.Nodes.JsonValue n && TryNumber(n, out _), _ => true };
    private static bool IsInteger(System.Text.Json.Nodes.JsonNode? node) { if (node is not System.Text.Json.Nodes.JsonValue value || value.TryGetValue<bool>(out _)) return false; if (value.TryGetValue<int>(out _) || value.TryGetValue<long>(out _)) return true; return TryNumber(value, out var number) && decimal.Truncate(number) == number; }
    private static bool TryNumber(System.Text.Json.Nodes.JsonNode? node, out decimal value) { value = 0; if (node is not System.Text.Json.Nodes.JsonValue v || v.TryGetValue<bool>(out _)) return false; if (v.TryGetValue<decimal>(out value)) return true; if (v.TryGetValue<int>(out var i)) { value = i; return true; } if (v.TryGetValue<long>(out var l)) { value = l; return true; } if (v.TryGetValue<double>(out var d) && double.IsFinite(d)) { try { value = (decimal)d; return true; } catch (OverflowException) { return false; } } return false; }
    private static string ActualType(System.Text.Json.Nodes.JsonNode? node) { if (node is null) return "null"; if (node is System.Text.Json.Nodes.JsonObject) return "object"; if (node is System.Text.Json.Nodes.JsonArray) return "array"; if (node is System.Text.Json.Nodes.JsonValue v) { if (v.TryGetValue<bool>(out _)) return "boolean"; if (v.TryGetValue<string>(out _)) return "string"; if (IsInteger(v)) return "integer"; if (TryNumber(v, out _)) return "number"; } return "unknown"; }
    private static string ReadString(System.Text.Json.Nodes.JsonNode? node) => node is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<string>(out var s) ? s : string.Empty;
    private static string Child(string path, string name) => path + "." + name;
    private static string SchemaChild(string path, string name) => path + "." + name;
    private static string Display(System.Text.Json.Nodes.JsonNode? node) => node?.ToJsonString() ?? "null";
    private static void Add(System.Text.Json.Nodes.JsonArray errors, string path, string schemaPath, string keyword, string message, string expected, string actual) => errors.Add(new System.Text.Json.Nodes.JsonObject { ["path"] = path, ["schemaPath"] = schemaPath, ["keyword"] = keyword, ["message"] = message, ["expected"] = expected, ["actual"] = actual });
}
""";
}