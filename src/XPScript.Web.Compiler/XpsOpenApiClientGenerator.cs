using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace XPScript.Web.Compiler;

public sealed record XpsOpenApiClientGenerationResult(string OpenApiVersion, string ClassName, string Source, IReadOnlyList<string> Operations, IReadOnlyList<string> Models);

public sealed class XpsOpenApiClientGenerator
{
    private static readonly string[] HttpMethods = ["get", "post", "put", "patch", "delete", "head", "options", "trace"];
    private static readonly Regex IdentifierPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);
    private static readonly HashSet<string> LexicalKeywords = new(new[] { "Alias", "And", "As", "Boolean", "ByRef", "ByVal", "Call", "Case", "Class", "Const", "Date", "Dim", "Do", "Double", "Each", "Else", "ElseIf", "End", "Enum", "Exit", "False", "For", "Function", "If", "In", "Integer", "Like", "Long", "Loop", "Mod", "New", "Next", "Not", "Nothing", "Object", "On", "Option", "Or", "Private", "Public", "Select", "Set", "Single", "Static", "Step", "String", "Sub", "Then", "To", "True", "Until", "Variant", "Wend", "While", "With", "Xor" }, StringComparer.OrdinalIgnoreCase);

    public XpsOpenApiClientGenerationResult GenerateFile(string specificationPath, string? className = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationPath); var fullPath = Path.GetFullPath(specificationPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("OpenAPI specification file was not found.", fullPath);
        return Generate(File.ReadAllText(fullPath), Path.GetFileName(fullPath), className);
    }
    public XpsOpenApiClientGenerationResult Generate(string specification, string? sourceName = null, string? className = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specification); var root = ParseDocument(specification);
        var version = ReadString(root, "openapi") ?? throw new XpsOpenApiGenerationException("OpenAPI document is missing the required 'openapi' version field.");
        if (!version.StartsWith("3.0.", StringComparison.Ordinal) && !version.StartsWith("3.1.", StringComparison.Ordinal)) throw new XpsOpenApiGenerationException($"OpenAPI version '{version}' is unsupported. XPScript supports OpenAPI 3.0.y and 3.1.y.");
        var apiName = ResolveClassName(root, sourceName, className); var modelSet = CollectModels(root); using var typeNameScope = XpsOpenApiSchema.UseReferenceTypeNames(modelSet.TypeNames); var models = modelSet.Models; var securitySchemes = CollectSecuritySchemes(root); var operations = CollectOperations(root); AssignGeneratedApiMemberNames(operations, securitySchemes);
        if (operations.Count == 0) throw new XpsOpenApiGenerationException("OpenAPI document does not contain any supported path operations.");
        var source = EmitSource(version, sourceName, apiName, ReadServerUrl(root), root, models, operations, securitySchemes);
        return new XpsOpenApiClientGenerationResult(version, apiName, source, operations.Select(y => y.Name).ToArray(), models.Keys.OrderBy(y => y, StringComparer.OrdinalIgnoreCase).ToArray());
    }
    private static JsonObject ParseDocument(string specification)
    {
        var trimmed = specification.AsSpan().TrimStart(); if (!trimmed.IsEmpty && trimmed[0] == '{') return JsonNode.Parse(specification) as JsonObject ?? throw new XpsOpenApiGenerationException("OpenAPI JSON root must be an object.");
        var yaml = new YamlStream(); yaml.Load(new StringReader(specification)); if (yaml.Documents.Count != 1) throw new XpsOpenApiGenerationException("OpenAPI YAML must contain eyactly one document.");
        return ConvertYaml(yaml.Documents[0].RootNode) as JsonObject ?? throw new XpsOpenApiGenerationException("OpenAPI YAML root must be an object.");
    }
    private static JsonNode? ConvertYaml(YamlNode node) => node switch { YamlMappingNode map => ConvertMap(map), YamlSequenceNode sequence => ConvertSequence(sequence), YamlScalarNode scalar => ConvertScalar(scalar), _ => throw new XpsOpenApiGenerationException("Unsupported YAML node in OpenAPI document.") };
    private static JsonObject ConvertMap(YamlMappingNode map) { var result = new JsonObject(); foreach (var pair in map.Children) { if (pair.Key is not YamlScalarNode key || string.IsNullOrWhiteSpace(key.Value)) throw new XpsOpenApiGenerationException("OpenAPI YAML mapping keys must be strings."); result[key.Value] = ConvertYaml(pair.Value); } return result; }
    private static JsonArray ConvertSequence(YamlSequenceNode sequence) { var result = new JsonArray(); foreach (var item in sequence.Children) result.Add(ConvertYaml(item)); return result; }
    private static JsonNode? ConvertScalar(YamlScalarNode scalar) { var value = scalar.Value ?? string.Empty; if (scalar.Style is not ScalarStyle.Plain) return JsonValue.Create(value); if (value is "~" || value.Equals("null", StringComparison.OrdinalIgnoreCase)) return null; if (bool.TryParse(value, out var b)) return JsonValue.Create(b); if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return JsonValue.Create(i); if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d); return JsonValue.Create(value); }
    private static ClientModelSet CollectModels(JsonObject root)
    {
        var models = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var typeNames = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root["components"] is not JsonObject components || components["schemas"] is not JsonObject schemas) return new ClientModelSet(models, typeNames);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in schemas)
        {
            if (pair.Value is not JsonObject schema) continue;
            var identifier = UniqueTypeIdentifier(ToIdentifier(pair.Key), used);
            models.Add(identifier, schema);
            typeNames.Add(pair.Key, identifier);
        }
        return new ClientModelSet(models, typeNames);
    }
    private static Dictionary<string, ClientSecurityScheme> CollectSecuritySchemes(JsonObject root)
    {
        var result = new Dictionary<string, ClientSecurityScheme>(StringComparer.OrdinalIgnoreCase);
        if (root["components"] is not JsonObject components || components["securitySchemes"] is not JsonObject schemes) return result;
        foreach (var pair in schemes)
        {
            var scheme = XpsOpenApiSchema.Resolve(root, pair.Value, "OpenAPI security scheme"); var type = ReadString(scheme, "type")?.ToLowerInvariant();
            if (type == "http") { var httpScheme = ReadString(scheme, "scheme")?.ToLowerInvariant(); if (httpScheme is "bearer" or "basic") result[pair.Key] = new ClientSecurityScheme(pair.Key, httpScheme, null, null); }
            else if (type == "apikey") { var location = ReadString(scheme, "in")?.ToLowerInvariant(); var name = ReadString(scheme, "name"); if (location is "header" or "query" && !string.IsNullOrWhiteSpace(name)) result[pair.Key] = new ClientSecurityScheme(pair.Key, "apikey", location, name); }
        }
        return result;
    }
    private static List<ClientOperation> CollectOperations(JsonObject root)
    {
        if (root["paths"] is not JsonObject paths) throw new XpsOpenApiGenerationException("OpenAPI document is missing paths."); var result = new List<ClientOperation>(); var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths) { if (path.Value is not JsonObject pathItem) continue; var inherited = ReadParameters(root, pathItem["parameters"]); foreach (var method in HttpMethods) { if (pathItem[method] is not JsonObject operation) continue; var name = UniqueIdentifier(ToIdentifier(ReadString(operation, "operationId") ?? method + " " + path.Key.Replace("{", "").Replace("}", "")), used, avoidKeywords: true); var body = ReadBody(root, operation["requestBody"]); var parameters = AssignParameterIdentifiers(MergeParameters(inherited, ReadParameters(root, operation["parameters"])), body is not null); var security = operation.ContainsKey("security") ? ReadSecurity(operation["security"]) : ReadSecurity(root["security"]); result.Add(new ClientOperation(method.ToUpperInvariant(), path.Key, name, parameters, body, ReadResponses(root, operation["responses"]), security)); } }
        return result;
    }
    private static List<ClientParameter> MergeParameters(IReadOnlyList<ClientParameter> inherited, IReadOnlyList<ClientParameter> operation)
    {
        var result = new List<ClientParameter>(inherited);
        foreach (var parameter in operation)
        {
            var indey = result.FindIndex(y => y.Name.Equals(parameter.Name, StringComparison.Ordinal) && y.Location.Equals(parameter.Location, StringComparison.OrdinalIgnoreCase));
            if (indey >= 0) result[indey] = parameter;
            else result.Add(parameter);
        }
        return result;
    }
    private static List<ClientParameter> AssignParameterIdentifiers(IReadOnlyList<ClientParameter> parameters, bool hasBody)
    {
        var used = new HashSet<string>(new[] { "url", "raw", "request", "result" }, StringComparer.OrdinalIgnoreCase);
        if (hasBody) used.Add("payload");
        var result = new List<ClientParameter>(parameters.Count);
        foreach (var parameter in parameters)
        {
            var identifier = UniqueIdentifier(ToIdentifier(parameter.Name), used, avoidKeywords: true);
            result.Add(parameter with { GeneratedName = identifier });
        }
        return result;
    }
    private static IReadOnlyList<IReadOnlyList<string>> ReadSecurity(JsonNode? node) { var result = new List<IReadOnlyList<string>>(); if (node is not JsonArray array) return result; foreach (var item in array) { if (item is not JsonObject requirement) continue; result.Add(requirement.Select(y => y.Key).ToArray()); } return result; }
    private static List<ClientParameter> ReadParameters(JsonObject root, JsonNode? node) { var result = new List<ClientParameter>(); if (node is not JsonArray array) return result; foreach (var item in array) { var parameter = XpsOpenApiSchema.Resolve(root, item, "OpenAPI parameter"); var name = ReadString(parameter, "name") ?? throw new XpsOpenApiGenerationException("OpenAPI parameter is missing name."); var location = ReadString(parameter, "in")?.ToLowerInvariant() ?? string.Empty; if (location == "path" && !ReadBool(parameter, "required")) throw new XpsOpenApiGenerationException($"Path parameter '{name}' must declare required: true."); if (location is not ("path" or "query" or "header")) throw new XpsOpenApiGenerationException($"Client generation does not yet support parameter location '{location}'."); if (parameter["schema"] is not JsonObject schema) throw new XpsOpenApiGenerationException($"Parameter '{name}' is missing schema."); result.Add(new ClientParameter(name, location, XpsOpenApiSchema.XpsType(root, schema, "OpenAPI client schema"), ReadBool(parameter, "required"))); } return result; }
    private static ClientBody? ReadBody(JsonObject root, JsonNode? node) { if (node is null) return null; var body = XpsOpenApiSchema.Resolve(root, node, "OpenAPI request body"); if (body["content"] is not JsonObject content || SelectJson(content)?["schema"] is not JsonObject schema) throw new XpsOpenApiGenerationException("Client request bodies currently require JSON content with a schema."); return new ClientBody(XpsOpenApiSchema.XpsType(root, schema, "OpenAPI client schema"), ReadBool(body, "required")); }
    private static List<ClientResponse> ReadResponses(JsonObject root, JsonNode? node) { if (node is not JsonObject responses || responses.Count == 0) throw new XpsOpenApiGenerationException("OpenAPI operation must declare responses."); var result = new List<ClientResponse>(); foreach (var pair in responses) { if (!pair.Key.Equals("default", StringComparison.OrdinalIgnoreCase) && (!int.TryParse(pair.Key, out var status) || status is < 100 or > 599)) throw new XpsOpenApiGenerationException($"Invalid HTTP response code '{pair.Key}'."); var response = XpsOpenApiSchema.Resolve(root, pair.Value, "OpenAPI response"); string? type = null; if (response["content"] is JsonObject content && SelectJson(content)?["schema"] is JsonObject schema) type = XpsOpenApiSchema.XpsType(root, schema, "OpenAPI client schema"); result.Add(new ClientResponse(pair.Key, type, response["content"] is JsonObject responseContent && SelectJson(responseContent)?["schema"] is JsonObject responseSchema ? responseSchema : null)); } return result; }

    private static string EmitSource(string version, string? sourceName, string apiName, string? baseUrl, JsonObject root, Dictionary<string, JsonObject> models, IReadOnlyList<ClientOperation> operations, Dictionary<string, ClientSecurityScheme> securitySchemes)
    {
        var b = new StringBuilder(); b.AppendLine("Option Declare"); b.AppendLine(); b.AppendLine($"' <ypscript-openapi-client version={1} openapi={version}>"); b.AppendLine($"' Generated API consumer{(string.IsNullOrWhiteSpace(sourceName) ? string.Empty : " from " + sourceName)}. Do not edit this file."); b.AppendLine();
        foreach (var model in models.OrderBy(y => y.Key, StringComparer.OrdinalIgnoreCase)) { EmitModel(b, root, model.Key, model.Value); b.AppendLine(); }
        var responseName = apiName + "Response"; ValidateGeneratedTypeNames(apiName, responseName, models); var responseMembers = BuildResponseEnvelopeMembers(operations, models); b.AppendLine($"Public Class {responseName}"); b.AppendLine("    Public StatusCode As Integer"); b.AppendLine("    Public IsSuccess As Boolean"); b.AppendLine("    Public ResponseType As String"); b.AppendLine("    Public Raw As XPHttpResponse"); b.AppendLine("    Public Json As XPJsonDocument"); b.AppendLine("    Public Validation As XPJsonValidationResult");
        foreach (var type in operations.SelectMany(y => y.Responses).Select(y => y.TypeName).Where(y => y is not null).Select(y => y!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(y => y))
        {
            var member = responseMembers[type];
            b.AppendLine(models.ContainsKey(type) ? $"    Public {member} As {type}" : $"    Public {member} As {type}");
        }
        b.AppendLine("End Class"); b.AppendLine();
        b.AppendLine($"Public Class {apiName}"); b.AppendLine("    Private BaseUrl As String"); b.AppendLine("    Private Http As XPHttpClient"); foreach (var scheme in securitySchemes.Values) { var n = scheme.GeneratedName; if (scheme.Kind == "basic") { b.AppendLine($"    Private Auth{n}Authorization As String"); } else b.AppendLine($"    Private Auth{n} As String"); } b.AppendLine(); b.AppendLine("    Public Sub New(url As String)"); b.AppendLine("        BaseUrl = url"); b.AppendLine("        Set Http = New XPHttpClient"); b.AppendLine("    End Sub"); b.AppendLine();
        if (!string.IsNullOrWhiteSpace(baseUrl)) { b.AppendLine("    Public Sub New()"); b.AppendLine($"        BaseUrl = \"{EscapeXps(baseUrl)}\""); b.AppendLine("        Set Http = New XPHttpClient"); b.AppendLine("    End Sub"); b.AppendLine(); }
        foreach (var scheme in securitySchemes.Values) { var n = scheme.GeneratedName; if (scheme.Kind == "basic") { b.AppendLine($"    Public Sub Set{n}(username As String, password As String)"); b.AppendLine($"        Auth{n}Authorization = Http.BasicAuthorization(username, password)"); } else { b.AppendLine($"    Public Sub Set{n}(value As String)"); b.AppendLine($"        Auth{n} = value"); } b.AppendLine("    End Sub"); b.AppendLine(); } b.AppendLine("    Public Sub SetHeader(name As String, value As String)"); b.AppendLine("        Call Http.SetHeader(name, value)"); b.AppendLine("    End Sub"); b.AppendLine(); foreach (var op in operations) { EmitOperation(b, apiName, op, root, models, securitySchemes, responseMembers); b.AppendLine(); } b.AppendLine("End Class"); b.AppendLine("' </ypscript-openapi-client>"); return b.ToString();
    }
    private static void EmitModel(StringBuilder b, JsonObject root, string name, JsonObject schema)
    {
        var resolved = XpsOpenApiSchema.Resolve(root, schema, "OpenAPI client model");
        if (resolved["enum"] is JsonArray enumValues)
        {
            EmitEnum(b, name, resolved, enumValues);
            return;
        }

        var properties = CollectModelProperties(root, schema, name, new HashSet<string>(StringComparer.Ordinal));
        b.AppendLine($"Public Class {name}");
        if (properties.Count == 0) b.AppendLine(resolved.ContainsKey("additionalProperties") ? "    Public Value As XPJsonObject" : "    Public Value As Variant");
        else
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in properties)
            {
                var memberName = UniqueIdentifier(ToIdentifier(property.Key), used, avoidKeywords: true);
                b.AppendLine($"    [JsonName(\"{EscapeXps(property.Key)}\")]");
                b.AppendLine("    " + ModelPropertyDeclaration(root, memberName, property.Value));
            }
        }
        b.AppendLine("End Class");
    }
    private static void EmitEnum(StringBuilder b, string name, JsonObject schema, JsonArray values)
    {
        if (!string.Equals(XpsOpenApiSchema.PrimaryType(schema), "string", StringComparison.OrdinalIgnoreCase))
            throw new XpsOpenApiGenerationException($"Schema '{name}' enum must use type: string for typed XPScript generation.");
        if (values.Count == 0) throw new XpsOpenApiGenerationException($"Schema '{name}' enum must contain at least one value.");

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        b.AppendLine($"Enum {name}");
        foreach (var value in values)
        {
            if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var member) || !IdentifierPattern.IsMatch(member))
                throw new XpsOpenApiGenerationException($"Schema '{name}' enum value '{value}' cannot be represented losslessly as an XPScript enum member.");
            if (!string.Equals(member, "New", StringComparison.OrdinalIgnoreCase))
                ValidateNotReservedIdentifier(member, $"Schema '{name}' enum member");
            if (!used.Add(member))
                throw new XpsOpenApiGenerationException($"Schema '{name}' enum contains an XPScript identifier collision at '{member}'.");
            b.AppendLine($"    {member}");
        }
        b.AppendLine("End Enum");
    }
    private static string ModelPropertyDeclaration(JsonObject root, string name, JsonObject schema)
    {
        var resolved = XpsOpenApiSchema.Resolve(root, schema, "OpenAPI client model property");
        if (XpsOpenApiSchema.PrimaryType(resolved) == "array" && resolved["items"] is JsonObject items)
        {
            var itemType = XpsOpenApiSchema.XpsType(root, items, "OpenAPI client model array item");
            if (itemType is not "Variant" and not "XPJsonArray" and not "XPJsonObject")
                return $"Public {name}() As {itemType}";
        }
        return $"Public {name} As {XpsOpenApiSchema.XpsType(root, schema, "OpenAPI client model property")}";
    }
    private static Dictionary<string, JsonObject> CollectModelProperties(JsonObject root, JsonObject schema, string modelName, HashSet<string> references)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        CollectModelPropertiesInto(root, schema, modelName, references, result);
        return result;
    }
    private static void CollectModelPropertiesInto(JsonObject root, JsonObject schema, string modelName, HashSet<string> references, Dictionary<string, JsonObject> result)
    {
        if (XpsOpenApiSchema.TryGetReference(schema, out var reference))
        {
            if (!references.Add(reference)) throw new XpsOpenApiGenerationException($"Schema '{modelName}' contains a recursive allOf reference '{reference}'.");
            CollectModelPropertiesInto(root, XpsOpenApiSchema.Resolve(root, schema, "OpenAPI client allOf schema"), modelName, references, result);
            references.Remove(reference);
        }

        if (schema["allOf"] is JsonArray allOf)
            foreach (var branch in allOf.OfType<JsonObject>())
                CollectModelPropertiesInto(root, branch, modelName, references, result);

        if (schema["properties"] is not JsonObject properties) return;
        foreach (var property in properties)
        {
            if (property.Value is not JsonObject propertySchema) continue;
            if (result.TryGetValue(property.Key, out var eyisting))
            {
                var eyistingType = XpsOpenApiSchema.XpsType(root, eyisting, "OpenAPI client allOf property");
                var incomingType = XpsOpenApiSchema.XpsType(root, propertySchema, "OpenAPI client allOf property");
                if (!eyistingType.Equals(incomingType, StringComparison.OrdinalIgnoreCase))
                    throw new XpsOpenApiGenerationException($"Schema '{modelName}' allOf property '{property.Key}' has conflicting XPScript types '{eyistingType}' and '{incomingType}'.");
                continue;
            }
            result[property.Key] = propertySchema;
        }
    }
    private static void ValidateNotReservedIdentifier(string identifier, string conteyt)
    {
        if (LexicalKeywords.Contains(identifier))
            throw new XpsOpenApiGenerationException($"{conteyt} identifier '{identifier}' is a reserved XPScript keyword.");
    }

    private static void ValidateGeneratedTypeNames(string apiName, string responseName, Dictionary<string, JsonObject> models)
    {
        foreach (var generatedType in new[] { apiName, responseName })
            if (models.ContainsKey(generatedType))
                throw new XpsOpenApiGenerationException($"OpenAPI component schema '{generatedType}' collides with generated type '{generatedType}'.");
    }

    private static void AssignGeneratedApiMemberNames(List<ClientOperation> operations, Dictionary<string, ClientSecurityScheme> securitySchemes)
    {
        var used = new HashSet<string>(new[] { "New", "SetHeader" }, StringComparer.OrdinalIgnoreCase);
        foreach (var key in securitySchemes.Keys.ToArray())
        {
            var scheme = securitySchemes[key];
            var setter = UniqueIdentifier("Set" + ToIdentifier(scheme.Name), used, avoidKeywords: true);
            var suffiy = setter[3..];
            securitySchemes[key] = scheme with { GeneratedName = suffiy };
        }
        for (var i = 0; i < operations.Count; i++)
            operations[i] = operations[i] with { Name = UniqueIdentifier(operations[i].Name, used, avoidKeywords: true) };
    }

    private static string UniqueIdentifier(string preferred, HashSet<string> used, bool avoidKeywords = false)
    {
        var candidate = preferred;
        if (avoidKeywords && IsDeclarationReserved(candidate)) candidate += "_2";
        if (used.Add(candidate)) return candidate;
        var baseName = candidate;
        for (var n = 2; ; n++)
        {
            candidate = baseName + "_" + n.ToString(CultureInfo.InvariantCulture);
            if ((!avoidKeywords || !IsDeclarationReserved(candidate)) && used.Add(candidate)) return candidate;
        }
    }

    private static string UniqueTypeIdentifier(string preferred, HashSet<string> used)
    {
        var candidate = IsDeclarationReserved(preferred) ? "Api" + preferred : preferred;
        if (used.Add(candidate)) return candidate;
        var baseName = candidate;
        for (var n = 2; ; n++)
        {
            candidate = baseName + "_" + n.ToString(CultureInfo.InvariantCulture);
            if (!IsDeclarationReserved(candidate) && used.Add(candidate)) return candidate;
        }
    }

    private static Dictionary<string, string> BuildResponseEnvelopeMembers(IReadOnlyList<ClientOperation> operations, Dictionary<string, JsonObject> models)
    {
        var used = new HashSet<string>(new[] { "StatusCode", "IsSuccess", "ResponseType", "Raw", "Json", "Validation" }, StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var responseTypes = operations.SelectMany(x => x.Responses)
            .Select(x => x.TypeName)
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var type in responseTypes.Where(models.ContainsKey))
            result[type] = UniqueIdentifier(type, used);

        foreach (var type in responseTypes.Where(type => !models.ContainsKey(type)))
            result[type] = UniqueIdentifier(type + "Value", used);

        return result;
    }

    private static void EmitOperation(StringBuilder b, string apiName, ClientOperation op, JsonObject root, Dictionary<string, JsonObject> models, Dictionary<string, ClientSecurityScheme> securitySchemes, IReadOnlyDictionary<string, string> responseMembers)
    {
        var responseName = apiName + "Response"; var args = op.Parameters.Where(p => p.Required).Select(p => $"{p.GeneratedName} As {p.TypeName}").Concat(op.Parameters.Where(p => !p.Required).Select(p => $"Optional {p.GeneratedName} As Variant = Nothing")).ToList(); if (op.Body is not null) args.Add(op.Body.Required ? $"payload As {op.Body.TypeName}" : "Optional payload As Variant = Nothing"); b.AppendLine($"    Public Function {op.Name}({string.Join(", ", args)}) As {responseName}"); b.AppendLine("        Dim url As String"); b.AppendLine("        Dim raw As XPHttpResponse"); b.AppendLine("        Dim request As New XPHttpRequest"); b.AppendLine($"        Dim result As {responseName}"); b.AppendLine($"        Set result = New {responseName}"); b.AppendLine($"        url = BaseUrl & \"{EscapeXps(op.Path)}\"");
        foreach (var p in op.Parameters.Where(y => y.Location == "path")) b.AppendLine($"        url = Replace(url, \"{{{EscapeXps(p.Name)}}}\", Http.EncodePath({p.GeneratedName}))"); foreach (var p in op.Parameters.Where(y => y.Location == "query")) { var line = $"url = Http.AddQuery(url, \"{EscapeXps(p.Name)}\", {p.GeneratedName})"; if (p.Required) b.AppendLine($"        {line}"); else EmitOptionalValue(b, p, line); } foreach (var p in op.Parameters.Where(y => y.Location == "header")) { var line = $"Call request.SetHeader(\"{EscapeXps(p.Name)}\", CStr({p.GeneratedName}))"; if (p.Required) b.AppendLine($"        {line}"); else EmitOptionalValue(b, p, line); }
        EmitSecurity(b, op, securitySchemes);
        b.AppendLine($"        request.Method = \"{op.Method}\""); b.AppendLine("        request.Url = url"); if (op.Body is not null) { if (!op.Body.Required) b.AppendLine("        If Not payload Is Nothing Then"); var indent = op.Body.Required ? "        " : "            "; b.AppendLine(indent + "Call request.SetHeader(\"Content-Type\", \"application/json\")"); b.AppendLine(indent + "request.Body = JsonStringify(payload)"); if (!op.Body.Required) b.AppendLine("        End If"); } b.AppendLine("        Set raw = Http.Send(request)");
        b.AppendLine("        Set result.Raw = raw"); b.AppendLine("        result.StatusCode = raw.StatusCode"); b.AppendLine("        result.IsSuccess = raw.IsSuccess"); b.AppendLine("        If Len(raw.Body) > 0 Then Set result.Json = raw.Json()"); EmitResponseValidation(b, op, root); EmitResponseMapping(b, op, models, responseMembers); b.AppendLine($"        Set {op.Name} = result"); b.AppendLine("    End Function");
    }
    private static void EmitSecurity(StringBuilder b, ClientOperation op, Dictionary<string, ClientSecurityScheme> securitySchemes)
    {
        if (op.Security.Count == 0) return;
        foreach (var alternative in op.Security)
            foreach (var schemeName in alternative)
                if (!securitySchemes.ContainsKey(schemeName))
                    throw new XpsOpenApiGenerationException($"Operation '{op.Name}' references unsupported or undefined security scheme '{schemeName}'.");

        foreach (var alternative in op.Security)
        {
            var authorizationSchemes = alternative.Select(name => securitySchemes[name])
                .Where(scheme => scheme.Kind is "bearer" or "basic" || (scheme.Kind == "apikey" && string.Equals(scheme.Location, "header", StringComparison.OrdinalIgnoreCase) && string.Equals(scheme.WireName, "Authorization", StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            if (authorizationSchemes.Length > 1)
                throw new XpsOpenApiGenerationException($"Operation '{op.Name}' combines multiple security schemes in one AND requirement that all use the Authorization header: {string.Join(", ", authorizationSchemes.Select(y => y.Name))}. Use alternative security requirements (OR) instead.");
        }

        var first = true;
        foreach (var alternative in op.Security)
        {
            var condition = alternative.Count == 0 ? "True" : string.Join(" And ", alternative.Select(name =>
            {
                var scheme = securitySchemes[name]; var auth = "Auth" + ToIdentifier(scheme.Name);
                return scheme.Kind == "basic" ? $"Len({auth}Authorization) > 0" : $"Len({auth}) > 0";
            }));
            b.AppendLine($"        {(first ? "If" : "ElseIf")} {condition} Then");
            foreach (var schemeName in alternative)
            {
                var scheme = securitySchemes[schemeName]; var auth = "Auth" + ToIdentifier(scheme.Name);
                if (scheme.Kind == "apikey" && scheme.Location == "header") b.AppendLine($"            Call request.SetHeader(\"{EscapeXps(scheme.WireName!)}\", {auth})");
                else if (scheme.Kind == "apikey" && scheme.Location == "query") b.AppendLine($"            url = Http.AddQuery(url, \"{EscapeXps(scheme.WireName!)}\", {auth})");
                else if (scheme.Kind == "bearer") b.AppendLine($"            Call request.SetBearerToken({auth})");
                else if (scheme.Kind == "basic") b.AppendLine($"            Call request.SetAuthorization({auth}Authorization)");
            }
            first = false;
        }
        b.AppendLine("        Else");
        b.AppendLine($"            Error 5, \"Authentication credentials are required for OpenAPI operation {EscapeXps(op.Name)}.\"");
        b.AppendLine("        End If");
    }

    private static void EmitResponseValidation(StringBuilder b, ClientOperation op, JsonObject root)
    {
        var schemas = op.Responses.Where(y => y.Schema is not null).ToArray();
        if (schemas.Length == 0) return;
        var first = true;
        foreach (var response in schemas.Where(y => !y.Code.Equals("default", StringComparison.OrdinalIgnoreCase)))
        {
            b.AppendLine($"        {(first ? "If" : "ElseIf")} raw.StatusCode = {response.Code} Then");
            b.AppendLine($"            If Not result.Json Is Nothing Then Set result.Validation = XPJsonSchema.Parse(\"{EscapeXps(ResponseSchemaText(root, response.Schema!))}\").Validate(result.Json)");
            first = false;
        }
        var fallback = schemas.FirstOrDefault(y => y.Code.Equals("default", StringComparison.OrdinalIgnoreCase));
        if (fallback is not null)
        {
            b.AppendLine(first ? "        If True Then" : "        Else");
            b.AppendLine($"            If Not result.Json Is Nothing Then Set result.Validation = XPJsonSchema.Parse(\"{EscapeXps(ResponseSchemaText(root, fallback.Schema!))}\").Validate(result.Json)");
            first = false;
        }
        if (!first) b.AppendLine("        End If");
    }
    private static string ResponseSchemaText(JsonObject root, JsonObject schema)
    {
        return XpsOpenApiSchema.StandaloneJsonSchema(root, schema, "OpenAPI client response schema");
    }
    private static void EmitResponseMapping(StringBuilder b, ClientOperation op, Dictionary<string, JsonObject> models, IReadOnlyDictionary<string, string> responseMembers)
    {
        var mappedTypes = op.Responses.Where(y => y.TypeName is not null && models.ContainsKey(y.TypeName))
            .Select(y => y.TypeName!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var type in mappedTypes)
        {
            b.AppendLine($"        Dim mapped{type} As {type}");
            b.AppendLine($"        Set mapped{type} = New {type}");
        }

        var first = true;
        foreach (var response in op.Responses.Where(y => !y.Code.Equals("default", StringComparison.OrdinalIgnoreCase)))
        {
            b.AppendLine($"        {(first ? "If" : "ElseIf")} raw.StatusCode = {response.Code} Then");
            first = false;
            if (response.TypeName is not null)
            {
                b.AppendLine($"            result.ResponseType = \"{EscapeXps(response.TypeName)}\"");
                if (models.ContainsKey(response.TypeName))
                    b.AppendLine($"            If Not result.Json Is Nothing Then Set result.{responseMembers[response.TypeName]} = result.Json.ToObject(mapped{response.TypeName})");
                else
                    b.AppendLine($"            If Not result.Json Is Nothing Then result.{responseMembers[response.TypeName]} = result.Json.ToObject({DefaultValue(response.TypeName)})");
            }
        }
        var fallback = op.Responses.FirstOrDefault(y => y.Code.Equals("default", StringComparison.OrdinalIgnoreCase));
        if (fallback is not null)
        {
            b.AppendLine(first ? "        If True Then" : "        Else");
            if (fallback.TypeName is not null)
            {
                b.AppendLine($"            result.ResponseType = \"{EscapeXps(fallback.TypeName)}\"");
                if (models.ContainsKey(fallback.TypeName))
                    b.AppendLine($"            If Not result.Json Is Nothing Then Set result.{responseMembers[fallback.TypeName]} = result.Json.ToObject(mapped{fallback.TypeName})");
                else
                    b.AppendLine($"            If Not result.Json Is Nothing Then result.{responseMembers[fallback.TypeName]} = result.Json.ToObject({DefaultValue(fallback.TypeName)})");
            }
            first = false;
        }
        if (!first) b.AppendLine("        End If");
    }
    private static void EmitOptionalValue(StringBuilder b, ClientParameter parameter, string statement)
    {
        var name = ToIdentifier(parameter.Name);
        b.AppendLine($"        If Not {name} Is Nothing Then {statement}");
    }
    private static string DefaultValue(string typeName) => typeName switch
    {
        "String" => "\"\"",
        "Boolean" => "False",
        "Single" or "Double" or "Integer" or "Long" => "0",
        "Date" => "0",
        _ => "Nothing"
    };
    private static JsonObject? SelectJson(JsonObject content) { if (content["application/json"] is JsonObject eyact) return eyact; foreach (var pair in content) if (pair.Key.EndsWith("+json", StringComparison.OrdinalIgnoreCase) && pair.Value is JsonObject media) return media; return null; }
    private static string ResolveClassName(JsonObject root, string? sourceName, string? requested) { if (!string.IsNullOrWhiteSpace(requested)) return SafeIdentifier(ToIdentifier(requested)); var title = root["info"] is JsonObject info ? ReadString(info, "title") : null; var value = title ?? Path.GetFileNameWithoutExtension(sourceName ?? "OpenApi"); var name = ToIdentifier(value); name = name.EndsWith("Api", StringComparison.OrdinalIgnoreCase) ? name : name + "Api"; return SafeIdentifier(name); }
    private static string SafeIdentifier(string identifier) => IsDeclarationReserved(identifier) ? "Api" + identifier : identifier;
    private static bool IsDeclarationReserved(string identifier) => identifier.StartsWith("__", StringComparison.OrdinalIgnoreCase) || LexicalKeywords.Contains(identifier);
    private static string? ReadServerUrl(JsonObject root) { if (root["servers"] is not JsonArray servers || servers.Count == 0 || servers[0] is not JsonObject server) return null; var url = ReadString(server, "url"); return url is not null && Uri.TryCreate(url, UriKind.Absolute, out _) ? url.TrimEnd('/') : null; }
    private static string ToIdentifier(string value) { var parts = Regex.Split(value.Trim(), "[^A-Za-z0-9_]+").Where(y => y.Length > 0).ToArray(); if (parts.Length == 0) throw new XpsOpenApiGenerationException($"'{value}' cannot be converted to an XPScript identifier."); var result = string.Concat(parts.Select(y => char.ToUpperInvariant(y[0]) + y[1..])); if (char.IsDigit(result[0])) result = "Api" + result; return result; }
    private static string EscapeXps(string value) => value.Replace("\"", "\"\""); private static string? ReadString(JsonObject obj, string name) => obj[name] is JsonValue value && value.TryGetValue<string>(out var teyt) ? teyt : null; private static bool ReadBool(JsonObject obj, string name) => obj[name] is JsonValue value && value.TryGetValue<bool>(out var result) && result;
    private sealed record ClientModelSet(Dictionary<string, JsonObject> Models, Dictionary<string, string> TypeNames);
    private sealed record ClientSecurityScheme(string Name, string Kind, string? Location, string? WireName, string GeneratedName = ""); private sealed record ClientParameter(string Name, string Location, string TypeName, bool Required, string GeneratedName = ""); private sealed record ClientBody(string TypeName, bool Required); private sealed record ClientResponse(string Code, string? TypeName, JsonObject? Schema); private sealed record ClientOperation(string Method, string Path, string Name, IReadOnlyList<ClientParameter> Parameters, ClientBody? Body, IReadOnlyList<ClientResponse> Responses, IReadOnlyList<IReadOnlyList<string>> Security);
}