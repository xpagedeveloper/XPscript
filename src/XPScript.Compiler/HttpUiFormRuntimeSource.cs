namespace XPScript.Compiler;

internal static class HttpUiFormRuntimeSource
{
    public const string Code = """
internal static class XPScriptHttpUiFormHelpers
{
    public static XPScriptJsonDocument GetJson(object? clientValue, object? url)
    {
        var response = Client(clientValue).Get(url);
        EnsureSuccess(response, "GET JSON");
        return XPScriptNativeJson.Parse(response.Body);
    }

    public static XPScriptHttpResponse PostJson(object? clientValue, object? url, object? data)
        => SendJson(Client(clientValue), "POST", url, data);

    public static XPScriptHttpResponse PutJson(object? clientValue, object? url, object? data)
        => SendJson(Client(clientValue), "PUT", url, data);

    public static XPScriptHttpResponse PatchJson(object? clientValue, object? url, object? data)
        => SendJson(Client(clientValue), "PATCH", url, data);

    public static XPScriptHttpResponse PostForm(object? clientValue, object? url, object? data)
    {
        var client = Client(clientValue);
        client.SetHeader("Content-Type", "application/x-www-form-urlencoded");
        return client.Post(url, EncodeForm(data));
    }

    public static string AddQuery(object? clientValue, object? urlValue, object? nameValue, object? value)
    {
        _ = Client(clientValue);
        var url = XPScriptRuntime.CStr(urlValue).Trim();
        var name = XPScriptRuntime.CStr(nameValue);
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "HTTP query parameter name cannot be empty.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new XPScriptRuntimeException(5, "HTTP URL must be an absolute http:// or https:// URL.");

        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return url + separator + Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(XPScriptRuntime.CStr(value));
    }

    public static void LoadForm(object? clientValue, object? formValue, object? url)
    {
        var form = Form(formValue);
        var document = GetJson(clientValue, url);
        if (document.Root.AsObject() is null)
            throw new XPScriptRuntimeException(13, "UIForm.LoadForm requires a JSON object response.");
        form.BindData(document);
    }

    public static XPScriptHttpResponse SaveForm(object? clientValue, object? formValue, object? url)
    {
        var form = Form(formValue);
        var response = PostJson(clientValue, url, form.Data);
        if (response.IsSuccess) form.MarkClean();
        return response;
    }

    public static XPScriptHttpResponse PutForm(object? clientValue, object? formValue, object? url)
    {
        var form = Form(formValue);
        var response = PutJson(clientValue, url, form.Data);
        if (response.IsSuccess) form.MarkClean();
        return response;
    }

    public static XPScriptJsonDocument ResponseJson(object? responseValue)
    {
        var response = Response(responseValue);
        return XPScriptNativeJson.Parse(response.Body);
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

    private static XPScriptHttpResponse Response(object? value)
        => value as XPScriptHttpResponse ?? throw new XPScriptRuntimeException(13, "JSON response helper requires an HttpResponse instance.");

    private static XPScriptUIForm Form(object? value)
        => value as XPScriptUIForm ?? throw new XPScriptRuntimeException(13, "UIForm HTTP helper requires a UIForm instance.");
}
""";
}
