using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

namespace XPScript.UI.Browser;

[SupportedOSPlatform("browser")]
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
                if (type is not ("Image" or "WebView" or "Carousel" or "ProgressBar" or "ActivityIndicator" or "Icon" or "CameraPreview" or "FileField" or "LookupField" or "AutoCompleteField")) continue;
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
                else if (type == "Carousel")
                {
                    using var carousel = CreateCarousel(field, name);
                    ReplaceElement(existing, carousel);
                }
                else if (type is "ProgressBar" or "ActivityIndicator")
                {
                    using var progress = CreateProgress(field, name, type == "ActivityIndicator");
                    ReplaceElement(existing, progress);
                }
                else if (type == "Icon")
                {
                    using var icon = CreateElement("span");
                    icon.SetProperty("className", "xpscript-uiform-icon");
                    icon.SetProperty("textContent", Read(field, "icon", Read(field, "label", name)));
                    ReplaceElement(existing, icon);
                }
                else if (type == "CameraPreview")
                {
                    using var camera = CreateElement("input");
                    camera.SetProperty("type", "file");
                    camera.SetProperty("accept", "image/*");
                    camera.SetProperty("capture", "environment");
                    camera.SetProperty("className", "form-control xpscript-uiform-camera-upload");
                    ReplaceElement(existing, camera);
                }
                else if (type == "FileField")
                {
                    using var file = CreateElement("input");
                    file.SetProperty("type", "file");
                    file.SetProperty("className", "form-control xpscript-uiform-file-upload");
                    ReplaceElement(existing, file);
                }
                else if (type is "LookupField" or "AutoCompleteField")
                {
                    using var lookup = CreateElement("input");
                    lookup.SetProperty("type", "text");
                    lookup.SetProperty("className", "form-control");
                    var listId = "xps_" + name + "_options";
                    lookup.SetProperty("list", listId);
                    using var list = CreateElement("datalist");
                    list.SetProperty("id", listId);
                    if (field.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var option in options.EnumerateArray())
                        {
                            using var item = CreateElement("option");
                            item.SetProperty("value", option.GetString() ?? string.Empty);
                            AppendChild(list, item);
                        }
                    }
                    using var wrapper = CreateElement("div");
                    AppendChild(wrapper, lookup);
                    AppendChild(wrapper, list);
                    ReplaceElement(existing, wrapper);
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

    private static JSObject CreateProgress(JsonElement field, string name, bool activity)
    {
        using var progress = CreateElement("progress");
        progress.SetProperty("className", "xpscript-uiform-progress");
        progress.SetProperty("data-field-name", name);
        progress.SetProperty("max", "1");
        if (!(activity || (field.TryGetProperty("progressIndeterminate", out var indeterminate) && indeterminate.ValueKind == JsonValueKind.True)))
        {
            var value = field.TryGetProperty("progressValue", out var progressValue) && progressValue.TryGetDouble(out var number) ? Math.Clamp(number, 0, 1) : 0;
            progress.SetProperty("value", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        if (activity && field.TryGetProperty("activityRunning", out var running) && running.ValueKind == JsonValueKind.False)
            progress.SetProperty("hidden", true);
        return progress;
    }

    private static string Read(JsonElement root, string name, string fallback = "")
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static JSObject CreateCarousel(JsonElement field, string name)
    {
        var root = CreateElement("div");
        root.SetProperty("className", "xpscript-uiform-carousel");
        root.SetProperty("data-field-name", name);
        var sources = field.TryGetProperty("carouselSources", out var sourceValues) && sourceValues.ValueKind == JsonValueKind.Array
            ? sourceValues.EnumerateArray().Where(value => value.ValueKind == JsonValueKind.String).Select(value => value.GetString() ?? string.Empty).ToArray()
            : Array.Empty<string>();
        var index = field.TryGetProperty("carouselIndex", out var indexValue) && indexValue.TryGetInt32(out var requestedIndex)
            ? Math.Clamp(requestedIndex, 0, Math.Max(0, sources.Length - 1))
            : 0;
        var loop = field.TryGetProperty("carouselLoop", out var loopValue) && loopValue.ValueKind == JsonValueKind.True;
        foreach (var (source, sourceIndex) in sources.Select((value, position) => (value, position)))
        {
            using var image = CreateElement("img");
            image.SetProperty("className", "xps-carousel-slide");
            image.SetProperty("src", source);
            image.SetProperty("alt", Read(field, "label", name));
            image.SetProperty("style", sourceIndex == index ? "display:block;max-width:100%;max-height:320px;object-fit:contain" : "display:none;max-width:100%;max-height:320px;object-fit:contain");
            AppendChild(root, image);
        }
        using var previous = CreateElement("button");
        previous.SetProperty("type", "button");
        previous.SetProperty("className", "xps-carousel-prev");
        previous.SetProperty("textContent", "‹");
        previous.SetProperty("ariaLabel", "Previous");
        previous.SetProperty("onclick", CarouselButtonScript(-1, loop));
        AppendChild(root, previous);
        using var next = CreateElement("button");
        next.SetProperty("type", "button");
        next.SetProperty("className", "xps-carousel-next");
        next.SetProperty("textContent", "›");
        next.SetProperty("ariaLabel", "Next");
        next.SetProperty("onclick", CarouselButtonScript(1, loop));
        AppendChild(root, next);
        if (field.TryGetProperty("carouselAutoAdvance", out var intervalValue) && intervalValue.TryGetInt32(out var interval) && interval > 0)
            root.SetProperty("data-auto-advance", interval.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return root;
    }

    private static string CarouselButtonScript(int delta, bool loop)
    {
        var direction = delta < 0 ? "-1" : "1";
        var wrap = loop ? "true" : "false";
        return "const a=[...this.parentElement.querySelectorAll('.xps-carousel-slide')],i=a.findIndex(x=>x.style.display!=='none');if(!a.length)return;let n=i+" + direction + ";if(n<0)n=" + wrap + "?a.length-1:0;if(n>=a.length)n=" + wrap + "?0:a.length-1;a[i].style.display='none';a[n].style.display='block';";
    }

    [JSImport("globalThis.document.createElement")]
    private static partial JSObject CreateElement(string tagName);

    [JSImport("globalThis.Element.prototype.querySelector.call")]
    private static partial JSObject? QuerySelector(JSObject element, string selector);

    [JSImport("globalThis.Element.prototype.replaceWith.call")]
    private static partial void ReplaceElement(JSObject existing, JSObject replacement);

    [JSImport("globalThis.Node.prototype.appendChild.call")]
    private static partial JSObject AppendChild(JSObject parent, JSObject child);

    [JSImport("globalThis.Element.prototype.remove.call")]
    private static partial void RemoveElement(JSObject element);
}
