namespace XPScript.Web.Runtime;

public static class XpsUIWebRuntimeBridge
{
    private const string BootstrapCss = "<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css\" rel=\"stylesheet\" integrity=\"sha384-sRIl4kxILFvY47J16cr9ZwB07vP4J8+LH7qKQnuqkuIAvNWLzeN8tE5YBujZqJLB\" crossorigin=\"anonymous\">";
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XpsWebResponse, BootstrapState> BootstrapStates = new();

    private sealed class BootstrapState
    {
        public bool Written;
    }

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

    public static string[] FormValues(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return XpsWebContextAccessor.Current.Request.Form(name).ToArray();
    }

    public static void WriteHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var response = XpsWebContextAccessor.Current.Response;
        response.ContentType = "text/html; charset=utf-8";
        EnsureBootstrap(response);
        response.Write(html);
    }

    private static void EnsureBootstrap(XpsWebResponse response)
    {
        var state = BootstrapStates.GetOrCreateValue(response);
        lock (state)
        {
            if (state.Written) return;
            response.Write(BootstrapCss);
            state.Written = true;
        }
    }
}
