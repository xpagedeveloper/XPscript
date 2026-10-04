using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using XPScript.Web.Compiler;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

Environment.SetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS", "1");

var parent = Path.Combine(Path.GetTempPath(), "xps-uiform-kestrel-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var assets = Path.Combine(root, "assets");
Directory.CreateDirectory(assets);
var assetBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
await File.WriteAllBytesAsync(Path.Combine(assets, "test.png"), assetBytes);
var scriptPath = Path.Combine(root, "form.xps");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim data As New XPJsonObject
    Dim form As New UIForm("Contact form")
    Dim result As String

    Call data.Set("existing", "Loaded from JSON")
    Call form.BindData(data)
    form.BootImage = "assets/test.png"
    Call form.AddTab("main", "Main")
    Call form.AddTab("details", "Details")
    Call form.AddTextField("existing", "Existing")
    Call form.AddTextField("missing", "Missing")
    Call form.SetFieldTab("existing", "main")
    Call form.SetFieldTab("missing", "details")
    Call form.AddButtonCallback("switch", "Show details", "SwitchTab")

    result = form.ShowDialog()
    If result = "OK" Then
        Response.ContentType = "application/json; charset=utf-8"
        Response.Write(data.Stringify())
    End If
End Sub

Sub SwitchTab(evt As Variant)
    Call evt.Form.SetActiveTab("details")
End Sub
""");

await using var cache = new XpsWebCompilationCache(new XpsWebCompiler());
await using var dispatcher = new XpsWebDispatcher(root, cache);
var options = new XpsKestrelOptions
{
    Port = 0,
    MaxRequestBodySize = 1024 * 1024,
    AllowedHosts = ["localhost", "127.0.0.1", "::1"],
    EnableStaticFiles = true
};
var serverInfo = new XpsServerInfo("uiform-kestrel-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
var sessions = new XpsSessionStore();
var app = XpsKestrelAdapter.Build(options, serverInfo, dispatcher, new SmokeApplicationState(), sessions: sessions);
var stopped = false;

try
{
    await app.StartAsync();
    var server = app.Services.GetRequiredService<IServer>();
    var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
        ?? throw new Exception("Kestrel did not expose a listener address.");
    using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true };
    using var client = new HttpClient(handler) { BaseAddress = new Uri(address) };
    string? protectedAssetUrl = null;

    using (var response = await client.GetAsync("/form.xps"))
    {
        if ((int)response.StatusCode != 200) throw new Exception($"UIForm Kestrel GET expected 200, got {(int)response.StatusCode}.");
        if (!response.Content.Headers.ContentType?.ToString().StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ?? true)
            throw new Exception("UIForm Kestrel GET did not return HTML.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains(">Contact form</h1>", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel GET did not render title.");
        if (!body.Contains("name=\"existing\" value=\"Loaded from JSON\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel GET did not load existing JSON value.");
        if (!body.Contains("name=\"missing\" value=\"\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel GET did not render missing JSON field as empty.");
        if (!body.Contains("data-xps-tab=\"main\"", StringComparison.Ordinal) || !body.Contains("data-xps-tab=\"details\"", StringComparison.Ordinal))
            throw new Exception("UIForm Kestrel GET did not render tab controls.");
        if (!body.Contains("if(s.activeTab)", StringComparison.Ordinal))
            throw new Exception("UIForm Kestrel GET reactive callback script does not apply activeTab.");
        if (body.Contains("src=\"assets/test.png\"", StringComparison.Ordinal))
            throw new Exception("UIForm Kestrel GET exposed the raw asset path.");
        var routeIndex = body.IndexOf(XpsUIWebRuntimeBridge.AssetRoute, StringComparison.Ordinal);
        if (routeIndex < 0) throw new Exception("UIForm Kestrel GET did not render a protected asset URL.");
        var quoteIndex = body.IndexOf('"', routeIndex);
        if (quoteIndex < 0) throw new Exception("UIForm Kestrel GET protected asset URL was malformed.");
        protectedAssetUrl = WebUtility.HtmlDecode(body[routeIndex..quoteIndex]);
    }

    using (var response = await client.GetAsync("/assets/test.png"))
    {
        if ((int)response.StatusCode == 200) throw new Exception("Raw UIForm asset path was publicly accessible.");
    }

    if (protectedAssetUrl is null) throw new Exception("Protected UIForm asset URL was not captured.");
    using (var response = await client.GetAsync(protectedAssetUrl))
    {
        if ((int)response.StatusCode != 200) throw new Exception($"Protected UIForm asset expected 200, got {(int)response.StatusCode}.");
        if (!response.Content.Headers.ContentType?.ToString().StartsWith("image/png", StringComparison.OrdinalIgnoreCase) ?? true)
            throw new Exception("Protected UIForm asset did not return image/png.");
        if (!(await response.Content.ReadAsByteArrayAsync()).SequenceEqual(assetBytes))
            throw new Exception("Protected UIForm asset returned unexpected bytes.");
    }

    using (var foreignHandler = new HttpClientHandler { UseCookies = false })
    using (var foreignClient = new HttpClient(foreignHandler) { BaseAddress = new Uri(address) })
    using (var response = await foreignClient.GetAsync(protectedAssetUrl))
    {
        if ((int)response.StatusCode != 404) throw new Exception($"Protected UIForm asset without session expected 404, got {(int)response.StatusCode}.");
    }

    using (var request = new HttpRequestMessage(HttpMethod.Get, protectedAssetUrl))
    {
        request.Headers.Referrer = new Uri("https://example.invalid/");
        using var response = await client.SendAsync(request);
        if ((int)response.StatusCode != 403) throw new Exception($"Protected UIForm asset with cross-origin Referer expected 403, got {(int)response.StatusCode}.");
    }

    var tampered = protectedAssetUrl.Replace("token=", "token=00", StringComparison.Ordinal);
    using (var response = await client.GetAsync(tampered))
    {
        if ((int)response.StatusCode != 404) throw new Exception($"Protected UIForm asset with invalid token expected 404, got {(int)response.StatusCode}.");
    }
    }

    using (var content = new StringContent("existing=Loaded+from+JSON&missing=&__xps_uiform_event=button%3Aswitch&__xps_uiform_event_value=", Encoding.UTF8, "application/x-www-form-urlencoded"))
    using (var response = await client.PostAsync("/form.xps", content))
    {
        if ((int)response.StatusCode != 200) throw new Exception($"UIForm Kestrel callback POST expected 200, got {(int)response.StatusCode}.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("\"activeTab\":\"details\"", StringComparison.Ordinal))
            throw new Exception("UIForm Kestrel callback did not return the programmatically selected active tab.");
    }

    using (var content = new StringContent("existing=Changed+value&missing=Created+by+user", Encoding.UTF8, "application/x-www-form-urlencoded"))
    using (var response = await client.PostAsync("/form.xps", content))
    {
        if ((int)response.StatusCode != 200) throw new Exception($"UIForm Kestrel POST expected 200, got {(int)response.StatusCode}.");
        if (!response.Content.Headers.ContentType?.ToString().StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ?? true)
            throw new Exception("UIForm Kestrel POST did not return JSON.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("\"existing\":\"Changed value\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel POST did not save existing field.");
        if (!body.Contains("\"missing\":\"Created by user\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel POST did not create missing JSON key.");
    }

    Console.WriteLine("WEB-UIFORM-KESTREL=OK");
    await app.StopAsync();
    stopped = true;
}
finally
{
    if (!stopped) await app.StopAsync();
    await app.DisposeAsync();
    if (Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    public object? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, object? value) => _values[name] = value;
    public bool Remove(string name) => _values.Remove(name);
    public void Clear() => _values.Clear();
}
