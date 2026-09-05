using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;

namespace XPScript.UI.Browser;

public static partial class BrowserFormLifecycleHost
{
    public static string ShowDialog(string requestJson) => Show(requestJson, null);
    public static string ShowDialog(string requestJson, Func<string, string, string>? eventCallback) => Show(requestJson, eventCallback);
    public static string Show(string requestJson) => Show(requestJson, null);

    public static string Show(string requestJson, Func<string, string, string>? eventCallback)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        using var document = JsonDocument.Parse(requestJson);
        var root = document.RootElement;
        var instanceId = root.TryGetProperty("instanceId", out var id) ? id.GetString() ?? string.Empty : string.Empty;
        var modal = root.TryGetProperty("modal", out var modalElement) && modalElement.ValueKind == JsonValueKind.True;
        if (instanceId.Length == 0) throw new ArgumentException("UIForm instance id is required.", nameof(requestJson));
        if (IsVisible(instanceId)) return "{\"result\":\"Pending\",\"values\":{}}";

        if (eventCallback is not null) BrowserFormHost.SetEventDispatcher(eventCallback);
        using var baseRoot = GetElementById("xpscript-app") ?? throw new InvalidOperationException("XPScript browser root element was not found.");
        baseRoot.SetProperty("id", "xpscript-app-base");

        using var shell = CreateElement("div");
        shell.SetProperty("id", "xps_uiform_" + instanceId);
        shell.SetProperty("className", modal ? "modal fade show d-block xpscript-uiform-modal" : "card shadow-sm mb-3 xpscript-uiform-window");
        if (modal)
        {
            shell.SetProperty("role", "dialog");
            shell.SetProperty("ariaModal", "true");
        }

        using var dialog = CreateElement("div");
        dialog.SetProperty("className", modal ? "modal-dialog modal-dialog-scrollable" : "card-body");
        using var content = CreateElement("div");
        if (modal) content.SetProperty("className", "modal-content");
        using var body = CreateElement("div");
        body.SetProperty("id", "xpscript-app");
        if (modal) body.SetProperty("className", "modal-body");

        AppendChild(content, body);
        AppendChild(dialog, content);
        AppendChild(shell, dialog);
        AppendToBody(shell);

        JSObject? backdrop = null;
        if (modal)
        {
            backdrop = CreateElement("div");
            backdrop.SetProperty("id", "xps_uiform_backdrop_" + instanceId);
            backdrop.SetProperty("className", "modal-backdrop fade show");
            AppendToBody(backdrop);
        }

        try
        {
            _ = BrowserFormHost.ShowDialog(requestJson);
            BrowserRichMediaHost.Apply(body, requestJson);
            body.SetProperty("id", "xps_uiform_body_" + instanceId);
            baseRoot.SetProperty("id", "xpscript-app");
            FocusFirst(body);
            return "{\"result\":\"Pending\",\"values\":{}}";
        }
        catch
        {
            baseRoot.SetProperty("id", "xpscript-app");
            RemoveElement(shell);
            if (backdrop is not null) RemoveElement(backdrop);
            throw;
        }
        finally
        {
            backdrop?.Dispose();
        }
    }

    public static void Close(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return;
        using var shell = GetElementById("xps_uiform_" + instanceId);
        if (shell is not null) RemoveElement(shell);
        using var backdrop = GetElementById("xps_uiform_backdrop_" + instanceId);
        if (backdrop is not null) RemoveElement(backdrop);
    }

    public static bool IsVisible(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        using var shell = GetElementById("xps_uiform_" + instanceId);
        return shell is not null;
    }

    [JSImport("globalThis.document.getElementById")]
    private static partial JSObject? GetElementById(string id);

    [JSImport("globalThis.document.createElement")]
    private static partial JSObject CreateElement(string tagName);

    [JSImport("globalThis.document.body.appendChild")]
    private static partial JSObject AppendToBody(JSObject element);

    [JSImport("globalThis.Node.prototype.appendChild.call")]
    private static partial JSObject AppendChild(JSObject parent, JSObject child);

    [JSImport("globalThis.Element.prototype.remove.call")]
    private static partial void RemoveElement(JSObject element);

    [JSImport("globalThis.HTMLElement.prototype.focus.call")]
    private static partial void FocusElement(JSObject element);

    private static void FocusFirst(JSObject root)
    {
        try
        {
            using var first = QuerySelector(root, "input,select,textarea,button");
            if (first is not null) FocusElement(first);
        }
        catch { }
    }

    [JSImport("globalThis.Element.prototype.querySelector.call")]
    private static partial JSObject? QuerySelector(JSObject element, string selector);
}
