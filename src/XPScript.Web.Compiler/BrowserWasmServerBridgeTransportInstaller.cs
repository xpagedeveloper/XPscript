namespace XPScript.Web.Compiler;

internal static class BrowserWasmServerBridgeTransportInstaller
{
    private const string SendUrlMarker = "        EnsureNotDisposed();\n        var url = XPScriptRuntime.CStr(urlValue).Trim();";
    private const string InstalledMarker = "XPScriptBrowserServerBridgeTransport.Send(method, url, bodyValue, _headers)";

    public static string TransformGenerated(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(InstalledMarker, StringComparison.Ordinal)) return generated;
        if (!generated.Contains(SendUrlMarker, StringComparison.Ordinal)) throw new XpsWebCompilationException("Unable to install browser-wasm server bridge transport in HttpClient runtime.");
        var replacement = SendUrlMarker + "\n" + "        if (XPScriptBrowserServerBridgeTransport.IsBridgeUrl(url))\n" + "            return XPScriptBrowserServerBridgeTransport.Send(method, url, bodyValue, _headers);";
        return generated.Replace(SendUrlMarker, replacement, StringComparison.Ordinal) + "\n\n" + RuntimeCode;
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
    public static bool IsBridgeUrl(string url) => url.Equals("__xpscript_bridge", StringComparison.Ordinal) || url.StartsWith("__xpscript_bridge/", StringComparison.Ordinal);

    public static XPScriptHttpResponse Send(System.Net.Http.HttpMethod method, string relativeUrl, object? bodyValue, Dictionary<string, string> headers)
    {
        if (!IsBridgeUrl(relativeUrl)) throw new XPScriptRuntimeException(5, "Invalid browser-wasm server bridge URL.");
        if (method != System.Net.Http.HttpMethod.Get && method != System.Net.Http.HttpMethod.Post) throw new XPScriptRuntimeException(5, "Browser-wasm server bridge only supports GET and POST.");
        var headerJson = System.Text.Json.JsonSerializer.Serialize(headers);
        var body = bodyValue is null ? string.Empty : XPScriptRuntime.CStr(bodyValue);
        string responseJson;
        try { responseJson = RequestAsync(method.Method, relativeUrl, headerJson, body).GetAwaiter().GetResult(); }
        catch (Exception ex) { throw new XPScriptRuntimeException(5, "Browser-wasm server bridge request failed: " + ex.Message); }
        if (responseJson.Length > MaxBridgeResponseChars) throw new XPScriptRuntimeException(5, "Browser-wasm server bridge response exceeds the supported size.");
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(responseJson, new System.Text.Json.JsonDocumentOptions { MaxDepth = 16 });
            var root = document.RootElement; var status = root.GetProperty("status").GetInt32();
            var statusText = root.TryGetProperty("statusText", out var statusTextElement) ? statusTextElement.GetString() ?? string.Empty : string.Empty;
            var responseBody = root.TryGetProperty("body", out var bodyElement) ? bodyElement.GetString() ?? string.Empty : string.Empty;
            var contentType = root.TryGetProperty("contentType", out var contentTypeElement) ? contentTypeElement.GetString() ?? string.Empty : string.Empty;
            var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("headers", out var headersElement) && headersElement.ValueKind == System.Text.Json.JsonValueKind.Object) foreach (var property in headersElement.EnumerateObject()) responseHeaders[property.Name] = property.Value.GetString() ?? string.Empty;
            return new XPScriptHttpResponse { StatusCode = status, StatusText = statusText, RawBodyBytes = System.Text.Encoding.UTF8.GetBytes(responseBody), BodyEncoding = System.Text.Encoding.UTF8, ContentType = contentType, Headers = responseHeaders, IsSuccess = status is >= 200 and <= 299 };
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException) { throw new XPScriptRuntimeException(5, "Browser-wasm server bridge returned an invalid response envelope."); }
    }

    [System.Runtime.InteropServices.JavaScript.JSImport("globalThis.__xpscriptWasmBridgeRequest")]
    private static partial System.Threading.Tasks.Task<string> RequestAsync(string method, string relativeUrl, string headersJson, string body);
}
""";

    private const string BrowserModuleCode = """
