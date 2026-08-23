using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XPScript.UI.Browser;

public static partial class BrowserFormHost
{
    private const string NavigationStateStorageKey = "xpscript.request-state.navigation";
    private const int NavigationStateLifetimeMilliseconds = 60_000;
    private const int MaxEventTokenLength = 260;
    private const int MaxEventPayloadLength = 1024 * 1024;
    private const int MaxDownloadBase64Length = 96 * 1024 * 1024;
    private static readonly object EventDispatcherSync = new();
    private static Func<string, string, string>? _eventDispatcher;

    public static string ShowDialog(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        var normalized = NormalizeStructuralElements(requestJson);
        ApplyApplicationMetadata(normalized);
        return RenderForm(normalized);
    }

    public static void SetEventDispatcher(Func<string, string, string> dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        lock (EventDispatcherSync) _eventDispatcher = dispatcher;
    }

    [JSExport]
    public static string DispatchEvent(string eventToken, string submittedValue)
    {
        if (string.IsNullOrWhiteSpace(eventToken) || eventToken.Length > MaxEventTokenLength)
            throw new ArgumentException("Browser UI event token is invalid.", nameof(eventToken));
        if (!(eventToken.StartsWith("change:", StringComparison.OrdinalIgnoreCase) ||
              eventToken.StartsWith("button:", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Browser UI event type is unsupported.", nameof(eventToken));
        if (submittedValue is null || submittedValue.Length > MaxEventPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(submittedValue), "Browser UI event payload exceeds the 1 MiB limit.");

        Func<string, string, string>? dispatcher;
        lock (EventDispatcherSync) dispatcher = _eventDispatcher;
        if (dispatcher is null)
            throw new InvalidOperationException("Browser UI event dispatcher is not registered.");
        return dispatcher(eventToken, submittedValue) ?? string.Empty;
    }

    public static void DownloadFile(string base64, string fileName, string contentType)
    {
        ArgumentNullException.ThrowIfNull(base64);
        if (base64.Length == 0 || base64.Length > MaxDownloadBase64Length)
            throw new ArgumentOutOfRangeException(nameof(base64), "Browser download payload exceeds the supported attachment limit.");

        var safeName = NormalizeDownloadFileName(fileName);
        var safeType = NormalizeContentType(contentType);
        var href = "data:" + safeType + ";base64," + base64;

        using var anchor = CreateElement("a");
        anchor.SetProperty("href", href);
        anchor.SetProperty("download", safeName);
        anchor.SetProperty("rel", "noopener");
        AppendToBody(anchor);
        try
        {
            Click(anchor);
        }
        finally
        {
            Remove(anchor);
        }
    }

    public static void StageRequestState(string stateJson)
    {
        ArgumentNullException.ThrowIfNull(stateJson);
        if (stateJson.Length > 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(stateJson), "Browser Request.State navigation payload exceeds 1 MiB.");

        StageRequestStateInBrowser(NavigationStateStorageKey, stateJson);
    }

    public static string ConsumeRequestState() =>
        ConsumeRequestStateInBrowser(NavigationStateStorageKey, NavigationStateLifetimeMilliseconds) ?? string.Empty;

    public static void Navigate(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new ArgumentException("Browser navigation target is required.", nameof(target));

        var path = target.Trim().Replace('\\', '/');
        var extension = Path.GetExtension(path);
        if (path.Length > 512 || path.StartsWith('/') || path.Contains("..", StringComparison.Ordinal) ||
            Uri.TryCreate(path, UriKind.Absolute, out _) ||
            (extension.Length > 0 && !extension.Equals(".xps", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Browser navigation target must be a relative local XPS module path with an optional .xps extension.", nameof(target));

        NavigateInBrowser(path, NavigationStateStorageKey);
    }

    private static void ApplyApplicationMetadata(string requestJson)
    {
        var root = JsonNode.Parse(requestJson)?.AsObject();
        if (root is null) return;

        var title = root["applicationTitle"]?.GetValue<string>() ?? string.Empty;
        var icon = root["applicationIcon"]?.GetValue<string>() ?? string.Empty;
        ApplyApplicationMetadataInBrowser(title, icon);
    }

    private static string NormalizeStructuralElements(string requestJson)
    {
        var root = JsonNode.Parse(requestJson)?.AsObject()
            ?? throw new InvalidOperationException("Browser UIForm request is empty.");
        if (root["fields"] is not JsonArray fields) return requestJson;

        foreach (var node in fields)
        {
            if (node is not JsonObject field) continue;
            var type = field["type"]?.GetValue<string>();
            if (string.Equals(type, "Separator", StringComparison.Ordinal))
            {
                field["type"] = "RadioGroup";
                field["label"] = "────────────────────────────────────────";
                field["required"] = false;
                field["readOnly"] = true;
                field["options"] = new JsonArray();
            }
            else if (string.Equals(type, "Spacer", StringComparison.Ordinal))
            {
                field["type"] = "RadioGroup";
                field["label"] = string.Empty;
                field["required"] = false;
                field["readOnly"] = true;
                field["options"] = new JsonArray();
            }
        }

        return root.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static string NormalizeDownloadFileName(string? fileName)
    {
        var name = (fileName ?? string.Empty).Trim().Replace('\\', '/');
        var slash = name.LastIndexOf('/');
        if (slash >= 0) name = name[(slash + 1)..];
        if (name.Length is < 1 or > 255 || name.Any(char.IsControl))
            throw new ArgumentException("Browser download file name is invalid.", nameof(fileName));
        return name;
    }

    private static string NormalizeContentType(string? contentType)
    {
        var value = (contentType ?? string.Empty).Trim();
        if (value.Length == 0) return "application/octet-stream";
        if (value.Length > 255 || value.IndexOfAny(['\r', '\n', '\0']) >= 0)
            throw new ArgumentException("Browser download content type is invalid.", nameof(contentType));
        return value;
    }

    [JSImport("renderForm", "xpscript-browser")]
    private static partial string RenderForm(string requestJson);

    [JSImport("stageRequestState", "xpscript-browser")]
    private static partial void StageRequestStateInBrowser(string key, string stateJson);

    [JSImport("consumeRequestState", "xpscript-browser")]
    private static partial string? ConsumeRequestStateInBrowser(string key, int lifetimeMilliseconds);

    [JSImport("navigate", "xpscript-browser")]
    private static partial void NavigateInBrowser(string target, string key);

    [JSImport("applyApplicationMetadata", "xpscript-browser")]
    private static partial void ApplyApplicationMetadataInBrowser(string title, string icon);

    [JSImport("globalThis.document.createElement")]
    private static partial JSObject CreateElement(string tagName);

    [JSImport("globalThis.document.body.appendChild")]
    private static partial JSObject AppendToBody(JSObject element);

    [JSImport("globalThis.HTMLElement.prototype.click.call")]
    private static partial void Click(JSObject element);

    [JSImport("globalThis.Element.prototype.remove.call")]
    private static partial void Remove(JSObject element);
}
