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
    private const int MaxResponseBodyBytes = 64 * 1024 * 1024;

    private readonly System.Net.Http.HttpClientHandler _handler;
    private readonly System.Net.Http.HttpClient _client;
    private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private bool _disposed;

    public XPScriptHttpClient()
    {
        _handler = new System.Net.Http.HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        _client = new System.Net.Http.HttpClient(_handler, disposeHandler: false)
        {
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };
    }

    public double Timeout
    {
        get => _timeout.TotalSeconds;
        set
        {
            EnsureNotDisposed();
            if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value))
                throw new XPScriptRuntimeException(5, "HttpClient.Timeout must be a finite value greater than zero.");
            try { _timeout = TimeSpan.FromSeconds(value); }
            catch (OverflowException)
            {
                throw new XPScriptRuntimeException(5, "HttpClient.Timeout is outside the supported range.");
            }
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
        ValidateOutboundTarget(uri);

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
                try { request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ct); }
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
            using var timeout = new CancellationTokenSource(_timeout);
            using var response = _client.Send(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var bodyBytes = ReadResponseBody(response.Content, timeout.Token, out var bodyEncoding);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers) headers[header.Key] = string.Join(", ", header.Value);
            foreach (var header in response.Content.Headers) headers[header.Key] = string.Join(", ", header.Value);
            return new XPScriptHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                StatusText = response.ReasonPhrase ?? "",
                RawBodyBytes = bodyBytes,
                BodyEncoding = bodyEncoding,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "",
                ContentDisposition = response.Content.Headers.ContentDisposition?.ToString() ?? "",
                Headers = headers,
                IsSuccess = response.IsSuccessStatusCode
            };
        }
        catch (OperationCanceledException)
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

    private static void ValidateOutboundTarget(Uri uri)
    {
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new XPScriptRuntimeException(5, "HTTP URL user information is not permitted.");
        if (uri.HostNameType == UriHostNameType.Unknown || string.IsNullOrWhiteSpace(uri.Host))
            throw new XPScriptRuntimeException(5, "HTTP URL host is invalid.");

        System.Net.IPAddress[] addresses;
        if (System.Net.IPAddress.TryParse(uri.Host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            try { addresses = System.Net.Dns.GetHostAddresses(uri.DnsSafeHost); }
            catch (System.Net.Sockets.SocketException)
            {
                throw new XPScriptRuntimeException(5, "HTTP host could not be resolved.");
            }
        }

        if (addresses.Length == 0)
            throw new XPScriptRuntimeException(5, "HTTP host could not be resolved.");
        if (addresses.Any(IsPrivateOrLocalAddress))
            throw new XPScriptRuntimeException(5, "HTTP target resolves to a private or local network address.");
    }

    private static bool IsPrivateOrLocalAddress(System.Net.IPAddress address)
    {
        if (System.Net.IPAddress.IsLoopback(address)) return true;
        if (address.Equals(System.Net.IPAddress.Any) || address.Equals(System.Net.IPAddress.IPv6Any) ||
            address.Equals(System.Net.IPAddress.None) || address.Equals(System.Net.IPAddress.IPv6None)) return true;

        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 0 ||
                   bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                   (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                   bytes[0] >= 224;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal ||
                   address.IsIPv6Multicast ||
                   (bytes[0] & 0xfe) == 0xfc ||
                   address.Equals(System.Net.IPAddress.IPv6Loopback);
        }

        return true;
    }

    private static byte[] ReadResponseBody(System.Net.Http.HttpContent content, CancellationToken cancellationToken, out Encoding encoding)
    {
        if (content.Headers.ContentLength is long declaredLength && declaredLength > MaxResponseBodyBytes)
            throw new XPScriptRuntimeException(5, "HTTP response body exceeds the 64 MiB limit.");

        using var stream = content.ReadAsStream(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        var total = 0;

        while (true)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0) break;
            total = checked(total + read);
            if (total > MaxResponseBodyBytes)
                throw new XPScriptRuntimeException(5, "HTTP response body exceeds the 64 MiB limit.");
            buffer.Write(chunk, 0, read);
        }

        var charset = content.Headers.ContentType?.CharSet;
        encoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try { encoding = Encoding.GetEncoding(charset.Trim().Trim('"')); }
            catch (ArgumentException)
            {
                throw new XPScriptRuntimeException(5, "HTTP response specifies an unsupported text charset.");
            }
        }
        return buffer.ToArray();
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
    private XPScriptHttpPartCollection? _parts;
    private XPScriptHttpFileCollection? _files;

    internal byte[] RawBodyBytes { get; init; } = [];
    internal Encoding BodyEncoding { get; init; } = Encoding.UTF8;
    internal string ContentDisposition { get; init; } = "";

    public int StatusCode { get; init; }
    public string StatusText { get; init; } = "";
    public string Body => BodyEncoding.GetString(RawBodyBytes);
    public long BodyLength => RawBodyBytes.LongLength;
    public string ContentType { get; init; } = "";
    public Dictionary<string, string> Headers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsSuccess { get; init; }
    public string FileName => XPScriptHttpMultipart.SafeFileName(XPScriptHttpMultipart.GetDispositionParameter(ContentDisposition, "filename*", "filename"));
    public XPScriptHttpPartCollection Parts => _parts ??= XPScriptHttpMultipart.ParseParts(RawBodyBytes, ContentType, ContentDisposition);
    public int PartCount => Parts.Count;
    public XPScriptHttpPart GetPart(object? indexValue) => Parts.Get(indexValue);
    public XPScriptHttpFileCollection Files => _files ??= new XPScriptHttpFileCollection(Parts.Items.Where(part => part.IsFile));
    public int FileCount => Files.Count;
    public XPScriptHttpPart GetFile(object? indexValue) => Files.Get(indexValue);

    public void SaveBodyToFile(object? pathValue) => XPScriptHttpFileStorage.Save(pathValue, RawBodyBytes);
}

