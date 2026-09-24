using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace XPScript.Web.Compiler;

public sealed record XpsOpenApiGenerationResult(
    string OpenApiVersion,
    string Source,
    IReadOnlyList<string> Operations,
    IReadOnlyList<string> Models);

public sealed class XpsOpenApiGenerationException : Exception
{
    public XpsOpenApiGenerationException(string message) : base(message) { }
    public XpsOpenApiGenerationException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class XpsOpenApiGenerator
{
    private static readonly string[] HttpMethods = ["get", "post", "put", "patch", "delete", "head", "options", "trace"];
    private static readonly Regex IdentifierPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> LexicalKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "And", "As", "Boolean", "ByRef", "ByVal", "Call", "Case", "Class", "Const", "Date", "Dim", "Do", "Double",
        "Each", "Else", "ElseIf", "End", "Enum", "Exit", "False", "For", "Function", "If", "Integer", "Long", "Loop",
        "Mod", "New", "Next", "Nothing", "Not", "Object", "On", "Option", "Or", "Private", "Public", "Select", "Set",
        "Single", "Static", "Step", "String", "Sub", "Then", "To", "True", "Variant", "Wend", "While", "With", "Xor"
    };

    public XpsOpenApiGenerationResult GenerateFile(string specificationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationPath);
        var fullPath = Path.GetFullPath(specificationPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("OpenAPI specification file was not found.", fullPath);
        return Generate(File.ReadAllText(fullPath), Path.GetFileName(fullPath));
    }

    public XpsOpenApiGenerationResult Generate(string specification, string? sourceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specification);
        var root = ParseDocument(specification, sourceName);
        var version = ReadString(root, "openapi")
            ?? throw new XpsOpenApiGenerationException("OpenAPI document is missing the required 'openapi' version field.");
        if (!version.StartsWith("3.0.", StringComparison.Ordinal) && !version.StartsWith("3.1.", StringComparison.Ordinal) && !version.StartsWith("3.2.", StringComparison.Ordinal))
            throw new XpsOpenApiGenerationException($"OpenAPI version '{version}' is unsupported. XPScript supports OpenAPI 3.0.x, 3.1.x and 3.2.x.");

        using var versionScope = XpsOpenApiSchema.UseOpenApiVersion(version);
        var models = CollectComponentModels(root);
        var operations = CollectOperations(root, models);
        if (operations.Count == 0) throw new XpsOpenApiGenerationException("OpenAPI document does not contain any supported path operations.");

