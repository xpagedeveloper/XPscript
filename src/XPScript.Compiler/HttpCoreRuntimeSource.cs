namespace XPScript.Compiler;

internal static class HttpCoreRuntimeSource
{
    public const string Code = """
internal static class XPScriptHttpCoreHelpers
{
    public static void SetBearerToken(object? clientValue, object? tokenValue)
    {
        var token = XPScriptRuntime.CStr(tokenValue);
        ValidateCredentialText(token, "Bearer token");
        Client(clientValue).SetHeader("Authorization", "Bearer " + token);
    }

    public static void SetBasicAuth(object? clientValue, object? usernameValue, object? passwordValue)
    {
        var username = XPScriptRuntime.CStr(usernameValue);
        var password = XPScriptRuntime.CStr(passwordValue);
        ValidateCredentialText(username, "Basic authentication username");
        ValidateCredentialText(password, "Basic authentication password");
        if (username.Contains(':'))
            throw new XPScriptRuntimeException(5, "Basic authentication username cannot contain a colon.");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
        Client(clientValue).SetHeader("Authorization", "Basic " + credentials);
    }

    public static string BasicAuthorization(object? usernameValue, object? passwordValue)
    {
        var username = XPScriptRuntime.CStr(usernameValue);
        var password = XPScriptRuntime.CStr(passwordValue);
        ValidateCredentialText(username, "Basic authentication username");
        ValidateCredentialText(password, "Basic authentication password");
        if (username.Contains(':'))
            throw new XPScriptRuntimeException(5, "Basic authentication username cannot contain a colon.");
        return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password));
    }

    public static string AddQuery(object? clientValue, object? urlValue, object? nameValue, object? value)
    {
        _ = Client(clientValue);
        var url = XPScriptRuntime.CStr(urlValue).Trim();
        var name = XPScriptRuntime.CStr(nameValue);
        if (name.Length == 0)
            throw new XPScriptRuntimeException(5, "HTTP query parameter name cannot be empty.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new XPScriptRuntimeException(5, "HTTP URL must be an absolute http:// or https:// URL.");

        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return url + separator + Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(XPScriptRuntime.CStr(value));
    }

    public static XPScriptJsonDocument ResponseJson(object? responseValue)
    {
        var response = Response(responseValue);
        return XPScriptNativeJson.Parse(response.Body);
    }

    private static void ValidateCredentialText(string value, string name)
    {
        if (value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new XPScriptRuntimeException(5, name + " contains a prohibited control character.");
    }

    private static XPScriptHttpClient Client(object? value)
        => value as XPScriptHttpClient ?? throw new XPScriptRuntimeException(13, "HTTP helper requires an HttpClient instance.");

    private static XPScriptHttpResponse Response(object? value)
        => value as XPScriptHttpResponse ?? throw new XPScriptRuntimeException(13, "JSON response helper requires an HttpResponse instance.");
}
""";
}
