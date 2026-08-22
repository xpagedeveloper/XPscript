namespace XPScript.Compiler;

internal static class AiToolRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptAiTool
{
    private const int MaxToolNameLength = 128;
    private readonly object _sync = new();
    private readonly List<XPScriptAiToolFunction> _functions = [];
    private readonly System.Text.Json.Nodes.JsonObject _requestContext = [];
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private string _description = string.Empty;

    public XPScriptAiTool(object? nameValue)
    {
        Name = NormalizeName(nameValue, "AITool name");
    }

    public string Name { get; }

    public string Description
    {
        get => _description;
        set
        {
            var description = value?.Trim() ?? string.Empty;
            if (description.Length > 4096 || description.IndexOf('\0') >= 0)
                throw new XPScriptRuntimeException(5, "AITool Description is invalid.");
            _description = description;
        }
    }

    public double Timeout
    {
        get => _timeout.TotalSeconds;
        set
        {
            if (value < 0.1 || value > 3600 || double.IsNaN(value) || double.IsInfinity(value))
                throw new XPScriptRuntimeException(5, "AITool Timeout must be between 0.1 and 3600 seconds.");
            _timeout = TimeSpan.FromSeconds(value);
        }
    }

    public XPScriptAiToolFunction AddFunction(object? nameValue, object? descriptionValue, object? handlerNameValue)
    {
        var function = new XPScriptAiToolFunction(Name, nameValue, descriptionValue, handlerNameValue);
        lock (_sync)
        {
            if (_functions.Any(existing => existing.Name.Equals(function.Name, StringComparison.OrdinalIgnoreCase)))
                throw new XPScriptRuntimeException(5, $"AITool function '{function.Name}' is already registered.");
            _functions.Add(function);
        }
        return function;
    }

    public void AddParameter(object? functionName, object? name, object? type, object? description, object? required)
        => GetFunction(functionName).AddParameter(name, type, description, required);

    public bool RemoveFunction(object? nameValue)
    {
        var name = NormalizeName(nameValue, "AITool function name");
        lock (_sync)
        {
            var index = _functions.FindIndex(function => function.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            _functions.RemoveAt(index);
            return true;
        }
    }

    public bool HasFunction(object? nameValue)
    {
        var name = NormalizeName(nameValue, "AITool function name");
        lock (_sync) return _functions.Any(function => function.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public XPScriptAiToolFunction GetFunction(object? nameValue)
    {
        var name = NormalizeName(nameValue, "AITool function name");
        lock (_sync)
        {
            var function = _functions.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (function is not null) return function;
        }
        throw new XPScriptRuntimeException(5, $"AITool function '{name}' is not registered.");
    }

    public XPScriptJsonArray GetFunctionNames()
    {
        var names = new System.Text.Json.Nodes.JsonArray();
        lock (_sync)
            foreach (var function in _functions)
                names.Add(function.Name);
        return new XPScriptJsonArray(names);
    }

    public int FunctionCount()
    {
        lock (_sync) return _functions.Count;
    }

    public void SetRequestProperty(object? nameValue, object? value)
    {
        var name = NormalizePropertyName(nameValue);
        lock (_sync) _requestContext[name] = XPScriptNativeJson.ToNode(value);
    }

    public void RemoveRequestProperty(object? nameValue)
    {
        var name = NormalizePropertyName(nameValue);
        lock (_sync) _requestContext.Remove(name);
    }

    public void ClearRequestProperties()
    {
        lock (_sync) _requestContext.Clear();
    }

    public XPScriptJsonObject GetRequestContext()
    {
        lock (_sync)
            return new XPScriptJsonObject((System.Text.Json.Nodes.JsonObject)_requestContext.DeepClone());
    }

    public XPScriptJsonObject ToJsonObject()
    {
        System.Text.Json.Nodes.JsonObject context;
        System.Text.Json.Nodes.JsonArray functions = [];
        lock (_sync)
        {
            context = (System.Text.Json.Nodes.JsonObject)_requestContext.DeepClone();
            foreach (var function in _functions)
                functions.Add(function.ToMetadataNode());
        }
        return new XPScriptJsonObject(new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = Name,
            ["description"] = Description,
            ["timeout"] = Timeout,
            ["requestContext"] = context,
            ["functions"] = functions
        });
    }

    public string ToJson() => ToJsonObject().Stringify();

    internal XPScriptAiToolFunction[] SnapshotFunctions()
    {
        lock (_sync) return _functions.ToArray();
    }

    internal static string NormalizeName(object? value, string label)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > MaxToolNameLength || name.IndexOfAny(['\r', '\n', '\0']) >= 0 ||
            !(char.IsLetter(name[0]) || name[0] == '_') || name.Skip(1).Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '-' or '.')))
            throw new XPScriptRuntimeException(5, label + " is invalid.");
        return name;
    }

    private static string NormalizePropertyName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 256 || name.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "AITool request property name is invalid.");
        return name;
    }
}