(() => {
    const defaultSpinnerDelayMs = 300; const activeRequests = new Map(); let nextBusyToken = 1;
    const ensureBusyOverlay = () => { let overlay = document.getElementById('xpscript-server-busy'); if (overlay) return overlay; overlay = document.createElement('div'); overlay.id = 'xpscript-server-busy'; overlay.setAttribute('role', 'status'); overlay.setAttribute('aria-live', 'polite'); overlay.setAttribute('aria-label', 'Server request in progress'); overlay.style.cssText = 'position:fixed;inset:0;z-index:2147483647;display:none;align-items:center;justify-content:center;background:rgba(255,255,255,.42);backdrop-filter:blur(1px);'; const spinner = document.createElement('div'); spinner.style.cssText = 'width:2.75rem;height:2.75rem;border:.32rem solid rgba(0,0,0,.18);border-top-color:currentColor;border-radius:50%;animation:xpscript-server-spin .8s linear infinite;'; const style = document.createElement('style'); style.textContent = '@keyframes xpscript-server-spin{to{transform:rotate(360deg)}}'; overlay.appendChild(spinner); document.head.appendChild(style); document.body.appendChild(overlay); return overlay; };
    const refreshBusy = () => { const visible = Array.from(activeRequests.values()).some(request => request.matured); const overlay = document.getElementById('xpscript-server-busy'); if (visible) ensureBusyOverlay().style.display = 'flex'; else if (overlay) overlay.style.display = 'none'; document.documentElement.setAttribute('aria-busy', visible ? 'true' : 'false'); };
    const beginBusy = (delayMs = defaultSpinnerDelayMs) => { const token = nextBusyToken++; const delay = Number.isFinite(Number(delayMs)) && Number(delayMs) >= 0 ? Number(delayMs) : defaultSpinnerDelayMs; const request = { matured: delay === 0, timer: 0 }; activeRequests.set(token, request); if (request.matured) refreshBusy(); else request.timer = window.setTimeout(() => { const active = activeRequests.get(token); if (!active) return; active.timer = 0; active.matured = true; refreshBusy(); }, delay); return token; };
    const endBusy = token => { const request = activeRequests.get(token); if (!request) return; if (request.timer) window.clearTimeout(request.timer); activeRequests.delete(token); refreshBusy(); };
    globalThis.__xpscriptWasmBridgeBusy = { begin: beginBusy, end: endBusy };
})();

globalThis.__xpscriptWasmBridgeRequest = async function(method, relativeUrl, headersJson, body) {
    const parsedHeaders = headersJson ? JSON.parse(headersJson) : {}; let spinnerDelayMs = 300;
    for (const name of Object.keys(parsedHeaders)) { if (name.toLowerCase() !== 'x-xps-wasm-spinner-delay') continue; const parsedDelay = Number(parsedHeaders[name]); if (Number.isInteger(parsedDelay) && parsedDelay >= 0) spinnerDelayMs = parsedDelay; delete parsedHeaders[name]; }
    const safeMethod = String(method || '').toUpperCase(); if (safeMethod !== 'GET' && safeMethod !== 'POST') throw new Error('Unsupported bridge method.');
    const url = String(relativeUrl || ''); if (url !== '__xpscript_bridge' && !url.startsWith('__xpscript_bridge/')) throw new Error('Invalid bridge URL.');
    const busy = globalThis.__xpscriptWasmBridgeBusy; const busyToken = busy.begin(spinnerDelayMs);
    try {
        const perform = async csrfToken => { const headers = new Headers(); for (const [name, value] of Object.entries(parsedHeaders)) headers.set(name, String(value)); if (csrfToken) headers.set('X-XPS-CSRF-Token', csrfToken); return await fetch(url, { method: safeMethod, headers, body: safeMethod === 'GET' ? undefined : String(body || ''), credentials: 'same-origin', cache: 'no-store' }); };
        let response = await perform(null);
        if (safeMethod === 'POST' && response.status === 403) { const csrf = response.headers.get('X-XPS-CSRF-Token') || ''; if (/^[A-Za-z0-9_-]{1,128}$/.test(csrf)) response = await perform(csrf); }
        const responseBody = await response.text(); const headers = {}; response.headers.forEach((value, name) => { headers[name] = value; });
        return JSON.stringify({ status: response.status, statusText: response.statusText || '', body: responseBody || '', contentType: response.headers.get('Content-Type') || '', headers });
    } finally { busy.end(busyToken); }
};
""";
}
