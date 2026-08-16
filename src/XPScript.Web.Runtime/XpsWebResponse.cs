using System.Collections.ObjectModel;
using System.Text;

namespace XPScript.Web.Runtime;

public sealed class XpsWebResponse
{
    private readonly MemoryStream _body = new();
    private readonly Dictionary<string, List<string>> _headers = new(StringComparer.OrdinalIgnoreCase);

    public int StatusCode { get; set; } = 200;
    public string? ContentType { get; set; } = "text/html; charset=utf-8";
    public bool Completed { get; private set; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers =>
        new ReadOnlyDictionary<string, IReadOnlyList<string>>(
            _headers.ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)Array.AsReadOnly(x.Value.ToArray()),
                StringComparer.OrdinalIgnoreCase));

    public ReadOnlyMemory<byte> Body => _body.ToArray();

    public void SetHeader(string name, string value)
    {
        EnsureWritable();
        ValidateHeaderName(name);
        ValidateHeaderValue(value);
        if (IsTransportOwnedHeader(name))
            throw new InvalidOperationException($"Header '{name}' is owned by the transport and cannot be set directly.");
        _headers[name] = [value];
    }

    public void AppendHeader(string name, string value)
    {
        EnsureWritable();
        ValidateHeaderName(name);
        ValidateHeaderValue(value);
        if (IsTransportOwnedHeader(name))
            throw new InvalidOperationException($"Header '{name}' is owned by the transport and cannot be set directly.");
        if (!_headers.TryGetValue(name, out var values)) _headers[name] = values = [];
        values.Add(value);
    }

    public void RemoveHeader(string name)
    {
        EnsureWritable();
        _headers.Remove(name);
    }

    public void Write(object? value)
    {
        EnsureWritable();
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        var bytes = Encoding.UTF8.GetBytes(text);
        _body.Write(bytes);
    }

    public void WriteBinary(ReadOnlySpan<byte> value)
    {
        EnsureWritable();
        _body.Write(value);
    }

    public void Clear()
    {
        EnsureWritable();
        _body.SetLength(0);
        _headers.Clear();
        StatusCode = 200;
        ContentType = "text/html; charset=utf-8";
    }

    public void Redirect(string url, int statusCode = 302)
    {
        EnsureWritable();
        if (statusCode is not (301 or 302 or 303 or 307 or 308))
            throw new ArgumentOutOfRangeException(nameof(statusCode), "Redirect status must be 301, 302, 303, 307 or 308.");
        ValidateHeaderValue(url);
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("Redirect URL must not be empty.", nameof(url));
        StatusCode = statusCode;
        SetHeader("Location", url);
    }

    public void Complete()
    {
        if (StatusCode is < 100 or > 599) throw new InvalidOperationException("Response status code must be between 100 and 599.");
        if (ContentType is not null) ValidateHeaderValue(ContentType);
        Completed = true;
    }

    private void EnsureWritable()
    {
        if (Completed) throw new InvalidOperationException("Response is already completed.");
    }

    private static bool IsTransportOwnedHeader(string name) =>
        name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);

    public static void ValidateHeaderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Header name must not be empty.", nameof(name));
        foreach (var c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~'))
                throw new ArgumentException("Header name contains an invalid HTTP token character.", nameof(name));
        }
    }

    public static void ValidateHeaderValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException("Header value contains a prohibited control character.", nameof(value));
    }
}