internal sealed class XPScriptAiToolFunction
{
    private const int MaxParameters = 128;
    private readonly object _sync = new();
    private readonly List<XPScriptAiToolParameter> _parameters = [];

    internal XPScriptAiToolFunction(object? toolNameValue, object? nameValue, object? descriptionValue, object? handlerNameValue)
    {
        ToolName = XPScriptAiTool.NormalizeName(toolNameValue, "AITool name");
        Name = XPScriptAiTool.NormalizeName(nameValue, "AITool function name");
        Description = XPScriptRuntime.CStr(descriptionValue).Trim();
        HandlerName = XPScriptRuntime.CStr(handlerNameValue).Trim();
        if (Description.Length == 0 || Description.Length > 8192 || Description.IndexOf('\0') >= 0)
            throw new XPScriptRuntimeException(5, "AITool function description is invalid.");
        if (HandlerName.Length is < 1 or > 256 || !(char.IsLetter(HandlerName[0]) || HandlerName[0] == '_') ||
            HandlerName.Skip(1).Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
            throw new XPScriptRuntimeException(5, "AITool function handler name is invalid.");
    }

    public string ToolName { get; }
    public string Name { get; }
    public string Description { get; }
    public string HandlerName { get; }

    public void AddParameter(object? nameValue, object? typeValue, object? descriptionValue, object? requiredValue)
    {
        var parameter = new XPScriptAiToolParameter(nameValue, typeValue, descriptionValue, requiredValue);
        lock (_sync)
        {
            if (_parameters.Count >= MaxParameters)
                throw new XPScriptRuntimeException(5, "AITool function parameter count exceeds the 128-parameter limit.");
            if (_parameters.Any(existing => existing.Name.Equals(parameter.Name, StringComparison.OrdinalIgnoreCase)))
                throw new XPScriptRuntimeException(5, $"AITool parameter '{parameter.Name}' is already registered.");
            _parameters.Add(parameter);
        }
    }

    public bool RemoveParameter(object? nameValue)
    {
        var name = XPScriptAiTool.NormalizeName(nameValue, "AITool parameter name");
        lock (_sync)
        {
            var index = _parameters.FindIndex(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            _parameters.RemoveAt(index);
            return true;
        }
    }

    public bool HasParameter(object? nameValue)
    {
        var name = XPScriptAiTool.NormalizeName(nameValue, "AITool parameter name");
        lock (_sync) return _parameters.Any(parameter => parameter.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public int ParameterCount()
    {
        lock (_sync) return _parameters.Count;
    }

    internal System.Text.Json.Nodes.JsonObject ToProviderNode()
    {
        System.Text.Json.Nodes.JsonObject properties = [];
        System.Text.Json.Nodes.JsonArray required = [];
        lock (_sync)
        {
            foreach (var parameter in _parameters)
            {
                properties[parameter.Name] = parameter.ToSchemaNode();
                if (parameter.Required) required.Add(parameter.Name);
            }
        }
        var parameters = new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };
        if (required.Count > 0) parameters["required"] = required;
        return new System.Text.Json.Nodes.JsonObject
        {
            ["type"] = "function",
            ["function"] = new System.Text.Json.Nodes.JsonObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["parameters"] = parameters
            }
        };
    }

    internal System.Text.Json.Nodes.JsonObject ToMetadataNode()
    {
        var provider = ToProviderNode();
        return new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = Name,
            ["description"] = Description,
            ["handler"] = HandlerName,
            ["parameters"] = provider["function"]?["parameters"]?.DeepClone()
        };
    }

    internal object? Invoke(object? callId, System.Text.Json.Nodes.JsonObject arguments, string sessionId)
    {
        ValidateArguments(arguments);
        var call = new XPScriptAiToolCall(ToolName, Name, callId, arguments, sessionId);
        return XPScriptCallbackRuntime.Invoke(HandlerName, "XPAi tool", [call]);
    }

    private void ValidateArguments(System.Text.Json.Nodes.JsonObject arguments)
    {
        XPScriptAiToolParameter[] parameters;
        lock (_sync) parameters = _parameters.ToArray();
        var known = parameters.Select(parameter => parameter.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var property in arguments)
            if (!known.Contains(property.Key))
                throw new XPScriptRuntimeException(5, $"XPAi tool argument '{property.Key}' is not declared for function '{Name}'.");
        foreach (var parameter in parameters)
        {
            if (!arguments.TryGetPropertyValue(parameter.Name, out var value) || value is null)
            {
                if (parameter.Required)
                    throw new XPScriptRuntimeException(5, $"XPAi tool function '{Name}' requires argument '{parameter.Name}'.");
                continue;
            }
            parameter.ValidateValue(value);
        }
    }
}

internal sealed class XPScriptAiToolParameter
{
    internal XPScriptAiToolParameter(object? nameValue, object? typeValue, object? descriptionValue, object? requiredValue)
    {
        Name = XPScriptAiTool.NormalizeName(nameValue, "AITool parameter name");
        Type = XPScriptRuntime.CStr(typeValue).Trim().ToLowerInvariant();
        if (Type is not ("string" or "integer" or "number" or "boolean" or "object" or "array"))
            throw new XPScriptRuntimeException(5, "AITool parameter type must be string, integer, number, boolean, object or array.");
        Description = XPScriptRuntime.CStr(descriptionValue).Trim();
        if (Description.Length > 4096 || Description.IndexOf('\0') >= 0)
            throw new XPScriptRuntimeException(5, "AITool parameter description is invalid.");
        Required = XPScriptRuntime.CBool(requiredValue);
    }

