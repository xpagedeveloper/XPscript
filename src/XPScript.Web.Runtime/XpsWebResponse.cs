using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace XPScript.Web.Runtime;

public sealed record XpsCookieOptions(
    string Path = "/",
    bool HttpOnly = false,
    bool Secure = false,
    string SameSite = "Lax",
    TimeSpan? MaxAge = null);

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

    public void SetCookie(string name, string value, XpsCookieOptions? options = null)
    {
        EnsureWritable();
        ValidateCookieName(name);
        ValidateCookieValue(value);
        options ??= new XpsCookieOptions();
        ValidateCookiePath(options.Path);
        var sameSite = NormalizeSameSite(options.SameSite);
        if (sameSite.Equals("None", StringComparison.OrdinalIgnoreCase) && !options.Secure)
            throw new ArgumentException("SameSite=None cookies must also use Secure.", nameof(options));

        var header = new StringBuilder()
            .Append(name).Append('=').Append(value)
            .Append("; Path=").Append(options.Path)
            .Append("; SameSite=").Append(sameSite);
        if (options.HttpOnly) header.Append("; HttpOnly");
        if (options.Secure) header.Append("; Secure");
        if (options.MaxAge is not null)
        {
            var seconds = checked((long)Math.Floor(options.MaxAge.Value.TotalSeconds));
            header.Append("; Max-Age=").Append(seconds.ToString(CultureInfo.InvariantCulture));
        }
        AppendHeader("Set-Cookie", header.ToString());
    }

    public void DeleteCookie(string name, string path = "/", bool secure = false, string sameSite = "Lax") =>
        SetCookie(name, string.Empty, new XpsCookieOptions(path, HttpOnly: false, Secure: secure, SameSite: sameSite, MaxAge: TimeSpan.Zero));

    public void Write(object? value)
    {
        EnsureWritable();
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
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

    private static void ValidateCookieName(string name)
    {
        ValidateHeaderName(name);
        if (name.StartsWith("$", StringComparison.Ordinal))
            throw new ArgumentException("Cookie name must not start with '$'.", nameof(name));
    }

    private static void ValidateCookieValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        foreach (var c in value)
        {
            if (c <= 0x20 || c >= 0x7f || c is '"' or ',' or ';' or '\\')
                throw new ArgumentException("Cookie value contains a prohibited character. Encode the value before setting the cookie.", nameof(value));
        }
    }

    private static void ValidateCookiePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("Cookie path must be an absolute HTTP path.", nameof(path));
        if (path.IndexOfAny(['\r', '\n', '\0', ';']) >= 0)
            throw new ArgumentException("Cookie path contains a prohibited character.", nameof(path));
    }

    private static string NormalizeSameSite(string value)
    {
        if (value.Equals("Strict", StringComparison.OrdinalIgnoreCase)) return "Strict";
        if (value.Equals("Lax", StringComparison.OrdinalIgnoreCase)) return "Lax";
        if (value.Equals("None", StringComparison.OrdinalIgnoreCase)) return "None";
        throw new ArgumentException("SameSite must be Strict, Lax or None.", nameof(value));
    }
}
