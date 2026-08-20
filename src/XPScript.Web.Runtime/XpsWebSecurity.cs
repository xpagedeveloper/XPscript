namespace XPScript.Web.Runtime;

public static class XpsWebSecurity
{
    public const string CsrfHeaderName = "X-XPS-CSRF-Token";
    public const string CsrfFormFieldName = "__xps_csrf";

    public static bool IsUnsafeMethod(string method) =>
        method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("PATCH", StringComparison.OrdinalIgnoreCase) ||
        method.Equals("DELETE", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresCsrfProtection(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!IsUnsafeMethod(context.Request.Method) || context.Session is null) return false;

        var sessionCookie = context.Request.Cookie("XPSID");
        var authorization = context.Request.HeaderFirst("Authorization");
        var bearerOnly = string.IsNullOrWhiteSpace(sessionCookie) &&
                         authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        if (bearerOnly) return false;

        var contentType = context.Request.ContentType ?? string.Empty;
        var browserForm = contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) ||
                          contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase);

        return !string.IsNullOrWhiteSpace(sessionCookie) ||
               browserForm ||
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

    public static void WriteCsrfFailure(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.Clear();
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        context.Response.SetHeader("Cache-Control", "no-store");
        context.Response.Write("{\"type\":\"about:blank\",\"title\":\"Forbidden\",\"status\":403,\"detail\":\"CSRF token is missing or invalid.\"}");
        context.Response.Complete();
    }
}
