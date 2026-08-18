namespace XPScript.Web.Runtime;

public static class XpsUIWebRuntimeBridge
{
    public static bool IsAvailable()
    {
        try
        {
            _ = XpsWebContextAccessor.Current;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static string Method() => XpsWebContextAccessor.Current.Request.Method;

    public static string FormFirst(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return XpsWebContextAccessor.Current.Request.FormFirst(name);
    }

    public static void WriteHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var response = XpsWebContextAccessor.Current.Response;
        response.ContentType = "text/html; charset=utf-8";
        response.Write(html);
    }
}
