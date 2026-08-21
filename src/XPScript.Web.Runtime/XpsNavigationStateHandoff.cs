using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace XPScript.Web.Runtime;

public static class XpsNavigationStateHandoff
{
    public const string CookieName = "XPSNAV";
    private const int MaxPending = 10_000;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<string, PendingState> Pending = new(StringComparer.Ordinal);

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

    public static void ConsumeInto(IXpsRequestState target, XpsWebRequest request, XpsWebResponse response)
    {
        ArgumentNullException.ThrowIfNull(target);
        var inherited = TryConsume(request, response);
        if (inherited is null) return;
        target.Clear();
        foreach (var key in inherited.Keys)
            target.Set(key, inherited.Get(key));
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
