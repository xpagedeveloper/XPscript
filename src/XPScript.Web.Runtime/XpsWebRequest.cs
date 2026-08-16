using System.Collections.ObjectModel;
using System.Text;

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
    public bool IsCancellationRequested => CancellationToken.IsCancellationRequested;

    public IReadOnlyList<string> Query(string name, int maxQueryChars = 16_384, int maxFields = 256) =>
        GetValues(ParseUrlEncoded(QueryString, maxQueryChars, maxFields, "query string"), name);

    public IReadOnlyList<string> Header(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Headers.TryGetValue(name, out var values) ? values : Array.Empty<string>();
    }

    public string? Cookie(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Cookies.TryGetValue(name, out var value) ? value : null;
    }

    public string BodyText(int maxBytes = 1_048_576)
    {
        if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (Body.Length > maxBytes) throw new InvalidOperationException($"Request body exceeds the configured {maxBytes} byte text limit.");
        return new UTF8Encoding(false, true).GetString(Body.Span);
    }

    public byte[] BodyBytes(int maxBytes = 1_048_576)
    {
        if (maxBytes < 0) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (Body.Length > maxBytes) throw new InvalidOperationException($"Request body exceeds the configured {maxBytes} byte binary limit.");
        return Body.ToArray();
    }

    public IReadOnlyList<string> Form(string name, int maxBytes = 1_048_576, int maxFields = 256)
    {
        if (ContentType is null || !ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();
        var text = BodyText(maxBytes);
        return GetValues(ParseUrlEncoded(text, maxBytes, maxFields, "form body"), name);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseUrlEncoded(
        string raw,
        int maxChars,
        int maxFields,
        string displayName)
    {
        if (maxChars < 0) throw new ArgumentOutOfRangeException(nameof(maxChars));
        if (maxFields is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(maxFields));
        var value = raw.StartsWith('?', StringComparison.Ordinal) ? raw[1..] : raw;
        if (value.Length > maxChars) throw new InvalidOperationException($"Request {displayName} exceeds the configured {maxChars} character limit.");

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (value.Length == 0) return new ReadOnlyDictionary<string, IReadOnlyList<string>>(new Dictionary<string, IReadOnlyList<string>>());

        var fields = value.Split('&');
        if (fields.Length > maxFields) throw new InvalidOperationException($"Request {displayName} exceeds the configured {maxFields} field limit.");
        foreach (var field in fields)
        {
            var separator = field.IndexOf('=');
            var rawName = separator >= 0 ? field[..separator] : field;
            var rawValue = separator >= 0 ? field[(separator + 1)..] : string.Empty;
            var name = DecodeUrlComponent(rawName);
            var decodedValue = DecodeUrlComponent(rawValue);
            if (!result.TryGetValue(name, out var values)) result[name] = values = [];
            values.Add(decodedValue);
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            result.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()), StringComparer.OrdinalIgnoreCase));
    }

    private static string DecodeUrlComponent(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
        catch (UriFormatException ex)
        {
            throw new InvalidOperationException("Request contains malformed URL encoding.", ex);
        }
    }

    private static IReadOnlyList<string> GetValues(IReadOnlyDictionary<string, IReadOnlyList<string>> values, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return values.TryGetValue(name, out var result) ? result : Array.Empty<string>();
    }

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
