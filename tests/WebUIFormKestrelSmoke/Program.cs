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
var scriptPath = Path.Combine(root, "form.xps");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim data As New JsonObject
    Dim form As New UIForm("Contact form")
    Dim result As String

    Call data.Set("existing", "Loaded from JSON")
    Call form.BindData(data)
    Call form.AddTextField("existing", "Existing")
    Call form.AddTextField("missing", "Missing")

    result = form.ShowDialog()
    If result = "OK" Then
        Response.ContentType = "application/json; charset=utf-8"
        Response.Write(data.Stringify())
    End If
End Sub
""");

await using var cache = new XpsWebCompilationCache(new XpsWebCompiler());
await using var dispatcher = new XpsWebDispatcher(root, cache);
var options = new XpsKestrelOptions
{
    Port = 0,
    MaxRequestBodySize = 1024 * 1024,
    AllowedHosts = ["localhost", "127.0.0.1", "::1"]
};
var serverInfo = new XpsServerInfo("uiform-kestrel-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
var app = XpsKestrelAdapter.Build(options, serverInfo, dispatcher, new SmokeApplicationState());
var stopped = false;

try
{
    await app.StartAsync();
    var server = app.Services.GetRequiredService<IServer>();
    var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
        ?? throw new Exception("Kestrel did not expose a listener address.");
    using var client = new HttpClient { BaseAddress = new Uri(address) };

    using (var response = await client.GetAsync("/form.xps"))
    {
        if ((int)response.StatusCode != 200) throw new Exception($"UIForm Kestrel GET expected 200, got {(int)response.StatusCode}.");
        if (!response.Content.Headers.ContentType?.ToString().StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ?? true)
            throw new Exception("UIForm Kestrel GET did not return HTML.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("<h1>Contact form</h1>", StringComparison.Ordinal))
            throw new Exception($"UIForm Kestrel GET did not render title. Body: {body}");
        if (!body.Contains("name=\"existing\" value=\"Loaded from JSON\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel GET did not load existing JSON value.");
        if (!body.Contains("name=\"missing\" value=\"\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel GET did not render missing JSON field as empty.");
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