        var source = EmitSource(version, sourceName, root, models, operations);
        return new XpsOpenApiGenerationResult(
            version,
            source,
            operations.Select(operation => operation.Name).ToArray(),
            models.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static JsonObject ParseDocument(string specification, string? sourceName)
    {
        try
        {
            var trimmed = specification.AsSpan().TrimStart();
            if (!trimmed.IsEmpty && (trimmed[0] == '{' || trimmed[0] == '['))
                return JsonNode.Parse(specification) as JsonObject
                    ?? throw new XpsOpenApiGenerationException("OpenAPI JSON root must be an object.");

            var stream = new YamlStream();
            stream.Load(new StringReader(specification));
            if (stream.Documents.Count != 1)
                throw new XpsOpenApiGenerationException("OpenAPI YAML must contain exactly one document.");
            return ConvertYamlNode(stream.Documents[0].RootNode) as JsonObject
                ?? throw new XpsOpenApiGenerationException("OpenAPI YAML root must be a mapping/object.");
        }
        catch (XpsOpenApiGenerationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or YamlException or FormatException)
        {
            throw new XpsOpenApiGenerationException($"Unable to parse OpenAPI specification{FormatSourceName(sourceName)}: {ex.Message}", ex);
        }
    }

    private static JsonNode? ConvertYamlNode(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
            {
                var result = new JsonObject();
                foreach (var entry in mapping.Children)
                {
                    if (entry.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value))
                        throw new XpsOpenApiGenerationException("OpenAPI YAML mapping keys must be non-empty scalar strings.");
                    if (result.ContainsKey(key.Value))
                        throw new XpsOpenApiGenerationException($"OpenAPI YAML contains duplicate key '{key.Value}'.");
                    result[key.Value] = ConvertYamlNode(entry.Value);
                }
                return result;
            }
            case YamlSequenceNode sequence:
            {
                var result = new JsonArray();
                foreach (var child in sequence.Children) result.Add(ConvertYamlNode(child));
                return result;
            }
            case YamlScalarNode scalar:
                return ConvertYamlScalar(scalar);
            default:
                throw new XpsOpenApiGenerationException($"Unsupported YAML node type '{node.NodeType}'.");
        }
    }

    private static JsonNode? ConvertYamlScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style is ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted or ScalarStyle.Literal or ScalarStyle.Folded)
            return JsonValue.Create(value);
        if (value is "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase)) return null;
        if (bool.TryParse(value, out var boolean)) return JsonValue.Create(boolean);
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return JsonValue.Create(integer);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return JsonValue.Create(number);
        return JsonValue.Create(value);
    }

    private static Dictionary<string, JsonObject> CollectComponentModels(JsonObject root)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (root["components"] is not JsonObject components || components["schemas"] is not JsonObject schemas) return result;
        foreach (var pair in schemas)
        {
            if (pair.Value is not JsonObject schema)
                throw new XpsOpenApiGenerationException($"components.schemas.{pair.Key} must be an object.");
            var modelName = ToTypeIdentifier(pair.Key, $"schema '{pair.Key}'");
            if (!result.TryAdd(modelName, schema))
                throw new XpsOpenApiGenerationException($"OpenAPI schemas generate duplicate XPScript class name '{modelName}'.");
        }
        return result;
    }

    private static List<OperationModel> CollectOperations(JsonObject root, Dictionary<string, JsonObject> models)
    {
        if (root["paths"] is not JsonObject paths)
            throw new XpsOpenApiGenerationException("OpenAPI document is missing the required 'paths' object.");

        var operations = new List<OperationModel>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootSecurity = root["security"];
        ValidateSecuritySchemes(root);

        foreach (var pathPair in paths)
        {
            if (pathPair.Value is not JsonObject pathItem) continue;
            var pathParameters = ReadParameters(root, pathItem["parameters"], $"paths.{pathPair.Key}.parameters");
            foreach (var method in HttpMethods)
            {
                if (pathItem[method] is not JsonObject operation) continue;
                var rawName = ReadString(operation, "operationId") ?? BuildOperationName(method, pathPair.Key);
                var name = UniqueOperationName(ToTypeIdentifier(rawName, $"operation '{method.ToUpperInvariant()} {pathPair.Key}'"), usedNames);
                var parameters = MergeParameters(pathParameters, ReadParameters(root, operation["parameters"], $"{method.ToUpperInvariant()} {pathPair.Key} parameters"));
                EnsureUniqueParameters(parameters, method, pathPair.Key);
                var body = ReadRequestBody(root, operation["requestBody"], name, models, $"{method.ToUpperInvariant()} {pathPair.Key} requestBody");
                var responses = ReadResponses(root, operation["responses"], $"{method.ToUpperInvariant()} {pathPair.Key} responses");
                var security = operation.ContainsKey("security") ? operation["security"] : rootSecurity;
                var authenticated = ReadServerSecurity(root, security, $"{method.ToUpperInvariant()} {pathPair.Key} security");
                operations.Add(new OperationModel(method.ToUpperInvariant(), pathPair.Key, name, parameters, body, responses, authenticated));
            }
        }
        return operations;
    }

    private static void ValidateSecuritySchemes(JsonObject root)
    {
        if (root["components"] is not JsonObject components || components["securitySchemes"] is not JsonObject schemes) return;
        foreach (var pair in schemes)
        {
            if (pair.Value is not JsonObject scheme)
                throw new XpsOpenApiGenerationException($"components.securitySchemes.{pair.Key} must be an object.");
            var type = ReadString(scheme, "type")?.ToLowerInvariant()
                ?? throw new XpsOpenApiGenerationException($"Security scheme '{pair.Key}' is missing 'type'.");
            if (type is not ("apikey" or "http" or "oauth2" or "openidconnect" or "mutualtls"))
                throw new XpsOpenApiGenerationException($"Security scheme '{pair.Key}' uses unsupported type '{type}'.");
        }
    }

    private static bool ReadServerSecurity(JsonObject root, JsonNode? node, string context)
    {
        if (node is null) return false;
        if (node is not JsonArray requirements)
            throw new XpsOpenApiGenerationException($"{context} must be an array.");
        if (requirements.Count == 0) return false;
        if (root["components"] is not JsonObject components || components["securitySchemes"] is not JsonObject schemes)
            throw new XpsOpenApiGenerationException($"{context} references security but components.securitySchemes is missing.");
        var allowsAnonymous = false;
        foreach (var requirementNode in requirements)
        {
            if (requirementNode is not JsonObject requirement)
                throw new XpsOpenApiGenerationException($"{context} contains a security requirement that is not an object.");
            if (requirement.Count == 0)
            {
                allowsAnonymous = true;
                continue;
            }
            foreach (var pair in requirement)
            {
                if (!schemes.ContainsKey(pair.Key))
                    throw new XpsOpenApiGenerationException($"{context} references undefined security scheme '{pair.Key}'.");
                if (pair.Value is not JsonArray)
                    throw new XpsOpenApiGenerationException($"{context} scheme '{pair.Key}' must declare an array of scopes.");
            }
        }
        return !allowsAnonymous;
    }

    private static List<ParameterModel> ReadParameters(JsonObject root, JsonNode? node, string context)
    {
        var result = new List<ParameterModel>();
        if (node is null) return result;
        if (node is not JsonArray array) throw new XpsOpenApiGenerationException($"{context} must be an array.");
        foreach (var entry in array)
        {
            var parameter = XpsOpenApiSchema.Resolve(root, entry, context);
            var name = ReadString(parameter, "name") ?? throw new XpsOpenApiGenerationException($"{context} contains a parameter without a name.");
            var location = ReadString(parameter, "in")?.ToLowerInvariant()
                ?? throw new XpsOpenApiGenerationException($"Parameter '{name}' in {context} is missing 'in'.");
            if (location == "path" && !ReadBoolean(parameter, "required"))
                throw new XpsOpenApiGenerationException($"Path parameter '{name}' in {context} must declare required: true.");
            if (location is not ("path" or "query" or "header" or "cookie"))
                throw new XpsOpenApiGenerationException($"Parameter '{name}' uses unsupported location '{location}'. Supported locations are path, query, header and cookie.");
            if (parameter["schema"] is not JsonObject schema)
                throw new XpsOpenApiGenerationException($"Parameter '{name}' in {context} must declare a schema.");
            var type = new XpsType(XpsOpenApiSchema.XpsType(root, schema, $"parameter '{name}'"), XpsOpenApiSchema.IsObjectType(root, schema, $"parameter '{name}'"));
            result.Add(new ParameterModel(name, location, type.TypeName, type.IsObject, ReadBoolean(parameter, "required")));
        }
        return result;
    }

    private static List<ParameterModel> MergeParameters(IReadOnlyList<ParameterModel> inherited, IReadOnlyList<ParameterModel> operation)
    {
        var result = new List<ParameterModel>(inherited);
        foreach (var parameter in operation)
        {
            var index = result.FindIndex(existing =>
                existing.Name.Equals(parameter.Name, StringComparison.Ordinal) &&
                existing.Location.Equals(parameter.Location, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) result[index] = parameter;
            else result.Add(parameter);
        }
        return result;
    }

    private static RequestBodyModel? ReadRequestBody(
        JsonObject root,
        JsonNode? node,
        string operationName,
        Dictionary<string, JsonObject> models,
        string context)
    {
        if (node is null) return null;
        var requestBody = XpsOpenApiSchema.Resolve(root, node, context);
        if (requestBody["content"] is not JsonObject content)
            throw new XpsOpenApiGenerationException($"{context} must declare content.");
        var media = SelectJsonMediaType(content, context);
        if (media["schema"] is not JsonObject schema)
            throw new XpsOpenApiGenerationException($"{context} JSON content must declare a schema.");

        if (XpsOpenApiSchema.TryGetReference(schema, out var reference))
        {
            var resolved = XpsOpenApiSchema.Resolve(root, schema, context);
            return new RequestBodyModel(XpsOpenApiSchema.ReferenceTypeName(reference, context), true, ReadBoolean(requestBody, "required"), resolved);
        }

        var type = XpsOpenApiSchema.PrimaryType(schema);
        if (type == "object" || schema.ContainsKey("properties"))
        {
            var modelName = UniqueModelName(operationName + "Body", models.Keys);
            models.Add(modelName, schema);
            return new RequestBodyModel(modelName, true, ReadBoolean(requestBody, "required"), schema);
        }

        var scalarType = new XpsType(XpsOpenApiSchema.XpsType(root, schema, context), XpsOpenApiSchema.IsObjectType(root, schema, context));
        return new RequestBodyModel(scalarType.TypeName, scalarType.IsObject, ReadBoolean(requestBody, "required"), schema);
    }

    private static List<ResponseModel> ReadResponses(JsonObject root, JsonNode? node, string context)
    {
        var result = new List<ResponseModel>();
        if (node is not JsonObject responses)
            throw new XpsOpenApiGenerationException($"{context} must be an object and contain at least one response.");
        foreach (var pair in responses)
        {
            if (!pair.Key.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                (!int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out var status) || status is < 100 or > 599))
                throw new XpsOpenApiGenerationException($"Response key '{pair.Key}' in {context} is not a valid HTTP status code or 'default'.");
            var response = XpsOpenApiSchema.Resolve(root, pair.Value, $"{context}.{pair.Key}");
            string? dataType = null;
            if (response["content"] is JsonObject content)
            {
                var jsonMedia = TrySelectJsonMediaType(content);
                if (jsonMedia?["schema"] is JsonObject schema)
                    dataType = DescribeSchemaType(root, schema, $"{context}.{pair.Key}");
            }
            result.Add(new ResponseModel(pair.Key, dataType));
        }
        if (result.Count == 0) throw new XpsOpenApiGenerationException($"{context} must contain at least one response.");
        return result;
    }

    private static string EmitSource(
        string version,
        string? sourceName,
        JsonObject root,
        Dictionary<string, JsonObject> models,
        IReadOnlyList<OperationModel> operations)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Option Declare");
        builder.AppendLine();
        builder.AppendLine($"' Generated by XPScript from OpenAPI {version}{(string.IsNullOrWhiteSpace(sourceName) ? string.Empty : " - " + sourceName)}");
        builder.AppendLine("' Handler functions are safe extension points. Regeneration overwrites this file.");
        builder.AppendLine();

        foreach (var model in models.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            EmitModel(builder, root, model.Key, model.Value);
            builder.AppendLine();
        }

        foreach (var operation in operations)
        {
            EmitOperation(builder, operation);
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void EmitModel(StringBuilder builder, JsonObject root, string name, JsonObject schema)
    {
        var resolved = XpsOpenApiSchema.Resolve(root, schema, $"schema '{name}'");
        builder.AppendLine($"Public Class {name}");
        if (resolved["properties"] is not JsonObject properties || properties.Count == 0)
        {
            builder.AppendLine("    Public Value As Variant");
            builder.AppendLine("End Class");
            return;
        }

        var required = ReadStringSet(resolved["required"]);
        foreach (var property in properties)
        {
            var fieldName = ValidateModelMemberName(property.Key, name);
            if (property.Value is not JsonObject propertySchema)
                throw new XpsOpenApiGenerationException($"Schema '{name}' property '{property.Key}' must be an object.");
            var fieldType = new XpsType(XpsOpenApiSchema.XpsType(root, propertySchema, $"schema '{name}' property '{property.Key}'"), XpsOpenApiSchema.IsObjectType(root, propertySchema, $"schema '{name}' property '{property.Key}'"));
            if (required.Contains(property.Key)) builder.AppendLine("    [Required]");
            var resolvedProperty = XpsOpenApiSchema.Resolve(root, propertySchema, $"schema '{name}' property '{property.Key}'");
            if (ReadString(resolvedProperty, "format")?.Equals("email", StringComparison.OrdinalIgnoreCase) == true)
                builder.AppendLine("    [Email]");
            if (TryReadInt(resolvedProperty, "maxLength", out var maxLength) && maxLength > 0)
                builder.AppendLine($"    [MaxLength:{maxLength.ToString(CultureInfo.InvariantCulture)}]");
            if (TryReadNumber(resolvedProperty, "minimum", out var minimum) && TryReadNumber(resolvedProperty, "maximum", out var maximum))
                builder.AppendLine($"    [Range:{minimum};{maximum}]");
            builder.AppendLine($"    Public {fieldName} As {fieldType.TypeName}");
            builder.AppendLine();
        }
        if (builder.Length >= Environment.NewLine.Length * 2)
            builder.Length -= Environment.NewLine.Length;
        builder.AppendLine("End Class");
    }

    private static void EmitOperation(StringBuilder builder, OperationModel operation)
    {
        var requestClass = operation.Name + "Request";
        var responseClass = operation.Name + "Response";
        var endpointName = "Endpoint" + operation.Name;
        var fields = BuildRequestFields(operation);

        builder.AppendLine($"Public Class {requestClass}");
        if (fields.Count == 0)
            builder.AppendLine("    Public HasInput As Boolean");
        else
            foreach (var field in fields) builder.AppendLine($"    Public {field.FieldName} As {field.TypeName}");
        builder.AppendLine("End Class");
        builder.AppendLine();

        builder.AppendLine($"Public Class {responseClass}");
        builder.AppendLine("    Public StatusCode As Integer");
        builder.AppendLine("    Public Data As Variant");
        builder.AppendLine("End Class");
        builder.AppendLine();

        builder.AppendLine($"' OpenAPI responses: {string.Join(", ", operation.Responses.Select(FormatResponseDescription))}");
        builder.AppendLine($"Function Handle{operation.Name}(request As {requestClass}) As {responseClass}");
        builder.AppendLine($"    Dim result As {responseClass}");
        builder.AppendLine($"    Set result = New {responseClass}");
        builder.AppendLine("    ' TODO: implement this operation and set result.StatusCode/result.Data.");
        builder.AppendLine("    result.StatusCode = 501");
        builder.AppendLine($"    Set Handle{operation.Name} = result");
        builder.AppendLine("End Function");
        builder.AppendLine();

        builder.AppendLine(operation.Authenticated ? "[Authenticated]" : "[Anonymous]");
        builder.AppendLine($"[{ToHttpAttribute(operation.Method)}]");
        builder.AppendLine($"[Route:{operation.Path}]");
        var parameters = BuildWrapperParameters(operation);
        builder.AppendLine($"Sub {endpointName}({string.Join(", ", parameters.Select(parameter => parameter.Declaration))})");
        builder.AppendLine($"    Dim request As {requestClass}");
        builder.AppendLine($"    Set request = New {requestClass}");
        builder.AppendLine($"    Dim result As {responseClass}");
        foreach (var parameter in parameters)
            builder.AppendLine($"    {(parameter.IsObject ? "Set " : string.Empty)}request.{parameter.FieldName} = {parameter.VariableName}");
        builder.AppendLine($"    Set result = Handle{operation.Name}(request)");
        builder.AppendLine($"    Call Write{operation.Name}Response(result)");
        builder.AppendLine("End Sub");
        builder.AppendLine();

        builder.AppendLine($"Sub Write{operation.Name}Response(result As {responseClass})");
        builder.AppendLine("    If result.StatusCode = 204 Then");
        builder.AppendLine("        Response.NoContent()");
        builder.AppendLine("    Else");
        builder.AppendLine("        Response.Json(result.StatusCode, result.Data)");
        builder.AppendLine("    End If");
        builder.AppendLine("End Sub");
    }

    private static List<RequestField> BuildRequestFields(OperationModel operation)
    {
        var fields = new List<RequestField>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in operation.Parameters)
        {
            var baseName = ToTypeIdentifier(parameter.Name, $"parameter '{parameter.Name}'");
            var fieldName = UniqueFieldName(baseName, parameter.Location, used);
            fields.Add(new RequestField(fieldName, parameter.TypeName, parameter.IsObject, parameter));
        }
        if (operation.Body is not null)
        {
            var fieldName = UniqueFieldName("Payload", "body", used);
            fields.Add(new RequestField(fieldName, operation.Body.TypeName, operation.Body.IsObject, operation.Body));
        }
        return fields;
    }

    private static List<WrapperParameter> BuildWrapperParameters(OperationModel operation)
    {
        var fields = BuildRequestFields(operation);
        var result = new List<WrapperParameter>();
        foreach (var field in fields)
        {
            if (field.Source is ParameterModel parameter)
            {
                var variableName = "p" + field.FieldName;
                var binding = parameter.Location switch
                {
                    "path" => "FromRoute",
                    "query" => "FromQuery",
                    "header" => "FromHeader",
                    "cookie" => "FromCookie",
                    _ => throw new InvalidOperationException("Unsupported parameter location.")
                };
                var escapedName = parameter.Name.Replace("\"", "\"\"", StringComparison.Ordinal);
                result.Add(new WrapperParameter(field.FieldName, variableName, field.TypeName, field.IsObject,
                    $"[{binding}:\"{escapedName}\"] {variableName} As {field.TypeName}"));
            }
            else
            {
                const string variableName = "payload";
                result.Add(new WrapperParameter(field.FieldName, variableName, field.TypeName, field.IsObject,
                    $"[FromBody] {variableName} As {field.TypeName}"));
            }
        }
        return result;
    }

    private static JsonObject SelectJsonMediaType(JsonObject content, string context) =>
        TrySelectJsonMediaType(content) ?? throw new XpsOpenApiGenerationException($"{context} currently requires application/json or a structured +json media type.");

    private static JsonObject? TrySelectJsonMediaType(JsonObject content)
    {
        if (content["application/json"] is JsonObject exact) return exact;
        foreach (var pair in content)
            if (pair.Key.EndsWith("+json", StringComparison.OrdinalIgnoreCase) && pair.Value is JsonObject media) return media;
        return null;
    }

    private static string DescribeSchemaType(JsonObject root, JsonObject schema, string context)
    {
        if (XpsOpenApiSchema.TryGetReference(schema, out var reference)) return XpsOpenApiSchema.ReferenceTypeName(reference, context);
        return XpsOpenApiSchema.XpsType(root, schema, context);
    }

    private static void EnsureUniqueParameters(IReadOnlyList<ParameterModel> parameters, string method, string path)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
            if (!seen.Add(parameter.Location + "\0" + parameter.Name))
                throw new XpsOpenApiGenerationException($"{method.ToUpperInvariant()} {path} declares duplicate {parameter.Location} parameter '{parameter.Name}'.");
    }

    private static HashSet<string> ReadStringSet(JsonNode? node)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (node is not JsonArray array) return result;
        foreach (var item in array)
            if (item is JsonValue value && value.TryGetValue<string>(out var text)) result.Add(text);
        return result;
    }

    private static string ValidateModelMemberName(string name, string modelName)
    {
        if (!IdentifierPattern.IsMatch(name) || IsDeclarationReserved(name))
            throw new XpsOpenApiGenerationException($"Schema '{modelName}' property '{name}' cannot be represented losslessly as an XPScript field name. Rename the OpenAPI property to a valid XPScript identifier in that declaration scope.");
        return name;
    }

    private static string ToTypeIdentifier(string value, string context)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new XpsOpenApiGenerationException($"{context} has an empty identifier.");
        var parts = Regex.Split(value.Trim(), "[^A-Za-z0-9_]+", RegexOptions.CultureInvariant).Where(part => part.Length > 0).ToArray();
        if (parts.Length == 0) throw new XpsOpenApiGenerationException($"{context} cannot be converted to an XPScript identifier.");
        var joined = string.Concat(parts.Select(Pascalize));
        if (joined.Length == 0 || char.IsDigit(joined[0])) joined = "Api" + joined;
        if (!IdentifierPattern.IsMatch(joined)) throw new XpsOpenApiGenerationException($"{context} cannot be converted to an XPScript identifier.");
        if (IsDeclarationReserved(joined)) joined = "Api" + joined;
        return joined;
    }

    private static bool IsDeclarationReserved(string identifier) => identifier.StartsWith("__", StringComparison.OrdinalIgnoreCase) || LexicalKeywords.Contains(identifier);

    private static string Pascalize(string value) => value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string BuildOperationName(string method, string path)
    {
        var suffix = Regex.Replace(path, "[{}]", string.Empty, RegexOptions.CultureInvariant);
        return method + " " + suffix;
    }

    private static string UniqueOperationName(string name, HashSet<string> used)
    {
        if (used.Add(name)) return name;
        for (var suffix = 2; suffix < 10_000; suffix++)
        {
            var candidate = name + suffix.ToString(CultureInfo.InvariantCulture);
            if (used.Add(candidate)) return candidate;
        }
        throw new XpsOpenApiGenerationException($"Unable to generate a unique operation name for '{name}'.");
    }

    private static string UniqueModelName(string baseName, IEnumerable<string> existing)
    {
        var used = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(baseName)) return baseName;
        for (var suffix = 2; suffix < 10_000; suffix++)
        {
            var candidate = baseName + suffix.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(candidate)) return candidate;
        }
        throw new XpsOpenApiGenerationException($"Unable to generate a unique model name for '{baseName}'.");
    }

    private static string UniqueFieldName(string baseName, string location, HashSet<string> used)
    {
        if (used.Add(baseName)) return baseName;
        var prefixed = ToTypeIdentifier(location, "parameter location") + baseName;
        if (used.Add(prefixed)) return prefixed;
        for (var suffix = 2; suffix < 10_000; suffix++)
        {
            var candidate = prefixed + suffix.ToString(CultureInfo.InvariantCulture);
            if (used.Add(candidate)) return candidate;
        }
        throw new XpsOpenApiGenerationException($"Unable to generate a unique request field name for '{baseName}'.");
    }

    private static string ToHttpAttribute(string method) => method.Length == 0 ? method : char.ToUpperInvariant(method[0]) + method[1..].ToLowerInvariant();

    private static string FormatResponseDescription(ResponseModel response) => response.DataType is null
        ? response.StatusCode
        : response.StatusCode + " " + response.DataType;

    private static string? ReadString(JsonObject node, string propertyName)
    {
        if (node[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)) return text;
        return null;
    }

    private static bool ReadBoolean(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<bool>(out var result) && result;
    }

    private static bool TryReadInt(JsonObject node, string propertyName, out int result)
    {
        result = 0;
        if (node[propertyName] is not JsonValue value) return false;
        if (value.TryGetValue<int>(out result)) return true;
        if (value.TryGetValue<long>(out var longValue) && longValue is >= int.MinValue and <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }
        return false;
    }

    private static bool TryReadNumber(JsonObject node, string propertyName, out string result)
    {
        result = string.Empty;
        if (node[propertyName] is not JsonValue value) return false;
        if (value.TryGetValue<long>(out var integer))
        {
            result = integer.ToString(CultureInfo.InvariantCulture);
            return true;
        }
        if (value.TryGetValue<double>(out var number))
        {
            result = number.ToString("R", CultureInfo.InvariantCulture);
            return true;
        }
        return false;
    }

    private static string FormatSourceName(string? sourceName) => string.IsNullOrWhiteSpace(sourceName) ? string.Empty : $" '{sourceName}'";

    private sealed record XpsType(string TypeName, bool IsObject);
    private sealed record ParameterModel(string Name, string Location, string TypeName, bool IsObject, bool Required);
    private sealed record RequestBodyModel(string TypeName, bool IsObject, bool Required, JsonObject Schema);
    private sealed record ResponseModel(string StatusCode, string? DataType);
    private sealed record OperationModel(
        string Method,
        string Path,
        string Name,
        IReadOnlyList<ParameterModel> Parameters,
        RequestBodyModel? Body,
        IReadOnlyList<ResponseModel> Responses,
        bool Authenticated);
    private sealed record RequestField(string FieldName, string TypeName, bool IsObject, object Source);
    private sealed record WrapperParameter(string FieldName, string VariableName, string TypeName, bool IsObject, string Declaration);
}
