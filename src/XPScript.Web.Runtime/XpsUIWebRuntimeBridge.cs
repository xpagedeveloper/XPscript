using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Text;

namespace XPScript.Web.Runtime;

public static class XpsUIWebRuntimeBridge
{
    private const string BootstrapCss = "<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/css/bootstrap.min.css\" rel=\"stylesheet\" integrity=\"sha384-sRIl4kxILFvY47J16cr9ZwB07vP4J8+LH7qKQnuqkuIAvNWLzeN8tE5YBujZqJLB\" crossorigin=\"anonymous\">";
    private const string BootstrapJs = "<script src=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.8/dist/js/bootstrap.bundle.min.js\" crossorigin=\"anonymous\"></script>";
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<XpsWebResponse, BootstrapState> BootstrapStates = new();
    private static readonly Regex AssetSource = new("(?<prefix>\\bsrc\\s*=\\s*[\\\"'])(?<path>assets/[^\\\"'#?]+)(?<suffix>[\\\"'])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly byte[] AssetSigningKey = RandomNumberGenerator.GetBytes(32);
    public const string AssetRoute = "/__xps/uiform-asset";
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
        response.Write(ProtectAssetSources(InjectCsrfFields(html)));
    }


    private static string ProtectAssetSources(string html)
    {
        var context = XpsWebContextAccessor.Current;
        if (context.Session is null) return html;
        return AssetSource.Replace(html, match =>
        {
            var path = match.Groups["path"].Value.Replace('\\', '/');
            var expires = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
            var expiry = expires.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var payload = context.Session.Id + "\n" + expiry + "\n" + path;
            var signature = Convert.ToHexString(HMACSHA256.HashData(AssetSigningKey, Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            var url = AssetRoute + "?path=" + Uri.EscapeDataString(path) + "&expires=" + expiry + "&token=" + signature;
            return match.Groups["prefix"].Value + WebUtility.HtmlEncode(url) + match.Groups["suffix"].Value;
        });
    }

    public static bool TryResolveAsset(string path, long expires, string token, string sessionId, string rootPath, out string fullPath)
    {
        fullPath = string.Empty;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (expires < now || expires > now + 900 || string.IsNullOrWhiteSpace(sessionId) || token?.Length != 64) return false;
        var normalized = (path ?? string.Empty).Replace('\\', '/');
        if (!normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)) return false;
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments.Any(x => x is "." or ".." || x.StartsWith(".", StringComparison.Ordinal))) return false;
        var expiry = expires.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var payload = sessionId + "\n" + expiry + "\n" + normalized;
        var expected = HMACSHA256.HashData(AssetSigningKey, Encoding.UTF8.GetBytes(payload));
        byte[] supplied;
        try { supplied = Convert.FromHexString(token); } catch (FormatException) { return false; }
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied)) return false;

        var root = Path.GetFullPath(rootPath);
        var relativeAsset = normalized.Replace('/', Path.DirectorySeparatorChar);
        var privateRoot = Path.GetFullPath(Path.Combine(root, ".xpscript-private", "assets"));
        var publicRoot = Path.GetFullPath(Path.Combine(root, "assets"));
        var candidate = Path.GetFullPath(Path.Combine(Directory.Exists(privateRoot) ? privateRoot : publicRoot, Path.GetRelativePath("assets", relativeAsset)));
        var allowedRoot = Directory.Exists(privateRoot) ? privateRoot : publicRoot;
        var relative = Path.GetRelativePath(allowedRoot, candidate);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(candidate)) return false;
        fullPath = candidate;
        return true;
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
            response.Write(BootstrapJs);
            state.Written = true;
        }
    }
}
