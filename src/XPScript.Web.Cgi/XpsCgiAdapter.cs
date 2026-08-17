using System.Globalization;
using System.Text;
using XPScript.Web.Runtime;

namespace XPScript.Web.Cgi;

public sealed class XpsCgiOptions
{
    public int MaxRequestBodyBytes { get; init; } = 4 * 1024 * 1024;
    public int MaxHeaderCount { get; init; } = 128;
    public int MaxHeaderValueBytes { get; init; } = 16 * 1024;

    public void Validate()
    {
        if (MaxRequestBodyBytes < 0 || MaxRequestBodyBytes > 256 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxRequestBodyBytes));
        if (MaxHeaderCount < 1 || MaxHeaderCount > 10_000)
            throw new ArgumentOutOfRangeException(nameof(MaxHeaderCount));
        if (MaxHeaderValueBytes < 1 || MaxHeaderValueBytes > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxHeaderValueBytes));
    }
}

public sealed class XpsCgiException : Exception
{
    public XpsCgiException(string message) : base(message) { }
    public XpsCgiException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class XpsCgiAdapter
{
    private readonly XpsCgiOptions _options;
    private readonly XpsServerInfo _serverInfo;
    private readonly IXpsWebRequestHandler _handler;
    private readonly IXpsApplicationState _application;
    private readonly XpsSessionStore? _sessions;
    private readonly Func<XpsWebRequest, XpsWebResponse, IXpsSession?>? _sessionFactory;
    private readonly Func<XpsWebRequest, XpsWebPrincipal>? _principalFactory;

    public XpsCgiAdapter(
        XpsCgiOptions options,
        XpsServerInfo serverInfo,
        IXpsWebRequestHandler handler,
        IXpsApplicationState? application = null,
        XpsSessionStore? sessions = null,
        Func<XpsWebRequest, XpsWebPrincipal>? principalFactory = null,
        Func<XpsWebRequest, XpsWebResponse, IXpsSession?>? sessionFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _application = application ?? new XpsApplicationState();
        _sessions = sessions;
        _principalFactory = principalFactory;
        _sessionFactory = sessionFactory;
    }

    public async Task RunAsync(
        Stream stdin,
        Stream stdout,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(environment);

        var body = await ReadBodyAsync(stdin, environment, cancellationToken).ConfigureAwait(false);
        var request = CreateRequest(environment, body, cancellationToken);
        var response = new XpsWebResponse();
        var principal = _principalFactory?.Invoke(request) ?? new XpsWebPrincipal(false);
        var session = _sessionFactory?.Invoke(request, response) ?? _sessions?.Bind(request, response);
        var context = new XpsWebContext(request, response, _serverInfo, principal, _application, session);

        using (XpsWebContextAccessor.Push(context))
            await _handler.HandleAsync(context).ConfigureAwait(false);
        if (!response.Completed) response.Complete();

        await WriteResponseAsync(stdout, response, request.Method, cancellationToken).ConfigureAwait(false);
    }

    private async Task<byte[]> ReadBodyAsync(
        Stream stdin,
        IReadOnlyDictionary<string, string?> environment,
        CancellationToken cancellationToken)
    {
        var rawLength = Value(environment, "CONTENT_LENGTH");
        if (string.IsNullOrEmpty(rawLength)) return Array.Empty<byte>();
        if (!int.TryParse(rawLength, NumberStyles.None, CultureInfo.InvariantCulture, out var length) || length < 0)
            throw new XpsCgiException("Invalid CGI CONTENT_LENGTH.");
        if (length > _options.MaxRequestBodyBytes)
            throw new XpsCgiException("CGI request body exceeds the configured limit.");

        var body = new byte[length];
        var offset = 0;
        while (offset < body.Length)
        {
            var read = await stdin.ReadAsync(body.AsMemory(offset), cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new XpsCgiException("CGI request body ended before CONTENT_LENGTH bytes were read.");
            offset += read;
        }
        return body;
    }

    private XpsWebRequest CreateRequest(
        IReadOnlyDictionary<string, string?> environment,
        byte[] body,
        CancellationToken cancellationToken)
    {
        var method = Required(environment, "REQUEST_METHOD");
        var scriptName = Value(environment, "SCRIPT_NAME") ?? "/";
        var pathInfo = Value(environment, "PATH_INFO") ?? string.Empty;
        var query = Value(environment, "QUERY_STRING") ?? string.Empty;
        var contentType = EmptyToNull(Value(environment, "CONTENT_TYPE"));
        var contentLength = body.Length == 0 && string.IsNullOrEmpty(Value(environment, "CONTENT_LENGTH"))
            ? null
            : (long?)body.Length;

        ValidateScriptFilename(environment);
        var headers = ExtractHeaders(environment);
        var cookies = ParseCookies(headers);
        var host = headers.TryGetValue("Host", out var hostValues) && hostValues.Count > 0
            ? hostValues[0]
            : Value(environment, "SERVER_NAME") ?? string.Empty;
        var scheme = IsHttps(environment) ? "https" : "http";
        var protocol = Value(environment, "SERVER_PROTOCOL") ?? "HTTP/1.1";
        var remoteAddress = EmptyToNull(Value(environment, "REMOTE_ADDR"));

        return new XpsWebRequest(
            method,
            NormalizeRequestPath(scriptName, pathInfo),
            pathInfo,
            query,
            headers,
            contentType,
            contentLength,
            body,
            host,
            scheme,
            remoteAddress,
            protocol,
            cookies,
            cancellationToken);
    }

    private void ValidateScriptFilename(IReadOnlyDictionary<string, string?> environment)
    {
        var value = Value(environment, "SCRIPT_FILENAME");
        if (string.IsNullOrWhiteSpace(value)) return;

        string full;
        string root;
        try
        {
            full = Path.GetFullPath(value);
            root = Path.GetFullPath(_serverInfo.RootPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new XpsCgiException("Invalid CGI SCRIPT_FILENAME.", ex);
        }

        EnsureUnderRoot(root, full);
        try
        {
            var info = new FileInfo(full);
            if (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null) EnsureUnderRoot(root, Path.GetFullPath(target.FullName));
            }
        }
        catch (IOException ex)
        {
            throw new XpsCgiException("Unable to validate CGI SCRIPT_FILENAME.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new XpsCgiException("Unable to validate CGI SCRIPT_FILENAME.", ex);
        }
    }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> ExtractHeaders(
        IReadOnlyDictionary<string, string?> environment)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in environment)
        {
            if (pair.Value is null) continue;
            string? name = pair.Key.StartsWith("HTTP_", StringComparison.Ordinal)
                ? pair.Key[5..].Replace('_', '-')
                : pair.Key.Equals("CONTENT_TYPE", StringComparison.Ordinal) ? "Content-Type"
                : pair.Key.Equals("CONTENT_LENGTH", StringComparison.Ordinal) ? "Content-Length"
                : null;
            if (name is null) continue;
            if (Encoding.UTF8.GetByteCount(pair.Value) > _options.MaxHeaderValueBytes)
                throw new XpsCgiException("CGI HTTP header value exceeds the configured limit.");
            XpsWebResponse.ValidateHeaderName(name);
            XpsWebResponse.ValidateHeaderValue(pair.Value);
            if (result.Count >= _options.MaxHeaderCount && !result.ContainsKey(name))
                throw new XpsCgiException("CGI HTTP header count exceeds the configured limit.");
            result[name] = Array.AsReadOnly(new[] { pair.Value });
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> ParseCookies(
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers)
    {
        var cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!headers.TryGetValue("Cookie", out var values)) return cookies;
        foreach (var header in values)
            foreach (var segment in header.Split(';'))
            {
                var item = segment.Trim();
                var equals = item.IndexOf('=');
                if (equals <= 0) continue;
                var name = item[..equals].Trim();
                if (!cookies.ContainsKey(name)) cookies[name] = item[(equals + 1)..].Trim();
            }
        return cookies;
    }

    public static async Task WriteErrorAsync(Stream stdout, int statusCode, string message, CancellationToken cancellationToken = default)
    {
        var response = new XpsWebResponse
        {
            StatusCode = statusCode,
            ContentType = "text/plain; charset=utf-8"
        };
        response.Write(message);
        response.Complete();
        await WriteResponseAsync(stdout, response, null, cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteResponseAsync(Stream stdout, XpsWebResponse response, string? method, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append("Status: ")
            .Append(response.StatusCode.ToString(CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(ReasonPhrase(response.StatusCode))
            .Append("\r\n");
        if (!string.IsNullOrWhiteSpace(response.ContentType))
            builder.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
        foreach (var header in response.Headers)
            foreach (var value in header.Value)
                builder.Append(header.Key).Append(": ").Append(value).Append("\r\n");
        builder.Append("\r\n");

        var headerBytes = Encoding.UTF8.GetBytes(builder.ToString());
        await stdout.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase) && !response.Body.IsEmpty)
            await stdout.WriteAsync(response.Body, cancellationToken).ConfigureAwait(false);
        await stdout.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void EnsureUnderRoot(string root, string fullPath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!fullPath.Equals(root, comparison) && !fullPath.StartsWith(prefix, comparison))
            throw new XpsCgiException("CGI SCRIPT_FILENAME escapes the configured site root.");
    }

    private static string NormalizeRequestPath(string scriptName, string pathInfo)
    {
        var path = string.IsNullOrWhiteSpace(scriptName) ? "/" : scriptName;
        if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/" + path;
        if (!string.IsNullOrEmpty(pathInfo) && pathInfo != "/" && !path.EndsWith(pathInfo, StringComparison.Ordinal))
            path += pathInfo.StartsWith("/", StringComparison.Ordinal) ? pathInfo : "/" + pathInfo;
        return path;
    }

    private static bool IsHttps(IReadOnlyDictionary<string, string?> environment)
    {
        var value = Value(environment, "HTTPS");
        return value is not null &&
               (value.Equals("on", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.Ordinal));
    }

    private static string Required(IReadOnlyDictionary<string, string?> environment, string name) =>
        !string.IsNullOrWhiteSpace(Value(environment, name))
            ? Value(environment, name)!
            : throw new XpsCgiException($"Required CGI variable {name} is missing.");

    private static string? Value(IReadOnlyDictionary<string, string?> environment, string name) =>
        environment.TryGetValue(name, out var value) ? value : null;

    private static string? EmptyToNull(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        201 => "Created",
        204 => "No Content",
        301 => "Moved Permanently",
        302 => "Found",
        303 => "See Other",
        304 => "Not Modified",
        307 => "Temporary Redirect",
        308 => "Permanent Redirect",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        409 => "Conflict",
        413 => "Payload Too Large",
        415 => "Unsupported Media Type",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        502 => "Bad Gateway",
        503 => "Service Unavailable",
        504 => "Gateway Timeout",
        _ => "Status"
    };
}
