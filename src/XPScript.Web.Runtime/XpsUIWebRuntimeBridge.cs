using System.Net;
using System.Text.RegularExpressions;

namespace XPScript.Web.Runtime;

public static class XpsUIWebRuntimeBridge
{
    private const string BootstrapCss = "<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css\" rel=\"stylesheet\" integrity=\"sha384-sRIl4kxILFvY47J16cr9ZwB07vP4J8+LH7qKQnuqkuIAvNWLzeN8tE5YBujZqJLB\" crossorigin=\"anonymous\">";
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XpsWebResponse, BootstrapState> BootstrapStates = new();
    private static readonly Regex PostForm = new("<form\\b(?=[^>]*\\bmethod\\s*=\\s*[\\\"']?post[\\\"']?)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

    public static string CsrfToken()
    {
        var context = XpsWebContextAccessor.Current;
        if (context.Session is null) return string.Empty;
        return new XpsWebServer(context.Server).CsrfToken();
    }

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

    public static string FileJson(string name, long maxFileBytes, bool multiple)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (maxFileBytes < 1 || maxFileBytes > 64L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(maxFileBytes));
        var files = XpsWebContextAccessor.Current.Request.Files(name, maxFileBytes: checked((int)maxFileBytes));
        if (files.Count == 0) return string.Empty;
        object Shape(XpsUploadedFile file) => new
        {
            fileName = file.FileName,
            contentType = file.ContentType,
            length = file.Length,
            base64 = Convert.ToBase64String(file.Bytes())
        };
        return multiple
            ? System.Text.Json.JsonSerializer.Serialize(files.Select(Shape).ToArray())
            : System.Text.Json.JsonSerializer.Serialize(Shape(files[0]));
    }

    public static void WriteHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var response = XpsWebContextAccessor.Current.Response;
        response.ContentType = "text/html; charset=utf-8";
        EnsureBootstrap(response);
        response.Write(InjectCsrfFields(html));
    }

    private static string InjectCsrfFields(string html)
    {
        if (html.Contains("name=\"" + XpsWebSecurity.CsrfFormFieldName + "\"", StringComparison.OrdinalIgnoreCase))
            return html;
        var token = CsrfToken();
        if (token.Length == 0) return html;
        var field = "<input type=\"hidden\" name=\"" + XpsWebSecurity.CsrfFormFieldName + "\" value=\"" + WebUtility.HtmlEncode(token) + "\">";
        return PostForm.Replace(html, match => match.Value + field);
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
