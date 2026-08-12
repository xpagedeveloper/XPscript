namespace XPScript.Compiler;

internal static class NativeHttpRuntimeSource
{
    public const string Code = """
internal static class XPScriptNativeHttp
{
    public static XPScriptHttpClient CreateClient() => new();
}

internal sealed class XPScriptHttpClient : IDisposable
{
    private const int MaxRequestBodyBytes = 8 * 1024 * 1024;
    private const int MaxResponseBodyBytes = 8 * 1024 * 1024;

    private readonly System.Net.Http.HttpClientHandler _handler;
    private readonly System.Net.Http.HttpClient _client;
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public XPScriptHttpClient()
    {
        // Redirects are intentionally caller-controlled. This prevents custom/auth headers
        // from being silently forwarded to a different origin by automatic redirect handling.
        _handler = new System.Net.Http.HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        _client = new System.Net.Http.HttpClient(_handler, disposeHandler: false);
    }

    public double Timeout
    {
        get => _client.Timeout.TotalSeconds;
        set
        {
            EnsureNotDisposed();
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new XPScriptRuntimeException(5, "HttpClient.Timeout must be a finite value greater than zero.");
            _client.Timeout = TimeSpan.FromSeconds(value);
        }
    }

    public void SetHeader(object? nameValue, object? value)
    {
        EnsureNotDisposed();
        var name = ValidateHeaderName(nameValue);
        var text = XPScriptRuntime.CStr(value);
        ValidateHeaderValue(text);
        _headers[name] = text;
    }

    public void RemoveHeader(object? nameValue)
    {
        EnsureNotDisposed();
        _headers.Remove(ValidateHeaderName(nameValue));
    }

    public void ClearHeaders()
    {
        EnsureNotDisposed();
        _headers.Clear();
    }

    public XPScriptHttpResponse Get(object? url) => Send(System.Net.Http.HttpMethod.Get, url, null);
    public XPScriptHttpResponse Delete(object? url) => Send(System.Net.Http.HttpMethod.Delete, url, null);
    public XPScriptHttpResponse Post(object? url, object? body) => Send(System.Net.Http.HttpMethod.Post, url, body);
    public XPScriptHttpResponse Put(object? url, object? body) => Send(System.Net.Http.HttpMethod.Put, url, body);
    public XPScriptHttpResponse Patch(object? url, object? body) => Send(System.Net.Http.HttpMethod.Patch, url, body);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
        _handler.Dispose();
        GC.SuppressFinalize(this);
    }

    private XPScriptHttpResponse Send(System.Net.Http.HttpMethod method, object? urlValue, object? bodyValue)
    {
        EnsureNotDisposed();
        var url = XPScriptRuntime.CStr(urlValue).Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new XPScriptRuntimeException(5, "HTTP URL must be an absolute http:// or https:// URL.");

        using var request = new System.Net.Http.HttpRequestMessage(method, uri);
        if (bodyValue is not null && method != System.Net.Http.HttpMethod.Get && method != System.Net.Http.HttpMethod.Delete)
        {
            var bodyText = XPScriptRuntime.CStr(bodyValue);
            var requestBytes = Encoding.UTF8.GetByteCount(bodyText);
            if (requestBytes > MaxRequestBodyBytes)
                throw new XPScriptRuntimeException(5, "HTTP request body exceeds the 8 MiB limit.");

            request.Content = new System.Net.Http.StringContent(bodyText, Encoding.UTF8);
            if (_headers.TryGetValue("Content-Type", out var ct) && !string.IsNullOrWhiteSpace(ct))
            {
                try
                {
                    request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ct);
                }
                catch (FormatException)
                {
                    throw new XPScriptRuntimeException(5, "Invalid Content-Type header value.");
                }
            }
        }

        foreach (var header in _headers)
        {
            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content ??= new System.Net.Http.StringContent("");
                if (!request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    throw new XPScriptRuntimeException(5, "HTTP header is not valid for this request.");
            }
        }

        try
        {
            using var response = _client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            var body = ReadResponseBody(response.Content);
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
        catch (TaskCanceledException)
        {
            throw new XPScriptRuntimeException(5, "HTTP request timed out.");
        }
        catch (System.Net.Http.HttpRequestException)
        {
            throw new XPScriptRuntimeException(5, "HTTP request failed.");
        }
        catch (IOException)
        {
            throw new XPScriptRuntimeException(5, "HTTP response could not be read.");
        }
    }

    private static string ReadResponseBody(System.Net.Http.HttpContent content)
    {
        if (content.Headers.ContentLength is long declaredLength && declaredLength > MaxResponseBodyBytes)
            throw new XPScriptRuntimeException(5, "HTTP response body exceeds the 8 MiB limit.");

        using var stream = content.ReadAsStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        var total = 0;

        while (true)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read == 0) break;
            total = checked(total + read);
            if (total > MaxResponseBodyBytes)
                throw new XPScriptRuntimeException(5, "HTTP response body exceeds the 8 MiB limit.");
            buffer.Write(chunk, 0, read);
        }

        var charset = content.Headers.ContentType?.CharSet;
        Encoding encoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try { encoding = Encoding.GetEncoding(charset.Trim().Trim('"')); }
            catch (ArgumentException)
            {
                throw new XPScriptRuntimeException(5, "HTTP response specifies an unsupported text charset.");
            }
        }
        return encoding.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new XPScriptRuntimeException(5, "HttpClient has been disposed.");
    }

    private static string ValidateHeaderName(object? nameValue)
    {
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0)
            throw new XPScriptRuntimeException(5, "HTTP header name cannot be empty.");

        foreach (var c in name)
        {
            if (!IsHeaderTokenCharacter(c))
                throw new XPScriptRuntimeException(5, "HTTP header name contains an invalid character.");
        }
        return name;
    }

    private static void ValidateHeaderValue(string value)
    {
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, "HTTP header value contains a prohibited control character.");

        foreach (var c in value)
        {
            if ((c < 0x20 && c != '\t') || c == 0x7f)
                throw new XPScriptRuntimeException(5, "HTTP header value contains a prohibited control character.");
        }
    }

    private static bool IsHeaderTokenCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
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
