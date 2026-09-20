using System.Security.Cryptography;
using System.Text;

namespace XPScript.Web.Runtime;

public static class XpsWebClientCorrelation
{
    public const string CookieName = "XPSLOGID";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static string GetOrCreate(IReadOnlyDictionary<string, string> cookies, out bool created)
    {
        ArgumentNullException.ThrowIfNull(cookies);
        if (cookies.TryGetValue(CookieName, out var existing) && IsValid(existing))
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

    public static void SetCookie(XpsWebResponse response, string value, bool secure)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (!IsValid(value)) throw new ArgumentException("Client correlation id is invalid.", nameof(value));
        response.SetCookie(CookieName, value, new XpsCookieOptions(
            Path: "/",
            HttpOnly: true,
            Secure: secure,
            SameSite: "Lax",
            MaxAge: Lifetime));
    }

    public static bool IsValid(string? value) =>
        value is { Length: 32 } && value.All(char.IsAsciiHexDigit);
}
