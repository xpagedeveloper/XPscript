using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace XPScript.UI.Browser;

[SupportedOSPlatform("browser")]
public static partial class BrowserFormHost
{
    private const string NavigationStateStorageKey = "xpscript.request-state.navigation";
    private const int NavigationStateLifetimeMilliseconds = 60_000;
    private const int MaxEventTokenLength = 260;
    private const int MaxEventPayloadLength = 1024 * 1024;
    private const int MaxDownloadBase64Length = 96 * 1024 * 1024;
    private static readonly object EventDispatcherSync = new();
    private static Func<string, string, string>? _eventDispatcher;
    private static Func<string, string, Task<string>>? _asyncEventDispatcher;
    private static int _toastSequence;

    public static void ShowToast(string requestJson)
    {
        var request = JsonSerializer.Deserialize<BrowserToastRequest>(requestJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new BrowserToastRequest();
        var position = NormalizeToastPosition(request.Position);
        var warning = request.Kind.Equals("warning", StringComparison.OrdinalIgnoreCase);
        var regionId = "xps-toast-" + position;
        using var existingRegion = GetElementById(regionId);
        var region = existingRegion ?? CreateElement("div");
        if (existingRegion is null)
        {
            region.SetProperty("id", regionId);
            region.SetProperty("className", "position-fixed p-3 d-flex flex-column xpscript-toast-region " + position);
            var placement = position switch
            {
                "top-left" => "top:0;left:0",
                "bottom-left" => "bottom:0;left:0;justify-content:flex-end",
                "bottom-right" => "bottom:0;right:0;justify-content:flex-end",
                _ => "top:0;right:0"
            };
            SetAttribute(region, "style", $"position:fixed;z-index:1090;max-width:min(26rem,calc(100vw - 2rem));padding:1rem;display:flex;gap:0;pointer-events:none;{placement}");
            SetAttribute(region, "role", "region");
            SetAttribute(region, "aria-label", position + " notifications");
            AppendToBody(region);
        }

        var toast = CreateElement("div");
        toast.SetProperty("className", string.IsNullOrWhiteSpace(request.Background) ? "toast show bg-body text-body border shadow" : "toast show text-body border shadow");
        SetAttribute(toast, "role", warning ? "alert" : "status");
        var sequence = Interlocked.Increment(ref _toastSequence);
        var overlap = position.StartsWith("bottom", StringComparison.Ordinal) ? "margin-top:-18px" : "margin-bottom:-18px";
        var toastStyle = $"pointer-events:auto;position:relative;z-index:{sequence};width:100%;{overlap}";
        if (!string.IsNullOrWhiteSpace(request.Background)) toastStyle += ";background-color:" + request.Background;
        SetAttribute(toast, "style", toastStyle);
        var header = CreateElement("div");
        header.SetProperty("className", "d-flex align-items-center gap-2 px-3 py-2");
        var title = CreateElement("strong");
        title.SetProperty("className", warning ? "me-auto text-danger fw-bold" : "me-auto");
        title.SetProperty("textContent", string.IsNullOrWhiteSpace(request.Title) ? warning ? "Warning" : "Message" : request.Title);
        var close = CreateElement("button");
        close.SetProperty("type", "button");
        close.SetProperty("className", "btn-close");
        SetAttribute(close, "aria-label", "Close notification");
        SetAttribute(close, "onclick", "this.closest('.xpscript-toast').remove()");
        AppendChild(header, title);
        AppendChild(header, close);
        var body = CreateElement("div");
        body.SetProperty("className", warning ? "toast-body text-danger" : "toast-body");
        body.SetProperty("textContent", request.Message);
        AppendChild(toast, header);
        AppendChild(toast, body);
        SetAttribute(toast, "class", (string.IsNullOrWhiteSpace(request.Background) ? "toast show bg-body text-body border shadow" : "toast show text-body border shadow") + " xpscript-toast");
        if (position.StartsWith("bottom", StringComparison.Ordinal)) AppendChild(region, toast);
        else PrependChild(region, toast);
        _ = ExpireToastAsync(toast, Math.Clamp(request.DurationSeconds, 1, 30));
    }

    private static async Task ExpireToastAsync(JSObject toast, int durationSeconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds)).ConfigureAwait(false);
        try { RemoveElement(toast); }
        catch { }
        finally { toast.Dispose(); }
    }

    private static string NormalizeToastPosition(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "top-left" or "topleft" => "top-left",
            "bottom-left" or "bottomleft" => "bottom-left",
            "bottom-right" or "bottomright" => "bottom-right",
            _ => "top-right"
        };

    private sealed class BrowserToastRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Kind { get; set; } = "normal";
        public string Position { get; set; } = "top-right";
        public int DurationSeconds { get; set; } = 5;
        public string Background { get; set; } = string.Empty;
    }

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

    public static void SetAsyncEventDispatcher(Func<string, string, Task<string>> dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        lock (EventDispatcherSync) _asyncEventDispatcher = dispatcher;
    }

    [JSExport]
    public static string DispatchEvent(string eventToken, string submittedValue)
    {
        ValidateEvent(eventToken, submittedValue);
        Func<string, string, string>? dispatcher;
        lock (EventDispatcherSync) dispatcher = _eventDispatcher;
        if (dispatcher is null)
            throw new InvalidOperationException("Browser UI event dispatcher is not registered.");
        return dispatcher(eventToken, submittedValue) ?? string.Empty;
    }

    [JSExport]
    public static async Task<string> DispatchEventAsync(string eventToken, string submittedValue)
    {
        ValidateEvent(eventToken, submittedValue);
        Func<string, string, Task<string>>? asyncDispatcher;
        Func<string, string, string>? dispatcher;
        lock (EventDispatcherSync)
        {
            asyncDispatcher = _asyncEventDispatcher;
            dispatcher = _eventDispatcher;
        }

        if (asyncDispatcher is not null)
            return await asyncDispatcher(eventToken, submittedValue).ConfigureAwait(false) ?? string.Empty;
        if (dispatcher is not null)
            return dispatcher(eventToken, submittedValue) ?? string.Empty;
        throw new InvalidOperationException("Browser UI event dispatcher is not registered.");
    }

    private static void ValidateEvent(string eventToken, string submittedValue)
    {
        if (string.IsNullOrWhiteSpace(eventToken) || eventToken.Length > MaxEventTokenLength)
            throw new ArgumentException("Browser UI event token is invalid.", nameof(eventToken));
        if (!(eventToken.StartsWith("change:", StringComparison.OrdinalIgnoreCase) ||
              eventToken.StartsWith("button:", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Browser UI event type is unsupported.", nameof(eventToken));
        if (submittedValue is null || submittedValue.Length > MaxEventPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(submittedValue), "Browser UI event payload exceeds the 1 MiB limit.");
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
            ClickElement(anchor);
        }
        finally
        {
            RemoveElement(anchor);
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

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? GetElementById(string id);

    [JSImport("globalThis.document.body.appendChild")]
    private static partial JSObject AppendToBody(JSObject element);

    [JSImport("globalThis.Element.prototype.setAttribute.call")]
    private static partial void SetAttribute(JSObject element, string name, string value);

    [JSImport("globalThis.Node.prototype.appendChild.call")]
    private static partial JSObject AppendChild(JSObject parent, JSObject child);

    [JSImport("globalThis.Element.prototype.prepend.call")]
    private static partial void PrependChild(JSObject parent, JSObject child);

    [JSImport("globalThis.HTMLElement.prototype.click.call")]
    private static partial void ClickElement(JSObject element);

    [JSImport("globalThis.Element.prototype.remove.call")]
    private static partial void RemoveElement(JSObject element);
}
