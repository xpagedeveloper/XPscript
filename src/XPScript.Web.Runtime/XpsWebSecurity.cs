namespace XPScript.Web.Runtime;

public static class XpsWebSecurity
{
    public const string CsrfHeaderName = "X-XPS-CSRF-Token";
    public const string CsrfFormFieldName = "__xps_csrf";

    private const string DefaultCsp = "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'self'; form-action 'self'; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdn.tiny.cloud; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdn.tiny.cloud; img-src 'self' data: blob: https:; font-src 'self' data: https:; connect-src 'self' https:";

    public static bool IsUnsafeMethod(string method) =>
        method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("DELETE", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresCsrfProtection(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsUnsafeMethod(context.Request.Method) || context.Session is null) return false;

        var hasCookies = context.Request.Cookies.Count > 0;
        var authorization = context.Request.HeaderFirst("Authorization");
        var bearerOnly = !hasCookies && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        if (bearerOnly) return false;

        var contentType = context.Request.ContentType ?? string.Empty;
        var browserForm = contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) ||
                          contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase);
        var browserOrigin = !string.IsNullOrWhiteSpace(context.Request.HeaderFirst("Origin")) ||
                            !string.IsNullOrWhiteSpace(context.Request.HeaderFirst("Referer"));

        return hasCookies ||
               browserForm ||
               browserOrigin ||
               !string.IsNullOrWhiteSpace(context.Request.HeaderFirst(CsrfHeaderName));
    }

    public static bool ValidateCsrf(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!RequiresCsrfProtection(context)) return true;

        var token = context.Request.HeaderFirst(CsrfHeaderName);
        if (string.IsNullOrWhiteSpace(token))
            token = context.Request.FormFirst(CsrfFormFieldName);
        if (string.IsNullOrWhiteSpace(token)) return false;

        using var scope = XpsWebContextAccessor.Push(context);
        return new XpsWebServer(context.Server).ValidateCsrfToken(token);
    }

    public static string IssueCsrfToken(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Session is null) return string.Empty;
        using var scope = XpsWebContextAccessor.Push(context);
        return new XpsWebServer(context.Server).CsrfToken();
    }

    public static void ApplyResponseSecurityHeaders(XpsWebResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        SetIfMissing(response, "X-Content-Type-Options", "nosniff");
        SetIfMissing(response, "Referrer-Policy", "strict-origin-when-cross-origin");
        SetIfMissing(response, "X-Frame-Options", "SAMEORIGIN");
        SetIfMissing(response, "Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        if (response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            SetIfMissing(response, "Content-Security-Policy", DefaultCsp);
    }

    public static void WriteCsrfFailure(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        context.Response.SetHeader("Cache-Control", "no-store");
        var token = IssueCsrfToken(context);
        if (token.Length > 0) context.Response.SetHeader(CsrfHeaderName, token);
        ApplyResponseSecurityHeaders(context.Response);
        context.Response.Write("{\"type\":\"about:blank\",\"title\":\"Forbidden\",\"status\":403,\"detail\":\"CSRF token is missing or invalid.\"}");
        context.Response.Complete();
    }

    private static void SetIfMissing(XpsWebResponse response, string name, string value)
    {
        if (!response.Headers.ContainsKey(name)) response.SetHeader(name, value);
    }
}
