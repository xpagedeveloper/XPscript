using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using XPScript.Web.Compiler;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

Environment.SetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS", "1");
var parent = Path.Combine(Path.GetTempPath(), "xps-list-kestrel-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
await File.WriteAllTextAsync(Path.Combine(root, "list.xps"), """
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim rows As New XPJsonArray
    Dim row As New XPJsonObject
    Dim list As New UIListView("Customers")
    Call row.Set("id", "1001")
    Call rows.Add(row)
    Call list.BindData(rows)
    Call list.AddColumn("id", "Id")
    Call list.SetOnSelectCallback("OnSelected", "web-context")
    Call list.ShowDialog()
End Sub

Sub OnSelected(evt As Variant, context As String)
    If evt.EventType <> "select" Then Error 5, "wrong event type"
    If context <> "web-context" Then Error 5, "wrong callback context"
    Call evt.ListView.SetColumnLabel("id", "Selected")
End Sub
""");

await using var cache = new XpsWebCompilationCache(new XpsWebCompiler());
await using var dispatcher = new XpsWebDispatcher(root, cache);
var options = new XpsKestrelOptions { Port = 0, MaxRequestBodySize = 1024 * 1024, AllowedHosts = ["localhost", "127.0.0.1", "::1"] };
var serverInfo = new XpsServerInfo("uilist-kestrel-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
var app = XpsKestrelAdapter.Build(options, serverInfo, dispatcher, new SmokeApplicationState());
var stopped = false;
try
{
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.Single()
        ?? throw new Exception("Kestrel did not expose a listener address.");
    using var client = new HttpClient { BaseAddress = new Uri(address) };
    using (var get = await client.GetAsync("/list.xps"))
    {
        if ((int)get.StatusCode != 200) throw new Exception($"UIListView GET expected 200, got {(int)get.StatusCode}.");
        var html = await get.Content.ReadAsStringAsync();
        if (!html.Contains("Customers", StringComparison.Ordinal) || !html.Contains("1001", StringComparison.Ordinal))
            throw new Exception("UIListView GET did not render the bound list: " + html[..Math.Min(html.Length, 500)]);
    }
    using var content = new StringContent("__xps_list_event=select&__xps_list_index=0", Encoding.UTF8, "application/x-www-form-urlencoded");
    using var post = await client.PostAsync("/list.xps", content);
    var body = await post.Content.ReadAsStringAsync();
    if ((int)post.StatusCode != 200 || !body.Contains("\"label\":\"Selected\"", StringComparison.Ordinal))
        throw new Exception($"UIListView event POST did not return the callback-updated live state: {(int)post.StatusCode}/{body}");
    Console.WriteLine("WEB-UILISTVIEW-EVENT=OK");
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
