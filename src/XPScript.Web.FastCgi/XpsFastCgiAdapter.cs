using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using XPScript.Web.Runtime;

namespace XPScript.Web.FastCgi;

public sealed class XpsFastCgiAdapter : IAsyncDisposable
{
    private readonly XpsFastCgiOptions _options;
    private readonly XpsServerInfo _serverInfo;
    private readonly IXpsWebRequestHandler _handler;
    private readonly IXpsApplicationState _application;
    private readonly XpsSessionStore? _sessions;
    private readonly Func<XpsWebRequest, XpsWebPrincipal>? _principalFactory;
    private readonly SemaphoreSlim _connections;
    private TcpListener? _listener;
    private CancellationTokenSource? _shutdown;
    private Task? _acceptLoop;

    public XpsFastCgiAdapter(
        XpsFastCgiOptions options,
        XpsServerInfo serverInfo,
        IXpsWebRequestHandler handler,
        IXpsApplicationState? application = null,
        XpsSessionStore? sessions = null,
        Func<XpsWebRequest, XpsWebPrincipal>? principalFactory = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _application = application ?? new XpsApplicationState();
        _sessions = sessions;
        _principalFactory = principalFactory;
        _connections = new SemaphoreSlim(_options.MaxConcurrentConnections, _options.MaxConcurrentConnections);
    }

