using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace XPScript.Web.Runtime;

public static class XpsNavigationStateHandoff
{
    public const string CookieName = "XPSNAV";
    private const int MaxPending = 10_000;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<string, PendingState> Pending = new(StringComparer.Ordinal);

    public static void StageCurrent(string target)
    {
        var context = XpsWebContextAccessor.Current;
        Stage(context.RequestScope, context.Request, context.Response, target);
    }

    public static void Stage(IXpsRequestState state, XpsWebRequest request, XpsWebResponse response, string target)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        var copy = CopyState(state);
        if (copy.Count == 0)
        {
            ClearCookie(request, response);
            return;
        }

        var targetPath = ResolveTargetPath(request, target);
        CleanupExpired();
        if (Pending.Count >= MaxPending)
            throw new InvalidOperationException("Request.State navigation handoff capacity has been reached.");

        var token = CreateToken();
        if (!Pending.TryAdd(token, new PendingState(copy, targetPath, DateTimeOffset.UtcNow + Lifetime)))
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
        if (!Pending.TryGetValue(token, out var pending)) return null;
        if (pending.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            Pending.TryRemove(token, out _);
            ClearCookie(request, response);
            return null;
        }

        if (!CanonicalPath(request.Path).Equals(pending.TargetPath, StringComparison.OrdinalIgnoreCase))
            return null;

        ClearCookie(request, response);
        if (!Pending.TryRemove(token, out var consumed) || consumed.ExpiresUtc <= DateTimeOffset.UtcNow)
            return null;
        return CopyState(consumed.State);
    }

    private static XpsRequestState CopyState(IXpsRequestState source)
    {
        var copy = new XpsRequestState();
        foreach (var key in source.Keys)
            copy.Set(key, source.Get(key));
        return copy;
    }

    private static string ResolveTargetPath(XpsWebRequest request, string target)
    {
        var normalized = target.Trim().Replace('\\', '/');
        var extension = Path.GetExtension(normalized);
        if (normalized.Length is < 1 or > 512 || normalized.StartsWith('/') || normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.Contains(':') || normalized.IndexOfAny(['\r', '\n', ';']) >= 0 ||
            (extension.Length > 0 && !extension.Equals(".xps", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Navigation target must be a relative local XPS module path with an optional .xps extension.", nameof(target));

        var source = request.Path.Replace('\\', '/');
        if (!source.StartsWith('/')) source = "/" + source;
        var slash = source.LastIndexOf('/');
        var directory = slash < 0 ? "/" : source[..(slash + 1)];
        return CanonicalPath(directory + normalized);
    }

    private static string CanonicalPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith('/')) normalized = "/" + normalized;
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return "/";
        var last = segments[^1];
        if (last.EndsWith(".xps", StringComparison.OrdinalIgnoreCase))
            segments[^1] = last[..^4];
        return "/" + string.Join('/', segments);
    }

    private static string CookiePath(XpsWebRequest request)
    {
        // Kestrel stores ASP.NET PathBase in PathInfo. FastCGI/CGI store the
        // actual PATH_INFO there, so those hosts must use the SCRIPT_NAME
        // directory to make the one-hop cookie available to sibling scripts.
        var scriptName = request.Cgi("SCRIPT_NAME");
        if (!string.IsNullOrWhiteSpace(scriptName))
        {
            var normalizedScript = scriptName.Replace('\\', '/');
            if (normalizedScript.IndexOfAny(['\r', '\n', ';']) >= 0) return "/";
            if (!normalizedScript.StartsWith('/')) normalizedScript = "/" + normalizedScript;
            var slash = normalizedScript.LastIndexOf('/');
            return slash <= 0 ? "/" : normalizedScript[..(slash + 1)];
        }

        var pathBase = request.PathInfo.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(pathBase) || pathBase == "/" || !pathBase.StartsWith('/')) return "/";
        if (pathBase.IndexOfAny(['\r', '\n', ';']) >= 0) return "/";
        return pathBase.EndsWith('/') ? pathBase : pathBase + "/";
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

    private sealed record PendingState(XpsRequestState State, string TargetPath, DateTimeOffset ExpiresUtc);
}