    public string Name { get; }
    public string Type { get; }
    public string Description { get; }
    public bool Required { get; }

    internal System.Text.Json.Nodes.JsonObject ToSchemaNode()
    {
        var node = new System.Text.Json.Nodes.JsonObject { ["type"] = Type };
        if (Description.Length > 0) node["description"] = Description;
        return node;
    }

    internal void ValidateValue(System.Text.Json.Nodes.JsonNode value)
    {
        var valid = Type switch
        {
            "string" => value is System.Text.Json.Nodes.JsonValue sv && sv.TryGetValue<string>(out _),
            "integer" => value is System.Text.Json.Nodes.JsonValue iv && (iv.TryGetValue<int>(out _) || iv.TryGetValue<long>(out _)),
            "number" => value is System.Text.Json.Nodes.JsonValue nv && (nv.TryGetValue<double>(out _) || nv.TryGetValue<decimal>(out _)),
            "boolean" => value is System.Text.Json.Nodes.JsonValue bv && bv.TryGetValue<bool>(out _),
            "object" => value is System.Text.Json.Nodes.JsonObject,
            "array" => value is System.Text.Json.Nodes.JsonArray,
            _ => false
        };
        if (!valid)
            throw new XPScriptRuntimeException(5, $"XPAi tool argument '{Name}' must be of type {Type}.");
    }
}

internal sealed class XPScriptAiToolCall
{
    internal XPScriptAiToolCall(string toolName, string functionName, object? callId, System.Text.Json.Nodes.JsonObject arguments, string sessionId)
    {
        ToolName = toolName;
        FunctionName = functionName;
        CallId = XPScriptRuntime.CStr(callId);
        Arguments = new XPScriptJsonObject((System.Text.Json.Nodes.JsonObject)arguments.DeepClone());
        SessionId = sessionId;
    }

    public string ToolName { get; }
    public string FunctionName { get; }
    public string CallId { get; }
    public XPScriptJsonObject Arguments { get; }
    public string SessionId { get; }
}

internal static class XPScriptAiToolRegistry
{
    private sealed class ToolCollection
    {
        public readonly object Sync = new();
        public readonly List<XPScriptAiTool> Tools = [];
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, ToolCollection> Collections = new();