internal sealed class XPScriptHttpPartCollection
{
    internal List<XPScriptHttpPart> Items { get; }

    public XPScriptHttpPartCollection(IEnumerable<XPScriptHttpPart> parts) => Items = [.. parts];
    public int Count => Items.Count;

    public XPScriptHttpPart Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= Items.Count)
            throw new XPScriptRuntimeException(9, "HTTP response part index is out of range.");
        return Items[index];
    }
}

internal sealed class XPScriptHttpFileCollection
{
    private readonly List<XPScriptHttpPart> _files;

    public XPScriptHttpFileCollection(IEnumerable<XPScriptHttpPart> files) => _files = [.. files];
    public int Count => _files.Count;

    public XPScriptHttpPart Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _files.Count)
            throw new XPScriptRuntimeException(9, "HTTP response file index is out of range.");
        return _files[index];
    }
}

internal sealed class XPScriptHttpPart
{
    private readonly byte[] _data;

    public XPScriptHttpPart(string name, string fileName, string contentType, Dictionary<string, string> headers, byte[] data)
    {
        Name = name;
        FileName = fileName;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        Headers = headers;
        _data = data;
    }

    public string Name { get; }
    public string FileName { get; }
    public string ContentType { get; }
    public Dictionary<string, string> Headers { get; }
    public long Length => _data.LongLength;
    public bool IsFile => FileName.Length > 0;
    public bool IsText => XPScriptHttpMultipart.IsTextContentType(ContentType);
    public string Body => XPScriptHttpMultipart.DecodeText(_data, ContentType);
    public void SaveToFile(object? pathValue) => XPScriptHttpFileStorage.Save(pathValue, _data);
}

