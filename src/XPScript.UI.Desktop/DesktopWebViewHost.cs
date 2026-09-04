using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

internal static class DesktopWebViewHost
{
    private static readonly ConcurrentDictionary<string, NativeWebView> Views = new(StringComparer.Ordinal);

    internal static NativeWebView Create(string instanceId, string fieldName, string source, string html, string userAgent, string background)
    {
        var view = new NativeWebView { MinHeight = 240, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch };
        if (!string.IsNullOrWhiteSpace(userAgent)) view.UserAgent = userAgent;
        if (!string.IsNullOrWhiteSpace(background)) view.Background = new SolidColorBrush(Color.Parse(background));
        Views[Key(instanceId, fieldName)] = view;
        if (!string.IsNullOrEmpty(html)) view.AdapterCreated += (_, _) => view.NavigateToString(html);
        else if (Uri.TryCreate(string.IsNullOrWhiteSpace(source) ? "about:blank" : source, UriKind.Absolute, out var uri)) view.Source = uri;
        return view;
    }

    internal static void RemoveInstance(string instanceId)
    {
        var prefix = instanceId + "\u001f";
        foreach (var key in Views.Keys.Where(value => value.StartsWith(prefix, StringComparison.Ordinal))) Views.TryRemove(key, out _);
    }

    internal static string? TryCommand(string instanceId, string fieldName, string command, string? argument)
    {
        if (!Views.TryGetValue(Key(instanceId, fieldName), out var view)) return null;
        string result = string.Empty;
        void Execute()
        {
            result = command.ToLowerInvariant() switch
            {
                "source" => view.Source?.ToString() ?? string.Empty,
                "navigate" => Navigate(view, argument),
                "html" => NavigateHtml(view, argument),
                "script" => Wait(view.InvokeScript(argument ?? string.Empty)) ?? string.Empty,
                "back" => Bool(view.GoBack()), "forward" => Bool(view.GoForward()), "refresh" => Bool(view.Refresh()), "stop" => Bool(view.Stop()),
                "cangoback" => Bool(view.CanGoBack), "cangoforward" => Bool(view.CanGoForward),
                "useragent:get" => view.UserAgent ?? string.Empty, "useragent:set" => SetUserAgent(view, argument),
                "background:get" => view.Background?.ToString() ?? string.Empty, "background:set" => SetBackground(view, argument),
                "adapterinfo" => view.AdapterInfo?.ToString() ?? string.Empty,
                "platformhandle" => PlatformHandle(view),
                "print" => Print(view), "pdf" => PrintPdf(view, argument),
                "copy" => Edit(view, m => m.Copy()), "cut" => Edit(view, m => m.Cut()), "paste" => Edit(view, m => m.Paste()),
                "selectall" => Edit(view, m => m.SelectAll()), "undo" => Edit(view, m => m.Undo()), "redo" => Edit(view, m => m.Redo()),
                "cookies:get" => GetCookies(view), "cookies:set" => SetCookie(view, argument), "cookies:delete" => DeleteCookie(view, argument), "cookies:clear" => ClearCookies(view),
                _ => throw new InvalidOperationException("Unknown UIForm WebView command: " + command)
            };
        }
        if (Dispatcher.UIThread.CheckAccess()) Execute(); else Dispatcher.UIThread.Invoke(Execute);
        return result;
    }

    private static string Navigate(NativeWebView view, string? value) { if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) throw new InvalidOperationException("WebView navigation requires an absolute URI."); view.Navigate(uri); return uri.AbsoluteUri; }
    private static string NavigateHtml(NativeWebView view, string? html) { view.NavigateToString(html ?? string.Empty); return "true"; }
    private static string SetUserAgent(NativeWebView view, string? value) { view.UserAgent = value ?? string.Empty; return view.UserAgent ?? string.Empty; }
    private static string SetBackground(NativeWebView view, string? value) { if (!string.IsNullOrWhiteSpace(value)) view.Background = new SolidColorBrush(Color.Parse(value)); return view.Background?.ToString() ?? string.Empty; }
    private static string PlatformHandle(NativeWebView view) { var handle = view.TryGetPlatformHandle(); return handle is null ? string.Empty : handle.Handle.ToString(); }
    private static string Print(NativeWebView view) { view.ShowPrintUI(); return "true"; }
    private static string PrintPdf(NativeWebView view, string? path) { if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("WebView PrintToPdf requires a file path."); using var stream = Wait(view.PrintToPdfStreamAsync()) ?? throw new InvalidOperationException("WebView did not return PDF data."); var fullPath = Path.GetFullPath(path); var directory = Path.GetDirectoryName(fullPath); if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory); using var output = File.Create(fullPath); stream.CopyTo(output); return fullPath; }
    private static string Edit(NativeWebView view, Action<NativeWebViewCommandManager> action) { var manager = view.TryGetCommandManager(); if (manager is null) return "false"; action(manager); return "true"; }
    private static string GetCookies(NativeWebView view) { var manager = view.TryGetCookieManager(); if (manager is null) return "[]"; var cookies = Wait(manager.GetCookiesAsync()) ?? []; return JsonSerializer.Serialize(cookies.Select(cookie => new { cookie.Name, cookie.Value, cookie.Domain, cookie.Path, cookie.Secure, cookie.HttpOnly, expires = cookie.Expires == DateTime.MinValue ? null : cookie.Expires.ToString("O", System.Globalization.CultureInfo.InvariantCulture) })); }
    private static string SetCookie(NativeWebView view, string? payload) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; using var document = JsonDocument.Parse(payload ?? "{}"); manager.AddOrUpdateCookie(BuildCookie(document.RootElement, true)); return "true"; }
    private static string DeleteCookie(NativeWebView view, string? payload) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; using var document = JsonDocument.Parse(payload ?? "{}"); var cookie = BuildCookie(document.RootElement, false); manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path); return "true"; }
    private static string ClearCookies(NativeWebView view) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; var cookies = Wait(manager.GetCookiesAsync()) ?? []; foreach (var cookie in cookies) manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path); return "true"; }
    private static Cookie BuildCookie(JsonElement root, bool includeValue) { static string Read(JsonElement value, string name, string fallback = "") => value.TryGetProperty(name, out var property) ? property.GetString() ?? fallback : fallback; var name = Read(root, "name"); var domain = Read(root, "domain"); var path = Read(root, "path", "/"); if (name.Length == 0 || domain.Length == 0) throw new InvalidOperationException("WebView cookies require name and domain."); return new Cookie(name, includeValue ? Read(root, "value") : string.Empty, path.Length == 0 ? "/" : path, domain); }
    private static T? Wait<T>(Task<T> task) { if (!Dispatcher.UIThread.CheckAccess() || task.IsCompleted) return task.GetAwaiter().GetResult(); var frame = new DispatcherFrame(); task.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false), TaskScheduler.Default); Dispatcher.UIThread.PushFrame(frame); return task.GetAwaiter().GetResult(); }
    private static string Bool(bool value) => value ? "true" : "false";
    private static string Key(string instanceId, string fieldName) => instanceId + "\u001f" + fieldName.ToLowerInvariant();
}
