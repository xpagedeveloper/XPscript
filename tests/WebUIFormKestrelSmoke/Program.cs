using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XPScript.Web.Compiler;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

Environment.SetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS", "1");

var serving = args.Contains("--serve", StringComparer.OrdinalIgnoreCase);
var configuredPort = int.TryParse(Environment.GetEnvironmentVariable("XPSCRIPT_WEBUIFORM_SMOKE_PORT"), out var portFromEnvironment)
    && portFromEnvironment is > 0 and <= 65535
        ? portFromEnvironment
        : serving ? throw new InvalidOperationException("--serve requires XPSCRIPT_WEBUIFORM_SMOKE_PORT to name a valid port.") : 0;
var parent = serving
    ? Path.Combine(Path.GetTempPath(), "xps-uiform-kestrel-manual")
    : Path.Combine(Path.GetTempPath(), "xps-uiform-kestrel-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
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
    Call form.AddTextField("existing", "Existing")
    Call form.AddTextField("missing", "Missing")
    Dim firstPanel As Variant
    firstPanel = form.AddPanel("firstPanel", "First")
    firstPanel.Id = "firstPanel"
    Dim secondPanel As Variant
    secondPanel = form.AddPanel("secondPanel", "Second")
    secondPanel.Id = "secondPanel"
    Dim moved As Variant
    moved = form.AddTextField("moved", "Moved")
    moved.Id = "moved"
    Call firstPanel.AddUIElement(moved)
    Call secondPanel.AddUIElement(moved)
    Call form.AddUIElement(firstPanel)
    Call form.AddUIElement(secondPanel)
    Call form.AddButton("save", "Save", "Noop")

    result = form.ShowDialog()
    If result = "OK" Or result = "save" Then
        Call data.Set("dialogResult", result)
        Response.ContentType = "application/json; charset=utf-8"
        Response.Write(data.Stringify())
    End If
End Sub

Sub Noop()
End Sub
""");

await File.WriteAllTextAsync(Path.Combine(root, "manual.xps"), $$"""
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim form As New UIForm("UIForm manual test bench")
    Call form.AddLookupField("customerId", "Customer lookup (try typing 'al', 'mar', or 'jo')", "http://127.0.0.1:{{configuredPort}}/lookup.xps", "id", "name", True)
    Call form.SetRemoteSearchOptions("customerId", "q", "value", 2, 10)
    Call form.AddTextField("notes", "Notes")
    Call form.AddButton("save", "Save form", "Noop")
    Dim result As String
    result = form.ShowDialog()
    If Request.Method = "POST" Then
        Response.ContentType = "text/html; charset=utf-8"
        Response.Write("<!doctype html><html><head><meta charset=""utf-8""><title>Form result</title></head><body><h1>Form submitted</h1><p>Selected action: " & Server.HtmlEncode(result) & "</p><p><a href=""/manual.xps"">Return to test bench</a></p></body></html>")
    Else
        Response.Write("<section style=""font-family:system-ui;max-width:760px;margin:0 auto;padding:0 16px 24px""><h2>Browser dialog probes</h2><p>These buttons open native browser dialogs:</p><button type=""button"" onclick=""alert('This is a message dialog.')"">Message (alert)</button> <button type=""button"" onclick=""document.getElementById('dialog-result').textContent=confirm('Do you want to continue?')?'Choice: Yes':'Choice: No'"">Choice (confirm)</button> <button type=""button"" onclick=""document.getElementById('dialog-result').textContent='Input: '+(prompt('Enter a short value:')??'(cancelled)')"">Input (prompt)</button><p id=""dialog-result"" aria-live=""polite""></p></section>")
    End If
End Sub

Sub Noop()
End Sub
""");

await File.WriteAllTextAsync(Path.Combine(root, "lookup.xps"), """
[Anonymous]
[Get]
Sub Index()
    Dim query As String
    Dim value As String
    query = LCase(Request.Query("q"))
    value = Request.Query("value")
    Response.ContentType = "application/json; charset=utf-8"
    If value <> "" Then
        Select Case value
            Case "c1": Response.Write("[{""id"":""c1"",""name"":""Alex Morgan""}]")
            Case "c2": Response.Write("[{""id"":""c2"",""name"":""Maria Chen""}]")
            Case "c3": Response.Write("[{""id"":""c3"",""name"":""Jordan Lee""}]")
            Case Else: Response.Write("[]")
        End Select
    ElseIf InStr("alex morgan", query) > 0 And query <> "" Then
        Response.Write("[{""id"":""c1"",""name"":""Alex Morgan""}]")
    ElseIf InStr("maria chen", query) > 0 And query <> "" Then
        Response.Write("[{""id"":""c2"",""name"":""Maria Chen""}]")
    ElseIf InStr("jordan lee", query) > 0 And query <> "" Then
        Response.Write("[{""id"":""c3"",""name"":""Jordan Lee""}]")
    Else
        Response.Write("[]")
    End If
End Sub
""");

await using var cache = new XpsWebCompilationCache(new XpsWebCompiler());
await using var dispatcher = new XpsWebDispatcher(root, cache);
var options = new XpsKestrelOptions
{
    Port = args.Contains("--serve", StringComparer.OrdinalIgnoreCase)
        ? configuredPort
        : 0,
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
    if (args.Contains("--serve", StringComparer.OrdinalIgnoreCase))
    {
        using (var warmup = new HttpClient { BaseAddress = new Uri(address) })
        {
            foreach (var path in new[] { "/manual.xps", "/lookup.xps?q=al" })
            using (var response = await warmup.GetAsync(path))
            {
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Manual UIForm warmup for {path} failed with HTTP {(int)response.StatusCode}.");
                _ = await response.Content.ReadAsByteArrayAsync();
            }
        }
        Console.WriteLine($"WEBUIFORM-PLAYWRIGHT-READY={address}");
        await app.WaitForShutdownAsync();
        await app.StopAsync();
        stopped = true;
        return;
    }

    using var client = new HttpClient { BaseAddress = new Uri(address) };

    using (var response = await client.GetAsync("/form.xps"))
    {
        if ((int)response.StatusCode != 200) throw new Exception($"UIForm Kestrel GET expected 200, got {(int)response.StatusCode}.");
        if (!response.Content.Headers.ContentType?.ToString().StartsWith("text/html", StringComparison.OrdinalIgnoreCase) ?? true)
            throw new Exception("UIForm Kestrel GET did not return HTML.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains(">Contact form</h1>", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel GET did not render title.");
        if (!body.Contains("name=\"existing\" value=\"Loaded from JSON\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel GET did not load existing JSON value.");
        if (!body.Contains("name=\"missing\" value=\"\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel GET did not render missing JSON field as empty.");
        if (!body.Contains("data-parent-id=\"secondPanel\"", StringComparison.Ordinal) || body.Contains("data-parent-id=\"firstPanel\"", StringComparison.Ordinal))
            throw new Exception("UIForm reparenting did not move the control to its new visual parent.");
        if (!body.Contains("name=\"__xps_uiform_result\" value=\"save\"", StringComparison.Ordinal))
            throw new Exception("UIForm did not render the custom dialog result button.");
    }

    using (var content = new StringContent("existing=Changed+value&missing=Created+by+user&__xps_uiform_result=save", Encoding.UTF8, "application/x-www-form-urlencoded"))
    using (var response = await client.PostAsync("/form.xps", content))
    {
        if ((int)response.StatusCode != 200) throw new Exception($"UIForm Kestrel POST expected 200, got {(int)response.StatusCode}.");
        if (!response.Content.Headers.ContentType?.ToString().StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ?? true)
            throw new Exception("UIForm Kestrel POST did not return JSON.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("\"existing\":\"Changed value\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel POST did not save existing field.");
        if (!body.Contains("\"missing\":\"Created by user\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel POST did not create missing JSON key.");
        if (!body.Contains("\"dialogResult\":\"save\"", StringComparison.Ordinal)) throw new Exception("UIForm Kestrel POST did not return the selected custom result.");
    }

    Console.WriteLine("WEB-UIFORM-KESTREL=OK");
    await app.StopAsync();
    stopped = true;
}
finally
{
    if (!stopped) await app.StopAsync();
    await app.DisposeAsync();
    if (!serving && Directory.Exists(parent)) Directory.Delete(parent, recursive: true);
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    public object? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, object? value) => _values[name] = value;
    public bool Remove(string name) => _values.Remove(name);
    public void Clear() => _values.Clear();
}
