namespace XPScript.Compiler;

internal static class HttpJsonRuntimeSource
{
    public const string Code = """
internal static class XPScriptHttpJsonHelpers
{
    public static XPScriptJsonDocument GetJson(object? clientValue, object? url)
    {
        var response = Client(clientValue).Get(url);
        EnsureSuccess(response, "GET JSON");
        return XPScriptNativeJson.Parse(response.Body);
    }

    public static XPScriptHttpResponse PostJson(object? clientValue, object? url, object? data) => SendJson(Client(clientValue), "POST", url, data);
    public static XPScriptHttpResponse PutJson(object? clientValue, object? url, object? data) => SendJson(Client(clientValue), "PUT", url, data);
    public static XPScriptHttpResponse PatchJson(object? clientValue, object? url, object? data) => SendJson(Client(clientValue), "PATCH", url, data);

    public static XPScriptHttpResponse PostForm(object? clientValue, object? url, object? data)
    {
        var client = Client(clientValue);
        client.SetHeader("Content-Type", "application/x-www-form-urlencoded");
        return client.Post(url, EncodeForm(data));
    }

    private static XPScriptHttpResponse SendJson(XPScriptHttpClient client, string method, object? url, object? data)
    {
        client.SetHeader("Content-Type", "application/json; charset=utf-8");
        var body = XPScriptNativeJson.Stringify(data);
        return method switch
        {
            "POST" => client.Post(url, body),
            "PUT" => client.Put(url, body),
            "PATCH" => client.Patch(url, body),
            _ => throw new XPScriptRuntimeException(5, "Unsupported JSON HTTP method.")
        };
    }

    private static string EncodeForm(object? data)
    {
        var node = XPScriptNativeJson.ToNode(data);
        if (node is not System.Text.Json.Nodes.JsonObject obj)
            throw new XPScriptRuntimeException(13, "HttpClient.PostForm requires a JsonObject, JsonDocument object root or compatible object.");
        var values = new List<string>();
        foreach (var pair in obj)
        {
            if (pair.Value is System.Text.Json.Nodes.JsonObject or System.Text.Json.Nodes.JsonArray)
                throw new XPScriptRuntimeException(13, "HttpClient.PostForm only supports scalar form values.");
            var value = XPScriptNativeJson.FromNode(pair.Value);
            var text = value is null ? string.Empty : XPScriptRuntime.CStr(value);
            values.Add(Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(text));
        }
        return string.Join("&", values);
    }

    private static void EnsureSuccess(XPScriptHttpResponse response, string operation)
    {
        if (response.IsSuccess) return;
        throw new XPScriptRuntimeException(5, $"{operation} failed with HTTP status {response.StatusCode} {response.StatusText}.".TrimEnd());
    }

    private static XPScriptHttpClient Client(object? value)
        => value as XPScriptHttpClient ?? throw new XPScriptRuntimeException(13, "HTTP helper requires an HttpClient instance.");
}
""";
}
