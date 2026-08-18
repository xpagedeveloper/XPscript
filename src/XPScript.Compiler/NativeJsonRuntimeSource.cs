namespace XPScript.Compiler;

internal static class NativeJsonRuntimeSource
{
    public const string Code = """
internal static class XPScriptNativeJson
{
    private const int MaxParseBytes = 8 * 1024 * 1024;
    private const int MaxDepth = 64;
    private const int MaxNodes = 100_000;
    private const long MaxEstimatedPayloadBytes = 16L * 1024 * 1024;

    private static readonly System.Text.Json.JsonSerializerOptions SerializerOptions = new()
    {
        MaxDepth = MaxDepth
    };

    public static XPScriptJsonDocument CreateDocument() => new(new System.Text.Json.Nodes.JsonObject());
    public static XPScriptJsonObject CreateObject() => new(new System.Text.Json.Nodes.JsonObject());
    public static XPScriptJsonArray CreateArray() => new(new System.Text.Json.Nodes.JsonArray());
    public static XPScriptJsonElement CreateElement() => new(null);

    public static XPScriptJsonDocument Parse(object? value)
    {
        var text = XPScriptRuntime.CStr(value);
        if (Encoding.UTF8.GetByteCount(text) > MaxParseBytes)
            throw new XPScriptRuntimeException(5, "JSON input exceeds the 8 MiB parse limit.");

        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(
                text,
                nodeOptions: null,
                documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = MaxDepth });
            ValidateBudget(node);
            return new XPScriptJsonDocument(node);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new XPScriptRuntimeException(5, "Invalid JSON input.");
        }
    }

    public static string Stringify(object? value)
    {
        var node = ToNode(value);
        ValidateBudget(node);
        var text = node?.ToJsonString() ?? "null";
        if (Encoding.UTF8.GetByteCount(text) > MaxEstimatedPayloadBytes)
            throw new XPScriptRuntimeException(5, "JSON output exceeds the 16 MiB limit.");
        return text;
    }

    internal static System.Text.Json.Nodes.JsonNode? ToNode(object? value)
    {
        System.Text.Json.Nodes.JsonNode? node;
        try
        {
            node = value switch
            {
                null => null,
                _ when XPScriptNullRuntime.IsNull(value) => null,
                ILSObjectReference reference when reference.IsNothing => null,
                ILSObjectReference => throw new XPScriptRuntimeException(5, "Bound XPScript object references are not supported for JSON conversion."),
                XPScriptJsonDocument document => document.Node?.DeepClone(),
                XPScriptJsonObject obj => obj.Node.DeepClone(),
                XPScriptJsonArray array => array.Node.DeepClone(),
                XPScriptJsonElement element => element.Node?.DeepClone(),
                System.Text.Json.Nodes.JsonNode jsonNode => jsonNode.DeepClone(),
                string s => System.Text.Json.Nodes.JsonValue.Create(s),
                bool b => System.Text.Json.Nodes.JsonValue.Create(b),
                byte n => System.Text.Json.Nodes.JsonValue.Create(n),
                short n => System.Text.Json.Nodes.JsonValue.Create(n),
                int n => System.Text.Json.Nodes.JsonValue.Create(n),
                long n => System.Text.Json.Nodes.JsonValue.Create(n),
                float n => CreateFiniteNumber(n),
                double n => CreateFiniteNumber(n),
                decimal n => System.Text.Json.Nodes.JsonValue.Create(n),
                DateTime dt => System.Text.Json.Nodes.JsonValue.Create(dt),
                _ => System.Text.Json.JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions)
            };
        }
        catch (System.Text.Json.JsonException)
        {
            throw new XPScriptRuntimeException(5, "Value cannot be converted to JSON within the supported depth limit.");
        }
        catch (NotSupportedException)
        {
            throw new XPScriptRuntimeException(5, "Value type is not supported for JSON conversion.");
        }

        ValidateBudget(node);
        return node;
    }

    internal static object? FromNode(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null) return null;
        if (node is System.Text.Json.Nodes.JsonObject obj) return new XPScriptJsonObject(obj);
        if (node is System.Text.Json.Nodes.JsonArray array) return new XPScriptJsonArray(array);
        if (node is System.Text.Json.Nodes.JsonValue value)
        {
            if (value.TryGetValue<bool>(out var b)) return b;
            if (value.TryGetValue<byte>(out var u8)) return u8;
            if (value.TryGetValue<short>(out var i16)) return i16;
            if (value.TryGetValue<int>(out var i32)) return i32;
            if (value.TryGetValue<long>(out var i64)) return i64;
            if (value.TryGetValue<decimal>(out var dec)) return dec;
            if (value.TryGetValue<float>(out var f))
            {
                if (float.IsNaN(f) || float.IsInfinity(f))
                    throw new XPScriptRuntimeException(5, "JSON numeric value is outside the supported finite range.");
                return f;
            }
            if (value.TryGetValue<double>(out var d))
            {
                if (double.IsNaN(d) || double.IsInfinity(d))
                    throw new XPScriptRuntimeException(5, "JSON numeric value is outside the supported finite range.");
                return d;
            }
            if (value.TryGetValue<string>(out var s)) return s;
        }
        return new XPScriptJsonElement(node);
    }

    internal static void ValidateBudget(System.Text.Json.Nodes.JsonNode? node)
    {
        try
        {
            var nodes = 0;
            long payload = 0;
            Visit(node, 0, ref nodes, ref payload);
        }
        catch (OverflowException)
        {
            throw new XPScriptRuntimeException(5, "JSON value exceeds the supported resource budget.");
        }
    }

    private static System.Text.Json.Nodes.JsonNode CreateFiniteNumber(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new XPScriptRuntimeException(5, "JSON numeric values must be finite.");
        return System.Text.Json.Nodes.JsonValue.Create(value)!;
    }

    private static System.Text.Json.Nodes.JsonNode CreateFiniteNumber(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new XPScriptRuntimeException(5, "JSON numeric values must be finite.");
        return System.Text.Json.Nodes.JsonValue.Create(value)!;
    }

    private static void Visit(System.Text.Json.Nodes.JsonNode? node, int depth, ref int nodes, ref long payload)
    {
        if (node is null) return;
        if (depth > MaxDepth)
            throw new XPScriptRuntimeException(5, "JSON nesting exceeds the maximum depth of 64.");

        nodes = checked(nodes + 1);
        if (nodes > MaxNodes)
            throw new XPScriptRuntimeException(5, "JSON value exceeds the maximum node count of 100000.");

        switch (node)
        {
            case System.Text.Json.Nodes.JsonObject obj:
                foreach (var item in obj)
                {
                    payload = checked(payload + Encoding.UTF8.GetByteCount(item.Key));
                    EnsurePayload(payload);
                    Visit(item.Value, depth + 1, ref nodes, ref payload);
                }
                break;

            case System.Text.Json.Nodes.JsonArray array:
                foreach (var item in array)
                    Visit(item, depth + 1, ref nodes, ref payload);
                break;

            case System.Text.Json.Nodes.JsonValue value:
                if (value.TryGetValue<string>(out var text))
                    payload = checked(payload + Encoding.UTF8.GetByteCount(text));
                else if (value.TryGetValue<bool>(out _))
                    payload = checked(payload + 5);
                else
                    payload = checked(payload + 32);
                EnsurePayload(payload);
                break;
        }
    }

    private static void EnsurePayload(long payload)
    {
        if (payload > MaxEstimatedPayloadBytes)
            throw new XPScriptRuntimeException(5, "JSON value exceeds the 16 MiB estimated payload limit.");
    }
}

internal sealed class XPScriptJsonDocument
{
    internal XPScriptJsonDocument(System.Text.Json.Nodes.JsonNode? node)
    {
        XPScriptNativeJson.ValidateBudget(node);
        Node = node;
    }
    internal System.Text.Json.Nodes.JsonNode? Node { get; }
    public XPScriptJsonElement Root => new(Node);
    public string Stringify() => XPScriptNativeJson.Stringify(this);
}

internal sealed class XPScriptJsonObject
{
    internal XPScriptJsonObject(System.Text.Json.Nodes.JsonObject node)
    {
        XPScriptNativeJson.ValidateBudget(node);
        Node = node;
    }
    internal System.Text.Json.Nodes.JsonObject Node { get; }
    public int Count => Node.Count;
    public object? Get(object? name) => XPScriptNativeJson.FromNode(Node[XPScriptRuntime.CStr(name)]);

    public void Set(object? name, object? value)
    {
        var key = XPScriptRuntime.CStr(name);
        var hadPrevious = Node.TryGetPropertyValue(key, out var previous);
        var previousCopy = previous?.DeepClone();
        Node[key] = XPScriptNativeJson.ToNode(value);
        try
        {
            XPScriptNativeJson.ValidateBudget(Node);
        }
        catch
        {
            if (hadPrevious) Node[key] = previousCopy;
            else Node.Remove(key);
            throw;
        }
    }

    public void Remove(object? name) => Node.Remove(XPScriptRuntime.CStr(name));
    public bool Contains(object? name) => Node.ContainsKey(XPScriptRuntime.CStr(name));
    public string Stringify() => XPScriptNativeJson.Stringify(this);
}

internal sealed class XPScriptJsonArray
{
    internal XPScriptJsonArray(System.Text.Json.Nodes.JsonArray node)
    {
        XPScriptNativeJson.ValidateBudget(node);
        Node = node;
    }
    internal System.Text.Json.Nodes.JsonArray Node { get; }
    public int Count => Node.Count;

    public void Add(object? value)
    {
        Node.Add(XPScriptNativeJson.ToNode(value));
        try
        {
            XPScriptNativeJson.ValidateBudget(Node);
        }
        catch
        {
            Node.RemoveAt(Node.Count - 1);
            throw;
        }
    }

    public object? Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= Node.Count) throw new XPScriptRuntimeException(9, "JSON array index out of range.");
        return XPScriptNativeJson.FromNode(Node[index]);
    }

    public void Set(object? indexValue, object? value)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= Node.Count) throw new XPScriptRuntimeException(9, "JSON array index out of range.");
        var previous = Node[index]?.DeepClone();
        Node[index] = XPScriptNativeJson.ToNode(value);
        try
        {
            XPScriptNativeJson.ValidateBudget(Node);
        }
        catch
        {
            Node[index] = previous;
            throw;
        }
    }

    public void RemoveAt(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= Node.Count) throw new XPScriptRuntimeException(9, "JSON array index out of range.");
        Node.RemoveAt(index);
    }
    public string Stringify() => XPScriptNativeJson.Stringify(this);
}

internal sealed class XPScriptJsonElement
{
    internal XPScriptJsonElement(System.Text.Json.Nodes.JsonNode? node)
    {
        XPScriptNativeJson.ValidateBudget(node);
        Node = node;
    }
    internal System.Text.Json.Nodes.JsonNode? Node { get; }
    public string Type => Node switch
    {
        null => "Null",
        System.Text.Json.Nodes.JsonObject => "Object",
        System.Text.Json.Nodes.JsonArray => "Array",
        System.Text.Json.Nodes.JsonValue value when value.TryGetValue<bool>(out _) => "Boolean",
        System.Text.Json.Nodes.JsonValue value when value.TryGetValue<string>(out _) => "String",
        System.Text.Json.Nodes.JsonValue => "Number",
        _ => "Null"
    };
    public object? Value => XPScriptNativeJson.FromNode(Node);
    public XPScriptJsonObject? AsObject() => Node is System.Text.Json.Nodes.JsonObject obj ? new(obj) : null;
    public XPScriptJsonArray? AsArray() => Node is System.Text.Json.Nodes.JsonArray array ? new(array) : null;
}
""";
}
