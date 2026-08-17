using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace XPScript.Web.Runtime;

public sealed record XpsCookieOptions(
    string Path = "/",
    bool HttpOnly = false,
    bool Secure = false,
    string SameSite = "Lax",
    TimeSpan? MaxAge = null,
    string? Domain = null,
    DateTimeOffset? Expires = null);

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
        var domain = NormalizeCookieDomain(options.Domain);
        var sameSite = NormalizeSameSite(options.SameSite);
        if (sameSite.Equals("None", StringComparison.OrdinalIgnoreCase) && !options.Secure)
            throw new ArgumentException("SameSite=None cookies must also use Secure.", nameof(options));

        var header = new StringBuilder()
            .Append(name).Append('=').Append(value)
            .Append("; Path=").Append(options.Path);
        if (domain is not null) header.Append("; Domain=").Append(domain);
        if (options.Expires is not null)
            header.Append("; Expires=").Append(options.Expires.Value.UtcDateTime.ToString("R", CultureInfo.InvariantCulture));
        if (options.MaxAge is not null)
        {
            var seconds = checked((long)Math.Floor(options.MaxAge.Value.TotalSeconds));
            header.Append("; Max-Age=").Append(seconds.ToString(CultureInfo.InvariantCulture));
        }
        header.Append("; SameSite=").Append(sameSite);
        if (options.HttpOnly) header.Append("; HttpOnly");
        if (options.Secure) header.Append("; Secure");

        RemoveEquivalentSetCookie(name, options.Path, domain);
        AppendHeader("Set-Cookie", header.ToString());
        EnsureCookieResponseNoStore();
    }

    public void DeleteCookie(
        string name,
        string path = "/",
        bool secure = false,
        string sameSite = "Lax",
        string? domain = null) =>
        SetCookie(
            name,
            string.Empty,
            new XpsCookieOptions(
                path,
                HttpOnly: false,
                Secure: secure,
                SameSite: sameSite,
                MaxAge: TimeSpan.Zero,
                Domain: domain,
                Expires: DateTimeOffset.UnixEpoch));

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

    public void SendFile(byte[] content, string fileName, string contentType = "application/octet-stream", bool inline = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        SendFile((ReadOnlyMemory<byte>)content, fileName, contentType, inline);
    }

    public void SendFile(ReadOnlyMemory<byte> content, string fileName, string contentType = "application/octet-stream", bool inline = false)
    {
        EnsureWritable();
        var safeName = NormalizeDownloadFileName(fileName);
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("Content type must not be empty.", nameof(contentType));
        ValidateHeaderValue(contentType);

        Clear();
        StatusCode = 200;
        ContentType = contentType;
        SetHeader("Content-Disposition", BuildContentDisposition(safeName, inline));
        WriteBinary(content.Span);
    }

    public void SendFile(XpsUploadedFile file, bool inline = false)
    {
        ArgumentNullException.ThrowIfNull(file);
        SendFile(file.Content, file.FileName, file.ContentType ?? "application/octet-stream", inline);
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
        if (_headers.ContainsKey("Set-Cookie")) EnsureCookieResponseNoStore();
        Completed = true;
    }

    private void RemoveEquivalentSetCookie(string name, string path, string? domain)
    {
        if (!_headers.TryGetValue("Set-Cookie", out var values)) return;
        var prefix = name + "=";
        values.RemoveAll(value =>
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal)) return false;
            var pathMatch = value.Contains("; Path=" + path, StringComparison.Ordinal);
            var domainMatch = domain is null
                ? !value.Contains("; Domain=", StringComparison.OrdinalIgnoreCase)
                : value.Contains("; Domain=" + domain, StringComparison.OrdinalIgnoreCase);
            return pathMatch && domainMatch;
        });
        if (values.Count == 0) _headers.Remove("Set-Cookie");
    }

    private static string NormalizeDownloadFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var normalized = fileName.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        if (slash >= 0) normalized = normalized[(slash + 1)..];
        if (normalized.Length is < 1 or > 255 || normalized.Any(char.IsControl))
            throw new ArgumentException("Download file name is invalid.", nameof(fileName));
        return normalized;
    }

    private static string BuildContentDisposition(string fileName, bool inline)
    {
        var ascii = new string(fileName.Select(c => c is >= ' ' and <= '~' && c is not '"' and not '\\' ? c : '_').ToArray());
        if (string.IsNullOrWhiteSpace(ascii)) ascii = "download";
        var encoded = Uri.EscapeDataString(fileName);
        return $"{(inline ? "inline" : "attachment")}; filename=\"{ascii}\"; filename*=UTF-8''{encoded}";
    }

    private void EnsureCookieResponseNoStore()
    {
        if (!_headers.TryGetValue("Cache-Control", out var values))
        {
            _headers["Cache-Control"] = ["no-store"];
            return;
        }

        if (values.Any(value => value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(directive => directive.Equals("no-store", StringComparison.OrdinalIgnoreCase))))
            return;

        values.Add("no-store");
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

    private static string? NormalizeCookieDomain(string? domain)
    {
        if (domain is null) return null;
        var value = domain.Trim();
        if (value.StartsWith(".", StringComparison.Ordinal)) value = value[1..];
        if (value.Length == 0 || value.Length > 253 || value.EndsWith(".", StringComparison.Ordinal) ||
            value.IndexOfAny(['\r', '\n', '\0', '/', '\\', ':', ';', ',']) >= 0 ||
            value.Any(char.IsWhiteSpace))
            throw new ArgumentException("Cookie Domain must be a DNS host name without a port, path or control characters.", nameof(domain));

        string ascii;
        try { ascii = new IdnMapping().GetAscii(value).ToLowerInvariant(); }
        catch (ArgumentException ex) { throw new ArgumentException("Cookie Domain is not a valid IDN/DNS name.", nameof(domain), ex); }
        if (Uri.CheckHostName(ascii) != UriHostNameType.Dns)
            throw new ArgumentException("Cookie Domain must be a DNS host name.", nameof(domain));
        return ascii;
    }

    private static string NormalizeSameSite(string value)
    {
        if (value.Equals("Strict", StringComparison.OrdinalIgnoreCase)) return "Strict";
        if (value.Equals("Lax", StringComparison.OrdinalIgnoreCase)) return "Lax";
        if (value.Equals("None", StringComparison.OrdinalIgnoreCase)) return "None";
        throw new ArgumentException("SameSite must be Strict, Lax or None.", nameof(value));
    }
}