internal static class XPScriptHttpFileStorage
{
    public static void Save(object? pathValue, byte[] data)
    {
        var path = XPScriptRuntime.CStr(pathValue).Trim();
        if (path.Length == 0)
            throw new XPScriptRuntimeException(5, "HTTP file save requires a file path.");

        string fullPath;
        try { fullPath = Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new XPScriptRuntimeException(5, "HTTP file save received an invalid file path.");
        }

        try
        {
            if (Directory.Exists(fullPath))
                throw new XPScriptRuntimeException(5, "HTTP file save target must be a file.");
            if (File.Exists(fullPath) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new XPScriptRuntimeException(5, "HTTP file save refuses symbolic-link or reparse-point targets.");

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                throw new XPScriptRuntimeException(5, "HTTP file save target directory does not exist.");

            using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(data, 0, data.Length);
            stream.Flush(flushToDisk: true);
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (UnauthorizedAccessException)
        {
            throw new XPScriptRuntimeException(70, "Permission denied while saving HTTP response data.");
        }
        catch (IOException)
        {
            throw new XPScriptRuntimeException(75, "Unable to save HTTP response data.");
        }
    }
}

internal static class XPScriptHttpMultipart
{
    private static readonly byte[] HeaderSeparator = [13, 10, 13, 10];

    public static XPScriptHttpPartCollection ParseParts(byte[] body, string contentType, string contentDisposition)
    {
        var parts = new List<XPScriptHttpPart>();
        if (!contentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = SafeFileName(GetDispositionParameter(contentDisposition, "filename*", "filename"));
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (contentType.Length > 0) headers["Content-Type"] = contentType;
            if (contentDisposition.Length > 0) headers["Content-Disposition"] = contentDisposition;
            parts.Add(new XPScriptHttpPart("", fileName, contentType, headers, [.. body]));
            return new XPScriptHttpPartCollection(parts);
        }

        var boundary = GetMediaTypeParameter(contentType, "boundary");
        if (boundary.Length == 0 || boundary.Length > 200)
            throw new XPScriptRuntimeException(5, "Multipart HTTP response has an invalid boundary.");

        var delimiter = Encoding.ASCII.GetBytes("--" + boundary);
        var prefixedDelimiter = Encoding.ASCII.GetBytes("\r\n--" + boundary);
        var cursor = IndexOf(body, delimiter, 0);
        if (cursor < 0)
            throw new XPScriptRuntimeException(5, "Multipart HTTP response boundary was not found.");

        while (cursor >= 0)
        {
            var afterBoundary = cursor + delimiter.Length;
            if (HasBytes(body, afterBoundary, (byte)'-', (byte)'-')) break;
            if (!HasBytes(body, afterBoundary, 13, 10))
                throw new XPScriptRuntimeException(5, "Multipart HTTP response has malformed boundary framing.");

            var headerStart = afterBoundary + 2;
            var headerEnd = IndexOf(body, HeaderSeparator, headerStart);
            if (headerEnd < 0)
                throw new XPScriptRuntimeException(5, "Multipart HTTP response has malformed part headers.");

            var dataStart = headerEnd + HeaderSeparator.Length;
            var nextMarker = IndexOf(body, prefixedDelimiter, dataStart);
            if (nextMarker < 0)
                throw new XPScriptRuntimeException(5, "Multipart HTTP response is missing a closing boundary.");

            var headerText = Encoding.Latin1.GetString(body, headerStart, headerEnd - headerStart);
            var headers = ParsePartHeaders(headerText);
            headers.TryGetValue("Content-Disposition", out var disposition);
            headers.TryGetValue("Content-Type", out var partContentType);
            var fileName = SafeFileName(GetDispositionParameter(disposition ?? "", "filename*", "filename"));
            var name = GetDispositionParameter(disposition ?? "", "name");
            var length = nextMarker - dataStart;
            var data = new byte[length];
            Buffer.BlockCopy(body, dataStart, data, 0, length);
            parts.Add(new XPScriptHttpPart(name, fileName, partContentType ?? "application/octet-stream", headers, data));

            cursor = nextMarker + 2;
        }

        return new XPScriptHttpPartCollection(parts);
    }

    public static bool IsTextContentType(string contentType)
    {
        var mediaType = contentType.Split(';', 2)[0].Trim();
        return mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/xml", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)
            || mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    public static string DecodeText(byte[] data, string contentType)
    {
        var charset = GetMediaTypeParameter(contentType, "charset");
        var encoding = Encoding.UTF8;
        if (charset.Length > 0)
        {
            try { encoding = Encoding.GetEncoding(charset); }
            catch (ArgumentException)
            {
                throw new XPScriptRuntimeException(5, "HTTP multipart part specifies an unsupported text charset.");
            }
        }
        return encoding.GetString(data);
    }

    public static string GetDispositionParameter(string value, params string[] names)
    {
        foreach (var name in names)
        {
            var raw = GetParameter(value, name);
            if (raw.Length == 0) continue;
            if (name.EndsWith('*'))
            {
                var marker = raw.IndexOf("''", StringComparison.Ordinal);
                if (marker >= 0)
                {
                    var charset = raw[..marker];
                    var encoded = raw[(marker + 2)..];
                    if (charset.Equals("UTF-8", StringComparison.OrdinalIgnoreCase) || charset.Length == 0)
                    {
                        try { return Uri.UnescapeDataString(encoded); }
                        catch (UriFormatException) { return ""; }
                    }
                }
            }
            return raw;
        }
        return "";
    }

    public static string SafeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var leaf = value.Replace('\\', '/');
        var slash = leaf.LastIndexOf('/');
        if (slash >= 0) leaf = leaf[(slash + 1)..];
        leaf = new string(leaf.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (leaf is "." or "..") return "";
        return leaf;
    }

    private static string GetMediaTypeParameter(string value, string name) => GetParameter(value, name);

    private static string GetParameter(string value, string name)
    {
        var parts = value.Split(';');
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            var equals = part.IndexOf('=');
            if (equals <= 0) continue;
            if (!part[..equals].Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            var result = part[(equals + 1)..].Trim();
            if (result.Length >= 2 && result[0] == '"' && result[^1] == '"')
                result = result[1..^1].Replace("\\\"", "\"");
            return result;
        }
        return "";
    }

    private static Dictionary<string, string> ParsePartHeaders(string text)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split("\r\n", StringSplitOptions.None))
        {
            var colon = raw.IndexOf(':');
            if (colon <= 0) continue;
            var name = raw[..colon].Trim();
            var value = raw[(colon + 1)..].Trim();
            if (name.Length > 0) headers[name] = value;
        }
        return headers;
    }

    private static int IndexOf(byte[] source, byte[] target, int start)
    {
        if (target.Length == 0) return start;
        for (var i = Math.Max(0, start); i <= source.Length - target.Length; i++)
        {
            var match = true;
            for (var j = 0; j < target.Length; j++)
            {
                if (source[i + j] == target[j]) continue;
                match = false;
                break;
            }
            if (match) return i;
        }
        return -1;
    }

    private static bool HasBytes(byte[] source, int index, byte first, byte second) =>
        index >= 0 && index + 1 < source.Length && source[index] == first && source[index + 1] == second;
}
""";
}
