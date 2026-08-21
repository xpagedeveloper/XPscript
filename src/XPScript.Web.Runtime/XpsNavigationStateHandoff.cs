using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace XPScript.Web.Runtime;

public static class XpsNavigationStateHandoff
{
    public const string CookieName = "XPSNAV";
    public const string EndpointName = ".xpscript-navigation-state";
    private const int MaxPending = 10_000;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<string, PendingState> Pending = new(StringComparer.Ordinal);

    public static bool IsStageEndpoint(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = path.Replace('\\', '/').TrimEnd('/');
        return normalized.Equals("/" + EndpointName, StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("/" + EndpointName, StringComparison.OrdinalIgnoreCase);
    }

    public static void StageCurrent()
    {
        var context = XpsWebContextAccessor.Current;
        Stage(context.RequestScope, context.Request, context.Response);
    }

    public static void Stage(IXpsRequestState state, XpsWebRequest request, XpsWebResponse response)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        var copy = CopyState(state);
        if (copy.Count == 0)
        {
            ClearCookie(request, response);
            return;
        }

        CleanupExpired();
        if (Pending.Count >= MaxPending)
            throw new InvalidOperationException("Request.State navigation handoff capacity has been reached.");

        var token = CreateToken();
        if (!Pending.TryAdd(token, new PendingState(copy, DateTimeOffset.UtcNow + Lifetime)))
            throw new InvalidOperationException("Unable to allocate Request.State navigation handoff token.");

        response.SetCookie(
            CookieName,
            token,
            new XpsCookieOptions(
                Path: CookiePath(request),
                HttpOnly: true,
                Secure: request.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase),
                SameSite: "Lax",
                MaxAge: Lifetime));
    }

    public static void StageJson(XpsWebRequest request, XpsWebResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        if (!request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Request.State navigation handoff requires POST.");
        if (request.Body.Length > 1024 * 1024)
            throw new InvalidOperationException("Request.State navigation handoff body exceeds 1 MiB.");

        using var document = JsonDocument.Parse(request.Body);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Request.State navigation handoff body must be a JSON object.");

        var state = new XpsRequestState();
        var count = 0;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (++count > 128)
                throw new InvalidOperationException("Request.State navigation handoff exceeds 128 entries.");
            state.Set(property.Name, DecodeValue(property.Value));
        }
        Stage(state, request, response);
    }

    public static IXpsRequestState? TryConsume(XpsWebRequest request, XpsWebResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        CleanupExpired();

        var token = request.Cookie(CookieName);
        if (string.IsNullOrWhiteSpace(token) || !IsValidToken(token)) return null;
        ClearCookie(request, response);
        if (!Pending.TryRemove(token, out var pending) || pending.ExpiresUtc <= DateTimeOffset.UtcNow)
            return null;
        return CopyState(pending.State);
    }

    private static XpsRequestState CopyState(IXpsRequestState source)
    {
        var copy = new XpsRequestState();
        foreach (var key in source.Keys)
            copy.Set(key, source.Get(key));
        return copy;
    }

    private static object? DecodeValue(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("type", out var typeElement) ||
            !element.TryGetProperty("value", out var valueElement))
            throw new InvalidOperationException("Request.State navigation handoff contains an invalid typed value.");

        var type = typeElement.GetString() ?? string.Empty;
        return type switch
        {
            "null" => null,
            "string" => valueElement.GetString() ?? string.Empty,
            "bool" => valueElement.GetBoolean(),
            "byte" => valueElement.GetByte(),
            "sbyte" => checked((sbyte)valueElement.GetInt32()),
            "short" => valueElement.GetInt16(),
            "ushort" => valueElement.GetUInt16(),
            "int" => valueElement.GetInt32(),
            "uint" => valueElement.GetUInt32(),
            "long" => valueElement.GetInt64(),
            "ulong" => valueElement.GetUInt64(),
            "float" => valueElement.GetSingle(),
            "double" => valueElement.GetDouble(),
            "decimal" => valueElement.GetDecimal(),
            "char" => DecodeChar(valueElement),
            "datetime" => DateTime.Parse(valueElement.GetString() ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "datetimeoffset" => DateTimeOffset.Parse(valueElement.GetString() ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "guid" => Guid.Parse(valueElement.GetString() ?? string.Empty),
            "bytes" => Convert.FromBase64String(valueElement.GetString() ?? string.Empty),
            _ => throw new InvalidOperationException("Request.State navigation handoff contains an unsupported value type.")
        };
    }

    private static char DecodeChar(JsonElement element)
    {
        var value = element.GetString() ?? string.Empty;
        if (value.Length != 1) throw new InvalidOperationException("Request.State navigation handoff char value is invalid.");
        return value[0];
    }

    private static string CookiePath(XpsWebRequest request)
    {
        var path = request.PathInfo;
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/')) return "/";
        return path.EndsWith('/') ? path : path + "/";
    }

    private static void ClearCookie(XpsWebRequest request, XpsWebResponse response) =>
        response.DeleteCookie(
            CookieName,
            path: CookiePath(request),
            secure: request.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase),
            sameSite: "Lax");

    private static void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in Pending)
            if (pair.Value.ExpiresUtc <= now)
                Pending.TryRemove(pair.Key, out _);
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool IsValidToken(string token)
    {
        if (token.Length is < 40 or > 64) return false;
        foreach (var c in token)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return false;
        return true;
    }

    private sealed record PendingState(XpsRequestState State, DateTimeOffset ExpiresUtc);
}