    public static void AddTool(object? clientValue, object? toolValue)
    {
        var client = RequireClient(clientValue);
        if (toolValue is not XPScriptAiTool tool)
            throw new XPScriptRuntimeException(13, "XPAi.AddTool requires an AITool instance.");
        var collection = Collections.GetOrCreateValue(client);
        lock (collection.Sync)
        {
            if (collection.Tools.Any(existing => existing.Name.Equals(tool.Name, StringComparison.OrdinalIgnoreCase)))
                throw new XPScriptRuntimeException(5, $"XPAi tool '{tool.Name}' is already registered.");
            var existingFunctions = collection.Tools.SelectMany(existing => existing.SnapshotFunctions()).Select(function => function.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var duplicateFunction = tool.SnapshotFunctions().FirstOrDefault(function => existingFunctions.Contains(function.Name));
            if (duplicateFunction is not null)
                throw new XPScriptRuntimeException(5, $"XPAi tool function '{duplicateFunction.Name}' is already exposed by another tool.");
            collection.Tools.Add(tool);
        }
    }

    public static bool RemoveTool(object? clientValue, object? nameValue)
    {
        var client = RequireClient(clientValue);
        var name = NormalizeLookupName(nameValue);
        if (!Collections.TryGetValue(client, out var collection)) return false;
        lock (collection.Sync)
        {
            var index = collection.Tools.FindIndex(tool => tool.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            collection.Tools.RemoveAt(index);
            return true;
        }
    }

    public static void ClearTools(object? clientValue)
    {
        var client = RequireClient(clientValue);
        if (!Collections.TryGetValue(client, out var collection)) return;
        lock (collection.Sync) collection.Tools.Clear();
    }

    public static bool HasTool(object? clientValue, object? nameValue)
    {
        var client = RequireClient(clientValue);
        var name = NormalizeLookupName(nameValue);
        if (!Collections.TryGetValue(client, out var collection)) return false;
        lock (collection.Sync) return collection.Tools.Any(tool => tool.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public static int ToolCount(object? clientValue)
    {
        var client = RequireClient(clientValue);
        if (!Collections.TryGetValue(client, out var collection)) return 0;
        lock (collection.Sync) return collection.Tools.Count;
    }

    public static XPScriptAiTool GetTool(object? clientValue, object? nameValue)
    {
        var client = RequireClient(clientValue);
        var name = NormalizeLookupName(nameValue);
        if (Collections.TryGetValue(client, out var collection))
        {
            lock (collection.Sync)
            {
                var tool = collection.Tools.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (tool is not null) return tool;
            }
        }
        throw new XPScriptRuntimeException(5, $"XPAi tool '{name}' is not registered.");
    }

    public static XPScriptJsonArray GetToolNames(object? clientValue)
    {
        var client = RequireClient(clientValue);
        var names = new System.Text.Json.Nodes.JsonArray();
        if (Collections.TryGetValue(client, out var collection))
        {
            lock (collection.Sync)
                foreach (var tool in collection.Tools) names.Add(tool.Name);
        }
        return new XPScriptJsonArray(names);
    }

    internal static System.Text.Json.Nodes.JsonArray BuildProviderTools(object client)
    {
        var result = new System.Text.Json.Nodes.JsonArray();
        if (!Collections.TryGetValue(client, out var collection)) return result;
        lock (collection.Sync)
            foreach (var tool in collection.Tools)
                foreach (var function in tool.SnapshotFunctions())
                    result.Add(function.ToProviderNode());
        return result;
    }

    internal static object? InvokeFunction(object client, string functionName, string callId, System.Text.Json.Nodes.JsonObject arguments, string sessionId)
    {
        if (!Collections.TryGetValue(client, out var collection))
            throw new XPScriptRuntimeException(5, $"XPAi tool function '{functionName}' is not registered.");
        lock (collection.Sync)
        {
            foreach (var tool in collection.Tools)
            {
                var function = tool.SnapshotFunctions().FirstOrDefault(candidate => candidate.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));
                if (function is not null) return function.Invoke(callId, arguments, sessionId);
            }
        }
        throw new XPScriptRuntimeException(5, $"XPAi tool function '{functionName}' is not registered.");
    }

    internal static XPScriptAiTool[] SnapshotTools(object client)
    {
        if (!Collections.TryGetValue(client, out var collection)) return [];
        lock (collection.Sync) return collection.Tools.ToArray();
    }

    private static object RequireClient(object? clientValue)
    {
        if (clientValue is null || XPScriptNullRuntime.IsNull(clientValue))
            throw new XPScriptRuntimeException(91, "XPAi client is not initialized.");
        return clientValue;
    }

    private static string NormalizeLookupName(object? value)
        => XPScriptAiTool.NormalizeName(value, "AITool name");
}
""";
}
