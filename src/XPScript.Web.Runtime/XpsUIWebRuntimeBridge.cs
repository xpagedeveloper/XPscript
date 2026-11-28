using System.Net;
using System.Text.RegularExpressions;

namespace XPScript.Web.Runtime;

public static class XpsUIWebRuntimeBridge
{
    private const string BootstrapCss = "<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css\" rel=\"stylesheet\" integrity=\"sha384-sRIl4kxILFvY47J16cr9ZwB07vP4J8+LH7qKQnuqkuIAvNWLzeN8tE5YBujZqJLB\" crossorigin=\"anonymous\">";
    private const string BootstrapJs = "<script src=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/js/bootstrap.bundle.min.js\" crossorigin=\"anonymous\"></script>";
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
        return XpsWebContextAccessor.Current.Request.FormAll(name).ToArray();
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
        response.Write(InjectCsrfFields(ApplyBootstrapMarkup(html)));
    }

    public static void ShowToast(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        var request = System.Text.Json.JsonSerializer.Deserialize<ToastRequest>(requestJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) ?? new ToastRequest();
        var position = NormalizeToastPosition(request.Position);
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            message = request.Message,
            title = string.IsNullOrWhiteSpace(request.Title)
                ? request.Kind.Equals("warning", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Message"
                : request.Title,
            warning = request.Kind.Equals("warning", StringComparison.OrdinalIgnoreCase),
            position,
            background = request.Background,
            duration = Math.Clamp(request.DurationSeconds, 1, 30)
        });
        var response = XpsWebContextAccessor.Current.Response;
        response.ContentType = "text/html; charset=utf-8";
        EnsureBootstrap(response);
response.Write("""
<style id="xpscript-toast-style">.xps-toast-region{position:fixed;z-index:1090;display:flex;gap:0;max-width:min(26rem,calc(100vw - 2rem));pointer-events:none}.xps-toast-region.top-left{top:1rem;left:1rem;flex-direction:column}.xps-toast-region.top-right{top:1rem;right:1rem;flex-direction:column}.xps-toast-region.bottom-left{bottom:1rem;left:1rem;flex-direction:column;justify-content:flex-end}.xps-toast-region.bottom-right{bottom:1rem;right:1rem;flex-direction:column;justify-content:flex-end}.xps-toast-region .toast{pointer-events:auto;width:100%}.xps-toast-header{display:flex;align-items:center;gap:.75rem;padding:.5rem .75rem}.xps-toast-header strong{flex:1}</style><script>(()=>{const config=__TOAST_CONFIG__;const present=()=>{let region=document.getElementById('xps-toast-'+config.position);if(!region){region=document.createElement('div');region.id='xps-toast-'+config.position;region.className='xps-toast-region '+config.position;region.setAttribute('role','region');region.setAttribute('aria-label',config.position+' notifications');document.body.appendChild(region);}const toast=document.createElement('div');toast.style.position='relative';toast.style[config.position.startsWith('bottom')?'marginTop':'marginBottom']='-18px';toast.style.zIndex=String(window.xpsToastSequence=(window.xpsToastSequence||0)+1);toast.className=config.background?'toast show text-body border shadow':'toast show bg-body text-body border shadow';if(config.background)toast.style.backgroundColor=config.background;toast.setAttribute('role',config.warning?'alert':'status');const header=document.createElement('div');header.className='xps-toast-header';const title=document.createElement('strong');title.textContent=config.title;if(config.warning)title.className='text-danger fw-bold';const close=document.createElement('button');close.type='button';close.className='btn-close';close.setAttribute('aria-label','Close notification');close.addEventListener('click',()=>toast.remove());header.append(title,close);const body=document.createElement('div');body.className='toast-body';body.textContent=config.message;if(config.warning)body.classList.add('text-danger');toast.append(header,body);if(config.position.startsWith('bottom'))region.appendChild(toast);else region.prepend(toast);window.setTimeout(()=>toast.remove(),config.duration*1000);};if(document.body)present();else document.addEventListener('DOMContentLoaded',present,{once:true});})();</script>
""".Replace("__TOAST_CONFIG__", payload, StringComparison.Ordinal));
    }

    public static void WriteJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        var response = XpsWebContextAccessor.Current.Response;
        response.ContentType = "application/json; charset=utf-8";
        response.Write(json);
    }

    private static string ApplyBootstrapMarkup(string html)
    {
        // Server-rendered UIForms are plain HTML at this point. Apply the same
        // Bootstrap classes used by the browser renderer directly to the
        // response so styling is present even when generated source shape
        // differs between compiler/runtime versions.
        return html
            .Replace("class=\"xpscript-uiform\"", "class=\"xpscript-uiform container-fluid py-3\"", StringComparison.Ordinal)
            .Replace("class=\"xpscript-uiform-field\"", "class=\"xpscript-uiform-field mb-3\"", StringComparison.Ordinal)
            .Replace("<label for=\"xps_", "<label class=\"form-label\" for=\"xps_", StringComparison.Ordinal)
            .Replace("<textarea id=\"xps_", "<textarea class=\"form-control\" id=\"xps_", StringComparison.Ordinal)
            .Replace("<select id=\"xps_", "<select class=\"form-select\" id=\"xps_", StringComparison.Ordinal)
            .Replace("<select multiple", "<select class=\"form-select\" multiple", StringComparison.Ordinal)
            .Replace("<input type=\"text\"", "<input class=\"form-control\" type=\"text\"", StringComparison.Ordinal)
            .Replace("<input list=\"", "<input class=\"form-control\" list=\"", StringComparison.Ordinal)
            .Replace("<input type=\"email\"", "<input class=\"form-control\" type=\"email\"", StringComparison.Ordinal)
            .Replace("<input type=\"password\"", "<input class=\"form-control\" type=\"password\"", StringComparison.Ordinal)
            .Replace("<input type=\"number\"", "<input class=\"form-control\" type=\"number\"", StringComparison.Ordinal)
            .Replace("<input type=\"date\"", "<input class=\"form-control\" type=\"date\"", StringComparison.Ordinal)
            .Replace("<input type=\"time\"", "<input class=\"form-control\" type=\"time\"", StringComparison.Ordinal)
            .Replace("<input type=\"checkbox\"", "<input class=\"form-check-input ms-2 me-2\" type=\"checkbox\"", StringComparison.Ordinal)
            .Replace("<label><input type=\"radio\"", "<label class=\"form-check form-check-inline px-2\"><input class=\"form-check-input me-2\" type=\"radio\"", StringComparison.Ordinal)
            .Replace("<button type=\"submit\"", "<button class=\"btn btn-primary\" type=\"submit\"", StringComparison.Ordinal);
    }

    private sealed class ToastRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Kind { get; set; } = "normal";
        public string Position { get; set; } = "top-right";
        public int DurationSeconds { get; set; } = 3;
        public string Background { get; set; } = string.Empty;
    }

    private static string NormalizeToastPosition(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "top-left" or "topleft" => "top-left",
            "bottom-left" or "bottomleft" => "bottom-left",
            "bottom-right" or "bottomright" => "bottom-right",
            _ => "top-right"
        };

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
            response.Write(BootstrapJs);
            state.Written = true;
        }
    }
}
