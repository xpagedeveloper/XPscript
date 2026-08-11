namespace XPScript.Compiler;

internal static class NativeJsonRuntimeSource
{
    public const string Code = """
internal static class XPScriptNativeJson
{
    public static XPScriptJsonDocument CreateDocument() => new(new System.Text.Json.Nodes.JsonObject());
    public static XPScriptJsonObject CreateObject() => new(new System.Text.Json.Nodes.JsonObject());
    public static XPScriptJsonArray CreateArray() => new(new System.Text.Json.Nodes.JsonArray());
    public static XPScriptJsonElement CreateElement() => new(null);

    public static XPScriptJsonDocument Parse(object? value)
    {
        try { return new XPScriptJsonDocument(System.Text.Json.Nodes.JsonNode.Parse(XPScriptRuntime.CStr(value))); }
        catch (System.Text.Json.JsonException ex) { throw new XPScriptRuntimeException(5, "Invalid JSON: " + ex.Message); }
    }

    public static string Stringify(object? value) => ToNode(value)?.ToJsonString() ?? "null";

    internal static System.Text.Json.Nodes.JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        XPScriptJsonDocument document => document.Node?.DeepClone(),
        XPScriptJsonObject obj => obj.Node.DeepClone(),
        XPScriptJsonArray array => array.Node.DeepClone(),
        XPScriptJsonElement element => element.Node?.DeepClone(),
        System.Text.Json.Nodes.JsonNode node => node.DeepClone(),
        string s => System.Text.Json.Nodes.JsonValue.Create(s),
        bool b => System.Text.Json.Nodes.JsonValue.Create(b),
        byte n => System.Text.Json.Nodes.JsonValue.Create(n),
        short n => System.Text.Json.Nodes.JsonValue.Create(n),
        int n => System.Text.Json.Nodes.JsonValue.Create(n),
        long n => System.Text.Json.Nodes.JsonValue.Create(n),
        float n => System.Text.Json.Nodes.JsonValue.Create(n),
        double n => System.Text.Json.Nodes.JsonValue.Create(n),
        decimal n => System.Text.Json.Nodes.JsonValue.Create(n),
        DateTime dt => System.Text.Json.Nodes.JsonValue.Create(dt),
        _ => System.Text.Json.JsonSerializer.SerializeToNode(value)
    };

    internal static object? FromNode(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null) return null;
        if (node is System.Text.Json.Nodes.JsonObject obj) return new XPScriptJsonObject(obj);
        if (node is System.Text.Json.Nodes.JsonArray array) return new XPScriptJsonArray(array);
        if (node is System.Text.Json.Nodes.JsonValue value)
        {
            if (value.TryGetValue<bool>(out var b)) return b;
            if (value.TryGetValue<long>(out var l)) return l;
            if (value.TryGetValue<double>(out var d)) return d;
            if (value.TryGetValue<string>(out var s)) return s;
        }
        return new XPScriptJsonElement(node);
    }
}

internal sealed class XPScriptJsonDocument
{
    internal XPScriptJsonDocument(System.Text.Json.Nodes.JsonNode? node) => Node = node;
    internal System.Text.Json.Nodes.JsonNode? Node { get; }
    public XPScriptJsonElement Root => new(Node);
    public string Stringify() => XPScriptNativeJson.Stringify(this);
}

internal sealed class XPScriptJsonObject
{
    internal XPScriptJsonObject(System.Text.Json.Nodes.JsonObject node) => Node = node;
    internal System.Text.Json.Nodes.JsonObject Node { get; }
    public int Count => Node.Count;
    public object? Get(object? name) => XPScriptNativeJson.FromNode(Node[XPScriptRuntime.CStr(name)]);
    public void Set(object? name, object? value) => Node[XPScriptRuntime.CStr(name)] = XPScriptNativeJson.ToNode(value);
    public void Remove(object? name) => Node.Remove(XPScriptRuntime.CStr(name));
    public bool Contains(object? name) => Node.ContainsKey(XPScriptRuntime.CStr(name));
    public string Stringify() => Node.ToJsonString();
}

internal sealed class XPScriptJsonArray
{
    internal XPScriptJsonArray(System.Text.Json.Nodes.JsonArray node) => Node = node;
    internal System.Text.Json.Nodes.JsonArray Node { get; }
    public int Count => Node.Count;
    public void Add(object? value) => Node.Add(XPScriptNativeJson.ToNode(value));
    public object? Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= Node.Count) throw new IndexOutOfRangeException("JSON array index out of range.");
        return XPScriptNativeJson.FromNode(Node[index]);
    }
    public void Set(object? indexValue, object? value)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= Node.Count) throw new IndexOutOfRangeException("JSON array index out of range.");
        Node[index] = XPScriptNativeJson.ToNode(value);
    }
    public void RemoveAt(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= Node.Count) throw new IndexOutOfRangeException("JSON array index out of range.");
        Node.RemoveAt(index);
    }
    public string Stringify() => Node.ToJsonString();
}

internal sealed class XPScriptJsonElement
{
    internal XPScriptJsonElement(System.Text.Json.Nodes.JsonNode? node) => Node = node;
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
