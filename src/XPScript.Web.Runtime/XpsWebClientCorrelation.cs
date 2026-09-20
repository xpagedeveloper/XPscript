using System.Security.Cryptography;
using System.Text;

namespace XPScript.Web.Runtime;

public static class XpsWebClientCorrelation
{
    public const string CookieName = "XPSLOGID";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static string CookieNameFor(string siteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(siteId.Trim().ToLowerInvariant()));
        return CookieName + "_" + Convert.ToHexString(digest.AsSpan(0, 6)).ToLowerInvariant();
    }

    public static string GetOrCreate(IReadOnlyDictionary<string, string> cookies, out bool created) =>
        GetOrCreate(cookies, CookieName, out created);

    public static string GetOrCreate(IReadOnlyDictionary<string, string> cookies, string cookieName, out bool created)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieName);
        if (cookies.TryGetValue(cookieName, out var existing) && IsValid(existing))
        {
            created = false;
            return existing.ToLowerInvariant();
        }

        created = true;
        return Guid.NewGuid().ToString("N");
    }

    public static string Hash(string value)
    {
        if (!IsValid(value)) throw new ArgumentException("Client correlation id is invalid.", nameof(value));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static void SetCookie(XpsWebResponse response, string value, bool secure) =>
        SetCookie(response, CookieName, value, secure);

    public static void SetCookie(XpsWebResponse response, string cookieName, string value, bool secure)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(cookieName);
        if (!IsValid(value)) throw new ArgumentException("Client correlation id is invalid.", nameof(value));
        response.SetCookie(cookieName, value, new XpsCookieOptions(
            Path: "/",
            HttpOnly: true,
            Secure: secure,
            SameSite: "Lax",
            MaxAge: Lifetime));
    }

    public static bool IsValid(string? value) =>
        value is { Length: 32 } && value.All(char.IsAsciiHexDigit);
}
