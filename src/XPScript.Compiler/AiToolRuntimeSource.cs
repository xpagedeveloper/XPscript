namespace XPScript.Compiler;

internal static class AiToolRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptAiTool
{
    private const int MaxToolNameLength = 128;
    private readonly object _sync = new();
    private readonly System.Text.Json.Nodes.JsonObject _requestContext = [];
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);

    public XPScriptAiTool(object? nameValue)
    {
        Name = NormalizeName(nameValue);
    }

    public string Name { get; }

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
        lock (_sync) context = (System.Text.Json.Nodes.JsonObject)_requestContext.DeepClone();
        return new XPScriptJsonObject(new System.Text.Json.Nodes.JsonObject
        {
            ["name"] = Name,
            ["timeout"] = Timeout,
            ["requestContext"] = context
        });
    }

    public string ToJson() => ToJsonObject().Stringify();

    private static string NormalizeName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > MaxToolNameLength || name.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "AITool name is invalid.");
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
        lock (collection.Sync)
            return collection.Tools.Any(tool => tool.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
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
                foreach (var tool in collection.Tools)
                    names.Add(tool.Name);
        }
        return new XPScriptJsonArray(names);
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
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 128 || name.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "AITool name is invalid.");
        return name;
    }
}
""";
}
