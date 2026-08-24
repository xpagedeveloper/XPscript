namespace XPScript.Web.Compiler;

internal static class BrowserWasmServerBridgeTransportInstaller
{
    private const string UrlMarker = "        var url = XPScriptRuntime.CStr(urlValue).Trim();";
    private const string InstalledMarker = "XPScriptBrowserServerBridgeTransport.Send(method, url, bodyValue, _headers)";

    public static string TransformGenerated(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(InstalledMarker, StringComparison.Ordinal)) return generated;
        if (!generated.Contains(UrlMarker, StringComparison.Ordinal))
            throw new XpsWebCompilationException("Unable to install browser-wasm server bridge transport in HttpClient runtime.");

        var replacement = UrlMarker + "\n" +
            "        if (XPScriptBrowserServerBridgeTransport.IsBridgeUrl(url))\n" +
            "            return XPScriptBrowserServerBridgeTransport.Send(method, url, bodyValue, _headers);";

        return generated.Replace(UrlMarker, replacement, StringComparison.Ordinal) + "\n\n" + RuntimeCode;
    }

    public static string TransformBrowserModule(string module)
    {
        ArgumentNullException.ThrowIfNull(module);
        if (module.Contains("__xpscriptWasmBridgeRequest", StringComparison.Ordinal)) return module;
        return module.TrimEnd() + "\n\n" + BrowserModuleCode + "\n";
    }

    private const string RuntimeCode = """
internal static partial class XPScriptBrowserServerBridgeTransport
{
    private const int MaxBridgeResponseChars = 16 * 1024 * 1024;

    public static bool IsBridgeUrl(string url) =>
        url.Equals("__xpscript_bridge", StringComparison.Ordinal) ||
        url.StartsWith("__xpscript_bridge/", StringComparison.Ordinal);

    public static XPScriptHttpResponse Send(
        System.Net.Http.HttpMethod method,
        string relativeUrl,
        object? bodyValue,
        Dictionary<string, string> headers)
    {
        if (!IsBridgeUrl(relativeUrl))
            throw new XPScriptRuntimeException(5, "Invalid browser-wasm server bridge URL.");
        if (method != System.Net.Http.HttpMethod.Get && method != System.Net.Http.HttpMethod.Post)
            throw new XPScriptRuntimeException(5, "Browser-wasm server bridge only supports GET and POST.");

        var headerJson = System.Text.Json.JsonSerializer.Serialize(headers);
        var body = bodyValue is null ? string.Empty : XPScriptRuntime.CStr(bodyValue);
        string responseJson;
        try
        {
            responseJson = Request(method.Method, relativeUrl, headerJson, body);
        }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Browser-wasm server bridge request failed: " + ex.Message);
        }

        if (responseJson.Length > MaxBridgeResponseChars)
            throw new XPScriptRuntimeException(5, "Browser-wasm server bridge response exceeds the supported size.");

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(responseJson, new System.Text.Json.JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement;
            var status = root.GetProperty("status").GetInt32();
            var statusText = root.TryGetProperty("statusText", out var statusTextElement) ? statusTextElement.GetString() ?? string.Empty : string.Empty;
            var responseBody = root.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
            var contentType = root.TryGetProperty("contentType", out var contentTypeElement) ? contentTypeElement.GetString() ?? string.Empty : string.Empty;
            var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("headers", out var headersElement) && headersElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var property in headersElement.EnumerateObject())
                    responseHeaders[property.Name] = property.Value.GetString() ?? string.Empty;
            }

            return new XPScriptHttpResponse
            {
                StatusCode = status,
                StatusText = statusText,
                RawBodyBytes = System.Text.Encoding.UTF8.GetBytes(responseBody),
                BodyEncoding = System.Text.Encoding.UTF8,
                ContentType = contentType,
                Headers = responseHeaders,
                IsSuccess = status is >= 200 and <= 299
            };
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new XPScriptRuntimeException(5, "Browser-wasm server bridge returned an invalid response envelope.");
        }
    }

    [System.Runtime.InteropServices.JavaScript.JSImport("globalThis.__xpscriptWasmBridgeRequest")]
    private static partial string Request(string method, string relativeUrl, string headersJson, string body);
}
""";

    private const string BrowserModuleCode = """
globalThis.__xpscriptWasmBridgeRequest = function(method, relativeUrl, headersJson, body) {
    const parsedHeaders = headersJson ? JSON.parse(headersJson) : {};
    const safeMethod = String(method || '').toUpperCase();
    if (safeMethod !== 'GET' && safeMethod !== 'POST') throw new Error('Unsupported bridge method.');
    const url = String(relativeUrl || '');
    if (url !== '__xpscript_bridge' && !url.startsWith('__xpscript_bridge/')) throw new Error('Invalid bridge URL.');

    const perform = (csrfToken) => {
        const xhr = new XMLHttpRequest();
        xhr.open(safeMethod, url, false);
        for (const [name, value] of Object.entries(parsedHeaders)) xhr.setRequestHeader(name, String(value));
        if (csrfToken) xhr.setRequestHeader('X-XPS-CSRF-Token', csrfToken);
        xhr.send(safeMethod === 'GET' ? null : String(body || ''));
        return xhr;
    };

    let xhr = perform(null);
    if (safeMethod === 'POST' && xhr.status === 403) {
        const csrf = xhr.getResponseHeader('X-XPS-CSRF-Token') || '';
        if (/^[A-Za-z0-9_-]{1,128}$/.test(csrf)) xhr = perform(csrf);
    }

    const headers = {};
    const rawHeaders = xhr.getAllResponseHeaders() || '';
    for (const line of rawHeaders.split(/\r?\n/)) {
        const separator = line.indexOf(':');
        if (separator <= 0) continue;
        headers[line.slice(0, separator).trim()] = line.slice(separator + 1).trim();
    }

    return JSON.stringify({
        status: xhr.status,
        statusText: xhr.statusText || '',
        body: xhr.responseText || '',
        contentType: xhr.getResponseHeader('Content-Type') || '',
        headers
    });
};
""";
}