    public IPEndPoint? LocalEndpoint => _listener?.LocalEndpoint as IPEndPoint;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_listener is not null) throw new InvalidOperationException("FastCGI listener is already running.");
        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new TcpListener(_options.Address, _options.Port);
        _listener.Start(_options.MaxConcurrentConnections);
        _acceptLoop = AcceptLoopAsync(_shutdown.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_listener is null || _shutdown is null) return;
        var shutdown = _shutdown;
        shutdown.Cancel();
        _listener.Stop();
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) when (shutdown.IsCancellationRequested) { }
        }
        _listener = null;
        _acceptLoop = null;
        _shutdown = null;
        shutdown.Dispose();
    }

    public async Task ProcessConnectionAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ushort requestId = 0;
        var keepConnection = false;
        var paramsComplete = false;
        var stdinComplete = false;
        using var paramStream = new MemoryStream();
        using var stdin = new MemoryStream();
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

        while (!cancellationToken.IsCancellationRequested)
        {
            var record = await XpsFastCgiProtocol.ReadRecordAsync(stream, cancellationToken).ConfigureAwait(false);
            if (record is null) return;
            var value = record.Value;

            if (value.RequestId == 0)
            {
                await HandleManagementRecordAsync(stream, value, cancellationToken).ConfigureAwait(false);
                continue;
            }

            switch (value.Type)
            {
                case XpsFastCgiRecordType.BeginRequest:
                    if (requestId != 0)
                    {
                        await XpsFastCgiProtocol.WriteEndRequestAsync(stream, value.RequestId, 0, XpsFastCgiProtocol.CantMultiplexConnection, cancellationToken).ConfigureAwait(false);
                        continue;
                    }
                    ParseBeginRequest(value, out keepConnection);
                    requestId = value.RequestId;
                    break;

                case XpsFastCgiRecordType.Params:
                    if (requestId == 0 || value.RequestId != requestId || paramsComplete)
                        throw new XpsFastCgiProtocolException("Unexpected FastCGI PARAMS record ordering.");
                    if (value.Content.Length == 0)
                    {
                        var paramBytes = paramStream.ToArray();
                        var totalBytes = 0;
                        var totalCount = 0;
                        XpsFastCgiProtocol.ParseParams(paramBytes, parameters, _options, ref totalBytes, ref totalCount);
                        paramsComplete = true;
                    }
                    else
                    {
                        if (paramStream.Length + value.Content.Length > _options.MaxParamsBytes)
                            throw new XpsFastCgiProtocolException("FastCGI PARAMS exceed the configured size limit.");
                        await paramStream.WriteAsync(value.Content, cancellationToken).ConfigureAwait(false);
                    }
                    break;

                case XpsFastCgiRecordType.Stdin:
                    if (requestId == 0 || value.RequestId != requestId || !paramsComplete || stdinComplete)
                        throw new XpsFastCgiProtocolException("Unexpected FastCGI STDIN record ordering.");
                    if (value.Content.Length == 0)
                    {
                        stdinComplete = true;
                        await ExecuteRequestAsync(stream, requestId, parameters, stdin.ToArray(), cancellationToken).ConfigureAwait(false);
                        await XpsFastCgiProtocol.WriteEndRequestAsync(stream, requestId, 0, XpsFastCgiProtocol.RequestComplete, cancellationToken).ConfigureAwait(false);
                        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        if (!keepConnection) return;
                        requestId = 0;
                        keepConnection = false;
                        paramsComplete = false;
                        stdinComplete = false;
                        parameters.Clear();
                        paramStream.SetLength(0);
                        stdin.SetLength(0);
                    }
                    else
                    {
                        if (stdin.Length + value.Content.Length > _options.MaxRequestBodyBytes)
                            throw new XpsFastCgiProtocolException("FastCGI request body exceeds the configured limit.");
                        await stdin.WriteAsync(value.Content, cancellationToken).ConfigureAwait(false);
                    }
                    break;

                case XpsFastCgiRecordType.AbortRequest:
                    if (value.RequestId != requestId) throw new XpsFastCgiProtocolException("FastCGI ABORT_REQUEST does not match the active request.");
                    await XpsFastCgiProtocol.WriteEndRequestAsync(stream, requestId, 0, XpsFastCgiProtocol.RequestComplete, cancellationToken).ConfigureAwait(false);
                    return;

                default:
                    throw new XpsFastCgiProtocolException("Unsupported FastCGI application record type.");
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        var listener = _listener ?? throw new InvalidOperationException("FastCGI listener is not initialized.");
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { break; }

            try
            {
                await _connections.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                client.Dispose();
                throw;
            }
            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using (client)
            using (var stream = client.GetStream())
                await ProcessConnectionAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (XpsFastCgiProtocolException) { }
        catch (IOException) { }
        catch (SocketException) { }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { _connections.Release(); }
    }

    private async Task ExecuteRequestAsync(Stream stream, ushort requestId, IReadOnlyDictionary<string, string> parameters, byte[] body, CancellationToken cancellationToken)
    {
        var request = CreateRequest(parameters, body, cancellationToken);
        var response = new XpsWebResponse();
        var principal = _principalFactory?.Invoke(request) ?? new XpsWebPrincipal(false);
        var session = _sessions?.Bind(request, response);
        var context = new XpsWebContext(request, response, _serverInfo, principal, _application, session);
        using (XpsWebContextAccessor.Push(context))
            await _handler.HandleAsync(context).ConfigureAwait(false);
        if (!response.Completed) response.Complete();
        await XpsFastCgiProtocol.WriteStreamAsync(stream, XpsFastCgiRecordType.Stdout, requestId, BuildResponseBytes(response), cancellationToken).ConfigureAwait(false);
    }

    private XpsWebRequest CreateRequest(IReadOnlyDictionary<string, string> parameters, byte[] body, CancellationToken cancellationToken)
    {
        var method = Required(parameters, "REQUEST_METHOD");
        var scriptName = parameters.TryGetValue("SCRIPT_NAME", out var script) ? script : "/";
        var pathInfo = parameters.TryGetValue("PATH_INFO", out var info) ? info : string.Empty;
        var query = parameters.TryGetValue("QUERY_STRING", out var queryValue) ? queryValue : string.Empty;
        var contentType = parameters.TryGetValue("CONTENT_TYPE", out var ct) && ct.Length > 0 ? ct : null;
        long? contentLength = null;
        if (parameters.TryGetValue("CONTENT_LENGTH", out var rawLength) && rawLength.Length > 0)
        {
            if (!long.TryParse(rawLength, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedLength) || parsedLength < 0)
                throw new XpsFastCgiProtocolException("Invalid FastCGI CONTENT_LENGTH.");
            if (parsedLength != body.Length) throw new XpsFastCgiProtocolException("FastCGI CONTENT_LENGTH does not match STDIN length.");
            contentLength = parsedLength;
        }
        else if (body.Length > 0) contentLength = body.Length;

        ValidateScriptFilename(parameters);
        var headers = ExtractHeaders(parameters);
        var cookies = ParseCookies(headers);
        var host = headers.TryGetValue("Host", out var hostValues) && hostValues.Count > 0
            ? hostValues[0]
            : parameters.TryGetValue("SERVER_NAME", out var serverName) ? serverName : string.Empty;
        var scheme = IsHttps(parameters) ? "https" : "http";
        var protocol = parameters.TryGetValue("SERVER_PROTOCOL", out var p) ? p : "HTTP/1.1";
        var remoteAddress = parameters.TryGetValue("REMOTE_ADDR", out var remote) ? remote : null;

        return new XpsWebRequest(method, NormalizeRequestPath(scriptName, pathInfo), pathInfo, query, headers, contentType, contentLength, body, host, scheme, remoteAddress, protocol, cookies, cancellationToken);
    }

    private void ValidateScriptFilename(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("SCRIPT_FILENAME", out var value) || string.IsNullOrWhiteSpace(value)) return;
        string full;
        try { full = Path.GetFullPath(value); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { throw new XpsFastCgiProtocolException("Invalid SCRIPT_FILENAME.", ex); }

        var root = Path.GetFullPath(_serverInfo.RootPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.Equals(root, comparison) && !full.StartsWith(prefix, comparison))
            throw new XpsFastCgiProtocolException("SCRIPT_FILENAME escapes the configured site root.");

        try
        {
            var relative = Path.GetRelativePath(root, full);
            _ = new XpsWebServer(_serverInfo).MapPath(relative);
        }
        catch (XpsWebPathException ex)
        {
            throw new XpsFastCgiProtocolException("SCRIPT_FILENAME resolves outside the configured site root.", ex);
        }
    }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> ExtractHeaders(IReadOnlyDictionary<string, string> parameters)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in parameters)
        {
            string? name = pair.Key.StartsWith("HTTP_", StringComparison.Ordinal) ? pair.Key[5..].Replace('_', '-') :
                pair.Key.Equals("CONTENT_TYPE", StringComparison.Ordinal) ? "Content-Type" :
                pair.Key.Equals("CONTENT_LENGTH", StringComparison.Ordinal) ? "Content-Length" : null;
            if (name is null) continue;
            if (Encoding.UTF8.GetByteCount(pair.Value) > _options.MaxHeaderValueBytes) throw new XpsFastCgiProtocolException("FastCGI HTTP header value exceeds the configured limit.");
            XpsWebResponse.ValidateHeaderName(name);
            XpsWebResponse.ValidateHeaderValue(pair.Value);
            if (result.Count >= _options.MaxHeaderCount && !result.ContainsKey(name)) throw new XpsFastCgiProtocolException("FastCGI HTTP header count exceeds the configured limit.");
            result[name] = Array.AsReadOnly(new[] { pair.Value });
        }
        return result;
    }

    private static IReadOnlyDictionary<string, string> ParseCookies(IReadOnlyDictionary<string, IReadOnlyList<string>> headers)
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

    private static string NormalizeRequestPath(string scriptName, string pathInfo)
    {
        var path = string.IsNullOrWhiteSpace(scriptName) ? "/" : scriptName;
        if (!path.StartsWith("/", StringComparison.Ordinal)) path = "/" + path;
        if (!string.IsNullOrEmpty(pathInfo) && pathInfo != "/" && !path.EndsWith(pathInfo, StringComparison.Ordinal))
            path += pathInfo.StartsWith("/", StringComparison.Ordinal) ? pathInfo : "/" + pathInfo;
        return path;
    }

    private static string Required(IReadOnlyDictionary<string, string> parameters, string name) =>
        parameters.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new XpsFastCgiProtocolException($"Required FastCGI parameter {name} is missing.");

    private static bool IsHttps(IReadOnlyDictionary<string, string> parameters) =>
        parameters.TryGetValue("HTTPS", out var value) && (value.Equals("on", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.Ordinal));

    private static byte[] BuildResponseBytes(XpsWebResponse response)
    {
        var builder = new StringBuilder().Append("Status: ").Append(response.StatusCode.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        if (!string.IsNullOrWhiteSpace(response.ContentType)) builder.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
        foreach (var header in response.Headers)
            foreach (var value in header.Value) builder.Append(header.Key).Append(": ").Append(value).Append("\r\n");
        builder.Append("\r\n");
        var headerBytes = Encoding.UTF8.GetBytes(builder.ToString());
        var output = new byte[checked(headerBytes.Length + response.Body.Length)];
        headerBytes.CopyTo(output, 0);
        response.Body.Span.CopyTo(output.AsSpan(headerBytes.Length));
        return output;
    }

    private static void ParseBeginRequest(XpsFastCgiProtocol.Record record, out bool keepConnection)
    {
        if (record.Content.Length != 8) throw new XpsFastCgiProtocolException("FastCGI BEGIN_REQUEST body must be 8 bytes.");
        var role = (ushort)((record.Content[0] << 8) | record.Content[1]);
        if (role != XpsFastCgiProtocol.ResponderRole) throw new XpsFastCgiProtocolException("Only the FastCGI responder role is supported.");
        for (var i = 3; i < record.Content.Length; i++) if (record.Content[i] != 0) throw new XpsFastCgiProtocolException("FastCGI BEGIN_REQUEST reserved bytes must be zero.");
        keepConnection = (record.Content[2] & XpsFastCgiProtocol.KeepConnectionFlag) != 0;
        if ((record.Content[2] & ~XpsFastCgiProtocol.KeepConnectionFlag) != 0) throw new XpsFastCgiProtocolException("FastCGI BEGIN_REQUEST contains unsupported flags.");
    }

    private static async Task HandleManagementRecordAsync(Stream stream, XpsFastCgiProtocol.Record record, CancellationToken cancellationToken)
    {
        if (record.Type == XpsFastCgiRecordType.GetValues)
        {
            await XpsFastCgiProtocol.WriteRecordAsync(stream, XpsFastCgiRecordType.GetValuesResult, 0, ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            return;
        }
        var body = new byte[8];
        body[0] = (byte)record.Type;
        await XpsFastCgiProtocol.WriteRecordAsync(stream, XpsFastCgiRecordType.UnknownType, 0, body, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _connections.Dispose();
    }
}
