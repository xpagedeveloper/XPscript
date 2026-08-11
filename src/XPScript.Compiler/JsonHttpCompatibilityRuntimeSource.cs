namespace XPScript.Compiler;

public static class JsonHttpCompatibilityRuntimeSource
{
    public const string Code = """
internal static class LSJsonHttpRuntime
{
    public static NotesHTTPRequest CreateHTTPRequest() => new();
    public static NotesJSONNavigator CreateJSONNavigator(object? input = null) => new(input);
    public static NotesJSONObject CreateJSONObject() => new(new System.Text.Json.Nodes.JsonObject());
    public static NotesJSONArray CreateJSONArray() => new(new System.Text.Json.Nodes.JsonArray());
    public static NotesJSONElement CreateJSONElement(object? value = null, object? name = null)
    {
        var node = LSJsonNodeRuntime.ToNode(value);
        return new NotesJSONElement(XPScriptRuntime.CStr(name), node, null);
    }
}

internal static class LSJsonNodeRuntime
{
    public static System.Text.Json.Nodes.JsonNode? ParseInput(object? input)
    {
        if (input is null) return new System.Text.Json.Nodes.JsonObject();
        if (input is NotesJSONNavigator navigator) return navigator.Root.DeepClone();
        if (input is NotesJSONObject obj) return obj.Node.DeepClone();
        if (input is NotesJSONArray arr) return arr.Node.DeepClone();
        if (input is NotesJSONElement element) return element.RawNode?.DeepClone();
        if (input is byte[] bytes) return ParseText(System.Text.Encoding.UTF8.GetString(bytes));

        var text = XPScriptRuntime.CStr(input);
        if (string.IsNullOrWhiteSpace(text)) return new System.Text.Json.Nodes.JsonObject();
        return ParseText(text);
    }

    private static System.Text.Json.Nodes.JsonNode ParseText(string text)
    {
        try
        {
            return System.Text.Json.Nodes.JsonNode.Parse(text)
                ?? throw new XPScriptRuntimeException(5, "JSON input is empty.");
        }
        catch (XPScriptRuntimeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Invalid JSON: " + ex.Message);
        }
    }

    public static System.Text.Json.Nodes.JsonNode? ToNode(object? value)
    {
        if (value is null) return null;
        if (value is NotesJSONElement element) return element.RawNode?.DeepClone();
        if (value is NotesJSONObject obj) return obj.Node.DeepClone();
        if (value is NotesJSONArray arr) return arr.Node.DeepClone();
        if (value is NotesJSONNavigator navigator) return navigator.Root.DeepClone();
        if (value is byte[] bytes) return System.Text.Json.Nodes.JsonValue.Create(System.Text.Encoding.UTF8.GetString(bytes));
        if (value is string s) return System.Text.Json.Nodes.JsonValue.Create(s);
        if (value is bool b) return System.Text.Json.Nodes.JsonValue.Create(b);
        if (value is byte by) return System.Text.Json.Nodes.JsonValue.Create((long)by);
        if (value is short sh) return System.Text.Json.Nodes.JsonValue.Create((long)sh);
        if (value is int i) return System.Text.Json.Nodes.JsonValue.Create((long)i);
        if (value is long l) return System.Text.Json.Nodes.JsonValue.Create(l);
        if (value is float f) return System.Text.Json.Nodes.JsonValue.Create((double)f);
        if (value is double d) return System.Text.Json.Nodes.JsonValue.Create(d);
        if (value is decimal dec) return System.Text.Json.Nodes.JsonValue.Create(dec);
        if (value is DateTime dt) return System.Text.Json.Nodes.JsonValue.Create(dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        return System.Text.Json.Nodes.JsonSerializer.SerializeToNode(value);
    }

    public static object? ToLotusValue(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null) return null;
        if (node is System.Text.Json.Nodes.JsonObject obj) return new NotesJSONObject(obj);
        if (node is System.Text.Json.Nodes.JsonArray arr) return new NotesJSONArray(arr);

        var element = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(node.ToJsonString());
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString() ?? "",
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            System.Text.Json.JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            System.Text.Json.JsonValueKind.Number => element.GetDouble(),
            System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined => null,
            _ => node.ToJsonString()
        };
    }

    public static int ElementType(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node is null) return 64;
        if (node is System.Text.Json.Nodes.JsonObject) return 1;
        if (node is System.Text.Json.Nodes.JsonArray) return 2;
        var element = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(node.ToJsonString());
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => 3,
            System.Text.Json.JsonValueKind.Number => 4,
            System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False => 5,
            System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined => 64,
            _ => 64
        };
    }

    public static NotesJSONElement ElementFromObject(System.Text.Json.Nodes.JsonObject obj, string name)
    {
        obj.TryGetPropertyValue(name, out var node);
        return new NotesJSONElement(name, node, replacement => obj[name] = replacement);
    }

    public static NotesJSONElement ElementFromArray(System.Text.Json.Nodes.JsonArray arr, int index)
    {
        var node = arr[index];
        return new NotesJSONElement("", node, replacement => arr[index] = replacement);
    }

    public static void ValidateOneBasedIndex(int index, int size, bool suppressErrors)
    {
        if (index >= 1 && index <= size) return;
        if (suppressErrors) return;
        throw new XPScriptRuntimeException(9, "Index is out of range.");
    }
}

internal sealed class NotesJSONElement
{
    private System.Text.Json.Nodes.JsonNode? _node;
    private readonly Action<System.Text.Json.Nodes.JsonNode?>? _setter;

    internal NotesJSONElement(string name, System.Text.Json.Nodes.JsonNode? node, Action<System.Text.Json.Nodes.JsonNode?>? setter)
    {
        Name = name;
        _node = node;
        _setter = setter;
    }

    public string Name { get; }
    public int Type => LSJsonNodeRuntime.ElementType(_node);

    public object? Value
    {
        get => LSJsonNodeRuntime.ToLotusValue(_node);
        set
        {
            _node = LSJsonNodeRuntime.ToNode(value);
            _setter?.Invoke(_node);
        }
    }

    internal System.Text.Json.Nodes.JsonNode? RawNode => _node;

    public void Copy(object? source)
    {
        if (source is not NotesJSONElement element)
            throw new XPScriptRuntimeException(13, "Copy requires NotesJSONElement.");
        _node = element.RawNode?.DeepClone();
        _setter?.Invoke(_node);
    }
}

internal sealed class NotesJSONObject
{
    private int _cursor = -1;
    internal NotesJSONObject(System.Text.Json.Nodes.JsonObject node) => Node = node;
    internal System.Text.Json.Nodes.JsonObject Node { get; }
    public int Size => Node.Count;

    public NotesJSONElement? GetElementByName(object? name)
    {
        var key = XPScriptRuntime.CStr(name);
        return Node.ContainsKey(key) ? LSJsonNodeRuntime.ElementFromObject(Node, key) : null;
    }

    public NotesJSONElement? GetFirstElement()
    {
        _cursor = 0;
        return ElementAt(_cursor, true);
    }

    public NotesJSONElement? GetNextElement()
    {
        _cursor++;
        return ElementAt(_cursor, true);
    }

    public NotesJSONElement? GetNthElement(object? index, object? suppressErrors = null)
    {
        var oneBased = XPScriptRuntime.CInt(index);
        var suppress = suppressErrors is not null && XPScriptRuntime.CBool(suppressErrors);
        LSJsonNodeRuntime.ValidateOneBasedIndex(oneBased, Size, suppress);
        if (oneBased < 1 || oneBased > Size) return null;
        _cursor = oneBased - 1;
        return ElementAt(_cursor, false);
    }

    public NotesJSONElement AppendElement(object? value, object? name)
    {
        var key = XPScriptRuntime.CStr(name);
        if (key.Length == 0) throw new XPScriptRuntimeException(5, "JSON object elements require a name.");
        Node[key] = LSJsonNodeRuntime.ToNode(value);
        return LSJsonNodeRuntime.ElementFromObject(Node, key);
    }

    public NotesJSONArray AppendArray(object? name)
    {
        var key = XPScriptRuntime.CStr(name);
        if (key.Length == 0) throw new XPScriptRuntimeException(5, "JSON object arrays require a name.");
        var arr = new System.Text.Json.Nodes.JsonArray();
        Node[key] = arr;
        return new NotesJSONArray(arr);
    }

    public NotesJSONObject AppendObject(object? name)
    {
        var key = XPScriptRuntime.CStr(name);
        if (key.Length == 0) throw new XPScriptRuntimeException(5, "JSON objects require a name.");
        var obj = new System.Text.Json.Nodes.JsonObject();
        Node[key] = obj;
        return new NotesJSONObject(obj);
    }

    public void Copy(object? source)
    {
        if (source is not NotesJSONObject other)
            throw new XPScriptRuntimeException(13, "Copy requires NotesJSONObject.");
        Node.Clear();
        foreach (var pair in other.Node)
            Node[pair.Key] = pair.Value?.DeepClone();
        _cursor = -1;
    }

    private NotesJSONElement? ElementAt(int zeroBased, bool suppress)
    {
        if (zeroBased < 0 || zeroBased >= Size)
        {
            if (suppress) return null;
            throw new XPScriptRuntimeException(9, "Index is out of range.");
        }
        var key = Node.ElementAt(zeroBased).Key;
        return LSJsonNodeRuntime.ElementFromObject(Node, key);
    }
}

internal sealed class NotesJSONArray
{
    private int _cursor = -1;
    internal NotesJSONArray(System.Text.Json.Nodes.JsonArray node) => Node = node;
    internal System.Text.Json.Nodes.JsonArray Node { get; }
    public int Size => Node.Count;

    public NotesJSONElement? GetFirstElement()
    {
        _cursor = 0;
        return ElementAt(_cursor, true);
    }

    public NotesJSONElement? GetNextElement()
    {
        _cursor++;
        return ElementAt(_cursor, true);
    }

    public NotesJSONElement? GetNthElement(object? index, object? suppressErrors = null)
    {
        var oneBased = XPScriptRuntime.CInt(index);
        var suppress = suppressErrors is not null && XPScriptRuntime.CBool(suppressErrors);
        LSJsonNodeRuntime.ValidateOneBasedIndex(oneBased, Size, suppress);
        if (oneBased < 1 || oneBased > Size) return null;
        _cursor = oneBased - 1;
        return ElementAt(_cursor, false);
    }

    public NotesJSONElement AppendElement(object? value)
    {
        Node.Add(LSJsonNodeRuntime.ToNode(value));
        return LSJsonNodeRuntime.ElementFromArray(Node, Node.Count - 1);
    }

    public NotesJSONArray AppendArray()
    {
        var arr = new System.Text.Json.Nodes.JsonArray();
        Node.Add(arr);
        return new NotesJSONArray(arr);
    }

    public NotesJSONObject AppendObject()
    {
        var obj = new System.Text.Json.Nodes.JsonObject();
        Node.Add(obj);
        return new NotesJSONObject(obj);
    }

    public void Copy(object? source)
    {
        if (source is not NotesJSONArray other)
            throw new XPScriptRuntimeException(13, "Copy requires NotesJSONArray.");
        Node.Clear();
        foreach (var item in other.Node)
            Node.Add(item?.DeepClone());
        _cursor = -1;
    }

    private NotesJSONElement? ElementAt(int zeroBased, bool suppress)
    {
        if (zeroBased < 0 || zeroBased >= Size)
        {
            if (suppress) return null;
            throw new XPScriptRuntimeException(9, "Index is out of range.");
        }
        return LSJsonNodeRuntime.ElementFromArray(Node, zeroBased);
    }
}

internal sealed class NotesJSONNavigator
{
    private int _cursor = -1;

    public NotesJSONNavigator(object? input = null)
    {
        Root = LSJsonNodeRuntime.ParseInput(input);
    }

    internal System.Text.Json.Nodes.JsonNode Root { get; private set; }
    public bool PreferJSONNavigator { get; set; } = true;
    public bool PreferUTF8 { get; set; } = true;

    public NotesJSONElement? GetElementByName(object? name)
    {
        if (Root is not System.Text.Json.Nodes.JsonObject obj) return null;
        var key = XPScriptRuntime.CStr(name);
        return obj.ContainsKey(key) ? LSJsonNodeRuntime.ElementFromObject(obj, key) : null;
    }

    public NotesJSONElement? GetElementByPointer(object? pointer)
    {
        var text = XPScriptRuntime.CStr(pointer);
        if (text.Length == 0) return new NotesJSONElement("", Root, replacement => Root = replacement ?? new System.Text.Json.Nodes.JsonObject());
        if (!text.StartsWith('/', StringComparison.Ordinal))
            throw new XPScriptRuntimeException(5, "JSON Pointer must start with '/'.");

        System.Text.Json.Nodes.JsonNode? current = Root;
        System.Text.Json.Nodes.JsonNode? parent = null;
        string? property = null;
        int? arrayIndex = null;

        foreach (var rawToken in text.Split('/').Skip(1))
        {
            var token = rawToken.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            parent = current;
            property = null;
            arrayIndex = null;

            if (current is System.Text.Json.Nodes.JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(token, out current)) return null;
                property = token;
            }
            else if (current is System.Text.Json.Nodes.JsonArray arr)
            {
                if (!int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index) || index < 0 || index >= arr.Count)
                    return null;
                current = arr[index];
                arrayIndex = index;
            }
            else
            {
                return null;
            }
        }

        if (parent is System.Text.Json.Nodes.JsonObject parentObj && property is not null)
            return new NotesJSONElement(property, current, replacement => parentObj[property] = replacement);
        if (parent is System.Text.Json.Nodes.JsonArray parentArr && arrayIndex.HasValue)
            return new NotesJSONElement("", current, replacement => parentArr[arrayIndex.Value] = replacement);
        return new NotesJSONElement("", current, null);
    }

    public NotesJSONElement? GetFirstElement()
    {
        _cursor = 0;
        return ElementAt(_cursor, true);
    }

    public NotesJSONElement? GetNextElement()
    {
        _cursor++;
        return ElementAt(_cursor, true);
    }

    public NotesJSONElement? GetNthElement(object? index, object? suppressErrors = null)
    {
        var oneBased = XPScriptRuntime.CInt(index);
        var suppress = suppressErrors is not null && XPScriptRuntime.CBool(suppressErrors);
        var size = Root switch
        {
            System.Text.Json.Nodes.JsonObject obj => obj.Count,
            System.Text.Json.Nodes.JsonArray arr => arr.Count,
            _ => 1
        };
        LSJsonNodeRuntime.ValidateOneBasedIndex(oneBased, size, suppress);
        if (oneBased < 1 || oneBased > size) return null;
        _cursor = oneBased - 1;
        return ElementAt(_cursor, false);
    }

    public string Stringify() => Root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = false });

    public NotesJSONElement AppendElement(object? value, object? name = null)
    {
        if (Root is System.Text.Json.Nodes.JsonObject obj)
        {
            var key = XPScriptRuntime.CStr(name);
            if (key.Length == 0) throw new XPScriptRuntimeException(5, "JSON object elements require a name.");
            obj[key] = LSJsonNodeRuntime.ToNode(value);
            return LSJsonNodeRuntime.ElementFromObject(obj, key);
        }
        if (Root is System.Text.Json.Nodes.JsonArray arr)
        {
            arr.Add(LSJsonNodeRuntime.ToNode(value));
            return LSJsonNodeRuntime.ElementFromArray(arr, arr.Count - 1);
        }
        throw new XPScriptRuntimeException(5, "Cannot append to a scalar JSON root.");
    }

    public NotesJSONArray AppendArray(object? name = null)
    {
        var child = new System.Text.Json.Nodes.JsonArray();
        if (Root is System.Text.Json.Nodes.JsonObject obj)
        {
            var key = XPScriptRuntime.CStr(name);
            if (key.Length == 0) throw new XPScriptRuntimeException(5, "JSON object arrays require a name.");
            obj[key] = child;
        }
        else if (Root is System.Text.Json.Nodes.JsonArray arr)
        {
            arr.Add(child);
        }
        else throw new XPScriptRuntimeException(5, "Cannot append to a scalar JSON root.");
        return new NotesJSONArray(child);
    }

    public NotesJSONObject AppendObject(object? name = null)
    {
        var child = new System.Text.Json.Nodes.JsonObject();
        if (Root is System.Text.Json.Nodes.JsonObject obj)
        {
            var key = XPScriptRuntime.CStr(name);
            if (key.Length == 0) throw new XPScriptRuntimeException(5, "JSON objects require a name.");
            obj[key] = child;
        }
        else if (Root is System.Text.Json.Nodes.JsonArray arr)
        {
            arr.Add(child);
        }
        else throw new XPScriptRuntimeException(5, "Cannot append to a scalar JSON root.");
        return new NotesJSONObject(child);
    }

    private NotesJSONElement? ElementAt(int zeroBased, bool suppress)
    {
        if (Root is System.Text.Json.Nodes.JsonObject obj)
        {
            if (zeroBased < 0 || zeroBased >= obj.Count)
            {
                if (suppress) return null;
                throw new XPScriptRuntimeException(9, "Index is out of range.");
            }
            var key = obj.ElementAt(zeroBased).Key;
            return LSJsonNodeRuntime.ElementFromObject(obj, key);
        }
        if (Root is System.Text.Json.Nodes.JsonArray arr)
        {
            if (zeroBased < 0 || zeroBased >= arr.Count)
            {
                if (suppress) return null;
                throw new XPScriptRuntimeException(9, "Index is out of range.");
            }
            return LSJsonNodeRuntime.ElementFromArray(arr, zeroBased);
        }
        if (zeroBased == 0) return new NotesJSONElement("", Root, replacement => Root = replacement ?? new System.Text.Json.Nodes.JsonObject());
        return null;
    }
}

internal sealed class NotesHTTPRequest
{
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private string? _proxyHost;
    private int _proxyPort;
    private string? _proxyUser;
    private string? _proxyPassword;
    private string[] _responseHeaders = [];

    public NotesHTTPRequest() => ResetHeaders();

    public int ResponseCode { get; private set; }
    public int TimeoutSec { get; set; } = 30;
    public int MaxRedirects { get; set; } = 0;
    public bool PreferStrings { get; set; }
    public bool PreferJSONNavigator { get; set; }
    public bool PreferUTF8 => !PreferStrings && !PreferJSONNavigator;

    public void SetHeaderField(object? name, object? value)
    {
        var key = XPScriptRuntime.CStr(name).Trim();
        if (key.Length == 0) throw new XPScriptRuntimeException(5, "HTTP header name cannot be empty.");
        _headers[key] = XPScriptRuntime.CStr(value);
    }

    public void ResetHeaders()
    {
        _headers.Clear();
        _headers["Accept"] = "application/json";
        _headers["Content-Type"] = "application/json";
        _headers["charsets"] = "utf-8";
    }

    public string[] GetResponseHeaders() => [.. _responseHeaders];

    public void SetProxy(object? proxyUrl, object? proxyPort)
    {
        var host = XPScriptRuntime.CStr(proxyUrl).Trim();
        var port = XPScriptRuntime.CInt(proxyPort);
        if (port < 0 || port > 65535) throw new XPScriptRuntimeException(5, "Illegal Proxy Port Value.");
        if (host.Length == 0) throw new XPScriptRuntimeException(5, "Proxy URL cannot be empty.");
        _proxyHost = host;
        _proxyPort = port;
    }

    public void SetProxyUser(object? userName, object? password)
    {
        _proxyUser = XPScriptRuntime.CStr(userName);
        _proxyPassword = XPScriptRuntime.CStr(password);
    }

    public void ResetProxy()
    {
        _proxyHost = null;
        _proxyPort = 0;
        _proxyUser = null;
        _proxyPassword = null;
    }

    public object? Get(object? url) => Send(System.Net.Http.HttpMethod.Get, url, null);
    public object? Post(object? url, object? data) => Send(System.Net.Http.HttpMethod.Post, url, data);
    public object? Put(object? url, object? data) => Send(System.Net.Http.HttpMethod.Put, url, data);
    public object? Patch(object? url, object? data) => Send(System.Net.Http.HttpMethod.Patch, url, data);
    public object? DeleteResource(object? url) => Send(System.Net.Http.HttpMethod.Delete, url, null);

    private object? Send(System.Net.Http.HttpMethod method, object? rawUrl, object? data)
    {
        var url = XPScriptRuntime.CStr(rawUrl).Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new XPScriptRuntimeException(5, "Invalid HTTP URL: " + url);

        try
        {
            using var handler = BuildHandler();
            using var client = new System.Net.Http.HttpClient(handler)
            {
                Timeout = TimeoutSec <= 0 ? System.Threading.Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(TimeoutSec)
            };
            using var request = new System.Net.Http.HttpRequestMessage(method, uri);

            if (data is not null)
            {
                var body = data switch
                {
                    NotesJSONNavigator nav => nav.Stringify(),
                    NotesJSONObject obj => obj.Node.ToJsonString(),
                    NotesJSONArray arr => arr.Node.ToJsonString(),
                    NotesJSONElement element => element.RawNode?.ToJsonString() ?? "null",
                    byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                    _ => XPScriptRuntime.CStr(data)
                };
                request.Content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8);
            }

            foreach (var pair in _headers)
            {
                if (pair.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    if (request.Content is not null)
                    {
                        request.Content.Headers.Remove("Content-Type");
                        request.Content.Headers.TryAddWithoutValidation("Content-Type", pair.Value);
                    }
                    continue;
                }
                if (pair.Key.Equals("charsets", StringComparison.OrdinalIgnoreCase)) continue;
                request.Headers.Remove(pair.Key);
                request.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
            }

            using var response = client.SendAsync(request).GetAwaiter().GetResult();
            ResponseCode = (int)response.StatusCode;
            _responseHeaders = response.Headers
                .Concat(response.Content.Headers)
                .Select(x => x.Key + ": " + string.Join(", ", x.Value))
                .ToArray();
            var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();

            if (PreferJSONNavigator)
                return new NotesJSONNavigator(bytes);
            if (PreferStrings)
                return System.Text.Encoding.UTF8.GetString(bytes);
            return bytes;
        }
        catch (XPScriptRuntimeException)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            throw new XPScriptRuntimeException(5, "HTTP request timed out: " + ex.Message);
        }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "HTTP request failed: " + ex.Message);
        }
    }

    private System.Net.Http.HttpClientHandler BuildHandler()
    {
        var handler = new System.Net.Http.HttpClientHandler
        {
            AllowAutoRedirect = MaxRedirects > 0,
            MaxAutomaticRedirections = Math.Max(1, MaxRedirects)
        };
        if (_proxyHost is null) return handler;

        var proxyText = _proxyHost.Contains("://", StringComparison.Ordinal) ? _proxyHost : "http://" + _proxyHost;
        if (_proxyPort > 0)
        {
            var builder = new UriBuilder(proxyText) { Port = _proxyPort };
            proxyText = builder.Uri.ToString();
        }
        var proxy = new System.Net.WebProxy(proxyText);
        if (_proxyUser is not null)
            proxy.Credentials = new System.Net.NetworkCredential(_proxyUser, _proxyPassword ?? "");
        handler.UseProxy = true;
        handler.Proxy = proxy;
        return handler;
    }
}
""";
}
