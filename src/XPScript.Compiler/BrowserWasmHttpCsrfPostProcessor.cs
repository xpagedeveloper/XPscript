namespace XPScript.Compiler;

internal sealed class BrowserWasmHttpCsrfPostProcessor
{
    private const string SendMarker = "            using var response = _client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, timeout.Token);";

    private readonly string _runtimeIdentifier;

    public BrowserWasmHttpCsrfPostProcessor(string runtimeIdentifier)
    {
        _runtimeIdentifier = runtimeIdentifier ?? string.Empty;
    }

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        generated = new CompilerSourceLineDirectivePostProcessor().Transform(generated);
        generated = new EvaluateSecurityPostProcessor().Transform(generated);
        if (!_runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase)) return generated;
        if (!generated.Contains("internal sealed class XPScriptHttpClient", StringComparison.Ordinal)) return generated;
        if (generated.Contains("__xpscriptCsrfRetryToken", StringComparison.Ordinal)) return generated;
        if (!generated.Contains(SendMarker, StringComparison.Ordinal))
            throw new CompilerException("Unable to install browser-wasm CSRF protection in HttpClient.");

        const string replacement = """
            using var response = _client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (method != System.Net.Http.HttpMethod.Get &&
                method != System.Net.Http.HttpMethod.Head &&
                !_headers.ContainsKey("X-XPS-CSRF-Token") &&
                (int)response.StatusCode == 403 &&
                response.Headers.TryGetValues("X-XPS-CSRF-Token", out var __xpscriptCsrfValues))
            {
                var __xpscriptCsrfRetryToken = __xpscriptCsrfValues.FirstOrDefault() ?? string.Empty;
                if (__xpscriptCsrfRetryToken.Length is > 0 and <= 128 &&
                    __xpscriptCsrfRetryToken.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
                {
                    _headers["X-XPS-CSRF-Token"] = __xpscriptCsrfRetryToken;
                    try { return Send(method, urlValue, bodyValue); }
                    finally { _headers.Remove("X-XPS-CSRF-Token"); }
                }
            }
""";
        return generated.Replace(SendMarker, replacement.TrimEnd('\n'), StringComparison.Ordinal);
    }
}