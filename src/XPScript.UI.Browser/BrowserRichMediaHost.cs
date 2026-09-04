using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace XPScript.UI.Browser;

internal static partial class BrowserRichMediaHost
{
    internal static void Apply(JSObject root, string requestJson)
    {
        using var document = JsonDocument.Parse(requestJson);
        var request = document.RootElement;
        if (request.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in fields.EnumerateArray())
            {
                var type = Read(field, "type");
                if (type is not ("Image" or "WebView")) continue;
                var name = Read(field, "name");
                if (name.Length == 0) continue;
                using var existing = QuerySelector(root, "[data-field-name=\"" + name + "\"] input");
                if (existing is null) continue;

                if (type == "Image")
                {
                    using var image = CreateElement("img");
                    image.SetProperty("className", "img-fluid xpscript-uiform-image");
                    image.SetProperty("src", Read(field, "imageSource"));
                    image.SetProperty("alt", Read(field, "imageAltText"));
                    image.SetProperty("loading", "lazy");
                    ReplaceElement(existing, image);
                }
                else
                {
                    using var frame = CreateElement("iframe");
                    frame.SetProperty("className", "xpscript-uiform-webview w-100 border rounded");
                    frame.SetProperty("title", Read(field, "label", name));
                    frame.SetProperty("loading", "lazy");
                    frame.SetProperty("style", "min-height:320px");
                    var html = Read(field, "webViewHtml");
                    if (html.Length > 0) frame.SetProperty("srcdoc", html);
                    else frame.SetProperty("src", Read(field, "webViewSource", "about:blank"));
                    ReplaceElement(existing, frame);
                }
            }
        }

        if (request.TryGetProperty("showDefaultButtons", out var showButtons) && showButtons.ValueKind == JsonValueKind.False)
        {
            using var ok = QuerySelector(root, "button.btn-primary:not([data-action-name])");
            if (ok is not null) RemoveElement(ok);
            using var cancel = QuerySelector(root, "button.btn-secondary:not([data-action-name])");
            if (cancel is not null) RemoveElement(cancel);
        }
    }

    private static string Read(JsonElement root, string name, string fallback = "")
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    [JSImport("globalThis.document.createElement")]
    private static partial JSObject CreateElement(string tagName);

    [JSImport("globalThis.Element.prototype.querySelector.call")]
    private static partial JSObject? QuerySelector(JSObject element, string selector);

    [JSImport("globalThis.Element.prototype.replaceWith.call")]
    private static partial void ReplaceElement(JSObject existing, JSObject replacement);

    [JSImport("globalThis.Element.prototype.remove.call")]
    private static partial void RemoveElement(JSObject element);
}
