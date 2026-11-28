using Avalonia.Controls;

namespace XPScript.UI.Desktop;

internal static class DesktopMediaHost
{
    internal static NativeWebView Create(string instanceId, DesktopFormField field)
    {
        var tag = field.Type.Equals("Video", StringComparison.OrdinalIgnoreCase) ? "video" : "audio";
        var source = System.Net.WebUtility.HtmlEncode(DesktopImageHost.ToWebSource(field.Source));
        var label = System.Net.WebUtility.HtmlEncode(field.Label.Length > 0 ? field.Label : field.Name);
        var html = $"<html><body style=\"margin:0;background:transparent;display:flex;align-items:center;justify-content:center\"><{tag} controls playsinline preload=\"metadata\" aria-label=\"{label}\" style=\"max-width:100%;width:100%;max-height:100%;\" src=\"{source}\"></{tag}></body></html>";
        return DesktopWebViewHost.Create(instanceId, field.Name, "about:blank", html, string.Empty, string.Empty);
    }
}
