using System.Collections.ObjectModel;

namespace XPScript.Web.Runtime;

public sealed class XpsWebRequest
{
    public XpsWebRequest(
        string method,
        string path,
        string pathInfo,
        string queryString,
        IReadOnlyDictionary<string, IReadOnlyList<string>> headers,
        string? contentType,
        long? contentLength,
        ReadOnlyMemory<byte> body,
        string host,
        string scheme,
        string? remoteAddress,
        string protocol,
        IReadOnlyDictionary<string, string> cookies,
        CancellationToken cancellationToken = default)
    {
        Method = NormalizeMethod(method);
        Path = path ?? throw new ArgumentNullException(nameof(path));
        PathInfo = pathInfo ?? string.Empty;
        QueryString = queryString ?? string.Empty;
        Headers = FreezeMultiValue(headers);
        ContentType = contentType;
        ContentLength = contentLength;
        Body = body;
        Host = host ?? string.Empty;
        Scheme = scheme ?? string.Empty;
        RemoteAddress = remoteAddress;
        Protocol = protocol ?? string.Empty;
        Cookies = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(cookies ?? throw new ArgumentNullException(nameof(cookies)), StringComparer.OrdinalIgnoreCase));
        CancellationToken = cancellationToken;
    }

    public string Method { get; }
    public string Path { get; }
    public string PathInfo { get; }
    public string QueryString { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }
    public string? ContentType { get; }
    public long? ContentLength { get; }
    public ReadOnlyMemory<byte> Body { get; }
    public string Host { get; }
    public string Scheme { get; }
    public string? RemoteAddress { get; }
    public string Protocol { get; }
    public IReadOnlyDictionary<string, string> Cookies { get; }
    public CancellationToken CancellationToken { get; }

    private static string NormalizeMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentException("HTTP method is required.", nameof(method));
        var normalized = method.Trim().ToUpperInvariant();
        foreach (var c in normalized)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~'))
                throw new ArgumentException("HTTP method contains an invalid token character.", nameof(method));
        }
        return normalized;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> FreezeMultiValue(
        IReadOnlyDictionary<string, IReadOnlyList<string>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copy = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key)) throw new ArgumentException("Header name must not be empty.", nameof(values));
            copy[pair.Key] = Array.AsReadOnly((pair.Value ?? []).ToArray());
        }
        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(copy);
    }
}
