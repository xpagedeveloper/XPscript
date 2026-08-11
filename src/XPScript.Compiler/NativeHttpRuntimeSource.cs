namespace XPScript.Compiler;

internal static class NativeHttpRuntimeSource
{
    public const string Code = """
internal static class XPScriptNativeHttp
{
    public static XPScriptHttpClient CreateClient() => new();
}

internal sealed class XPScriptHttpClient
{
    private readonly System.Net.Http.HttpClient _client = new();
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

    public double Timeout
    {
        get => _client.Timeout.TotalSeconds;
        set
        {
            if (value <= 0) throw new XPScriptRuntimeException(5, "HttpClient.Timeout must be greater than zero.");
            _client.Timeout = TimeSpan.FromSeconds(value);
        }
    }

    public void SetHeader(object? nameValue, object? value)
    {
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "HTTP header name cannot be empty.");
        _headers[name] = XPScriptRuntime.CStr(value);
    }

    public void RemoveHeader(object? nameValue) => _headers.Remove(XPScriptRuntime.CStr(nameValue).Trim());
    public void ClearHeaders() => _headers.Clear();
    public XPScriptHttpResponse Get(object? url) => Send(System.Net.Http.HttpMethod.Get, url, null);
    public XPScriptHttpResponse Delete(object? url) => Send(System.Net.Http.HttpMethod.Delete, url, null);
    public XPScriptHttpResponse Post(object? url, object? body) => Send(System.Net.Http.HttpMethod.Post, url, body);
    public XPScriptHttpResponse Put(object? url, object? body) => Send(System.Net.Http.HttpMethod.Put, url, body);
    public XPScriptHttpResponse Patch(object? url, object? body) => Send(System.Net.Http.HttpMethod.Patch, url, body);

    private XPScriptHttpResponse Send(System.Net.Http.HttpMethod method, object? urlValue, object? bodyValue)
    {
        var url = XPScriptRuntime.CStr(urlValue).Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new XPScriptRuntimeException(5, "Invalid HTTP URL: " + url);

        using var request = new System.Net.Http.HttpRequestMessage(method, uri);
        if (bodyValue is not null && method != System.Net.Http.HttpMethod.Get && method != System.Net.Http.HttpMethod.Delete)
        {
            request.Content = new System.Net.Http.StringContent(XPScriptRuntime.CStr(bodyValue), Encoding.UTF8);
            if (_headers.TryGetValue("Content-Type", out var ct) && !string.IsNullOrWhiteSpace(ct))
                request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ct);
        }

        foreach (var header in _headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content ??= new System.Net.Http.StringContent("");
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        try
        {
            using var response = _client.Send(request);
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers) headers[header.Key] = string.Join(", ", header.Value);
            foreach (var header in response.Content.Headers) headers[header.Key] = string.Join(", ", header.Value);
            return new XPScriptHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? "",
                Body = body,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "",
                Headers = headers,
                IsSuccess = response.IsSuccessStatusCode
            };
        }
        catch (TaskCanceledException ex)
        {
            throw new XPScriptRuntimeException(5, "HTTP request timed out: " + ex.Message);
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            throw new XPScriptRuntimeException(5, "HTTP request failed: " + ex.Message);
        }
    }
}

internal sealed class XPScriptHttpResponse
{
    public int StatusCode { get; init; }
    public string StatusText { get; init; } = "";
    public string Body { get; init; } = "";
    public string ContentType { get; init; } = "";
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsSuccess { get; init; }
}
""";
}
