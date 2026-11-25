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

await File.WriteAllTextAsync(Path.Combine(root, "manual.xps"), $$$"""
[Anonymous]
[Get]
[Post]
Sub Index()
    Dim form As New UIForm("UIForm manual test bench")
    Call form.AddLookupField("customerId", "Customer lookup (try typing 'al', 'mar', or 'jo')", "http://127.0.0.1:{{{configuredPort}}}/lookup.xps", "id", "name", True)
    Call form.SetRemoteSearchOptions("customerId", "q", "value", 2, 10)
    Call form.AddTextField("notes", "Notes")
    Call form.AddButton("save", "Save form", "Noop")
    Dim result As String
    result = form.ShowDialog()
    If Request.Method = "POST" Then
        Response.ContentType = "text/html; charset=utf-8"
        Response.Write("<!doctype html><html><head><meta charset=""utf-8""><title>Form result</title></head><body><h1>Form submitted</h1><p>Selected action: " & Server.HtmlEncode(result) & "</p><p><a href=""/manual.xps"">Return to test bench</a></p></body></html>")
    Else
Response.Write("<section style=""font-family:system-ui;max-width:760px;margin:0 auto;padding:0 16px 24px""><h2>Dialog titles</h2><label for=""dialog-title-target"">Dialog</label> <select id=""dialog-title-target""><option value=""message"">Message</option><option value=""choice"">Choice</option><option value=""input"">Input</option></select> <label for=""dialog-title-value"">Title</label> <input id=""dialog-title-value"" type=""text""><button type=""button"" id=""apply-dialog-title"">Apply title</button></section><section id=""choice-button-customizer"" style=""font-family:system-ui;max-width:760px;margin:0 auto;padding:0 16px 24px""><h2>Choice buttons</h2><p>Set each button label and the value it returns.</p><label for=""choice-label-1"">Button 1 label</label> <input id=""choice-label-1"" value=""Yes""><label for=""choice-value-1"">Return value</label> <input id=""choice-value-1"" value=""yes""><br><label for=""choice-label-2"">Button 2 label</label> <input id=""choice-label-2"" value=""No""><label for=""choice-value-2"">Return value</label> <input id=""choice-value-2"" value=""no""><br><label for=""choice-label-3"">Button 3 label</label> <input id=""choice-label-3"" value=""Maybe""><label for=""choice-value-3"">Return value</label> <input id=""choice-value-3"" value=""maybe""><br><button type=""button"" id=""apply-choice-buttons"">Apply choice buttons</button></section>")
Response.Write("<section style=""font-family:system-ui;max-width:760px;margin:0 auto;padding:0 16px 24px""><h2>Inline dialog probes</h2><p>Dialogs use Bootstrap styling shared with the web UIForm.</p><button type=""button"" class=""btn btn-primary"" onclick=""openInlineDialog('message')"">Message dialog</button> <button type=""button"" class=""btn btn-primary"" onclick=""openInlineDialog('choice')"">Choice dialog</button> <button type=""button"" class=""btn btn-primary"" onclick=""openInlineDialog('input')"">Input dialog</button><p id=""dialog-result"" aria-live=""polite""></p><dialog id=""message-dialog"" class=""bg-body text-body p-4 border rounded-3 shadow"" style=""min-width:min(28rem,calc(100vw - 2rem));min-height:12rem;max-width:calc(100vw - 2rem)"" aria-labelledby=""message-dialog-title""><h2 class=""h5 mb-3"" id=""message-dialog-title"">Message</h2><p class=""mb-4"">This is an inline message dialog.</p><div class=""d-flex justify-content-end gap-2""><button type=""button"" class=""btn btn-primary"" onclick=""document.getElementById('dialog-result').textContent='Message: closed';document.getElementById('message-dialog').close()"">OK</button></div></dialog><dialog id=""choice-dialog"" class=""bg-body text-body p-4 border rounded-3 shadow"" style=""min-width:min(28rem,calc(100vw - 2rem));min-height:12rem;max-width:calc(100vw - 2rem)"" aria-labelledby=""choice-dialog-title""><h2 class=""h5 mb-3"" id=""choice-dialog-title"">Choose an option</h2><p class=""mb-4"">Do you want to continue?</p><div class=""d-flex justify-content-end gap-2"" id=""choice-actions""></div></dialog><dialog id=""input-dialog"" class=""bg-body text-body p-4 border rounded-3 shadow"" style=""min-width:min(28rem,calc(100vw - 2rem));min-height:12rem;max-width:calc(100vw - 2rem)"" aria-labelledby=""input-dialog-title""><h2 class=""h5 mb-3"" id=""input-dialog-title"">Enter a short value</h2><div class=""mb-4""><label for=""input-dialog-value"" class=""form-label"">Value</label><input id=""input-dialog-value"" class=""form-control"" type=""text""></div><div class=""d-flex justify-content-end gap-2""><button type=""button"" class=""btn btn-secondary"" onclick=""document.getElementById('input-dialog').close()"">Cancel</button> <button type=""button"" class=""btn btn-primary"" onclick=""const d=document.getElementById('input-dialog');document.getElementById('dialog-result').textContent='Input: '+document.getElementById('input-dialog-value').value;d.close()"">Use value</button></div></dialog></section>")
Response.Write("<script>function openInlineDialog(name){document.getElementById(name+'-dialog').showModal();}document.getElementById('apply-dialog-title').addEventListener('click',function(){const name=document.getElementById('dialog-title-target').value;const title=document.getElementById(name+'-dialog-title');const value=document.getElementById('dialog-title-value').value.trim();if(value)title.textContent=value;});function applyChoiceButtons(){const host=document.getElementById('choice-actions');host.replaceChildren();for(let i=1;i<=3;i++){const label=document.getElementById('choice-label-'+i).value.trim();const value=document.getElementById('choice-value-'+i).value.trim();if(!label||!value)continue;const button=document.createElement('button');button.type='button';button.className='btn btn-primary';button.textContent=label;button.dataset.returnValue=value;button.addEventListener('click',function(){document.getElementById('dialog-result').textContent='Choice: '+button.dataset.returnValue;document.getElementById('choice-dialog').close();});host.appendChild(button);}}document.getElementById('apply-choice-buttons').addEventListener('click',applyChoiceButtons);applyChoiceButtons();</script>")
        Response.Write("<style>.toast-region{position:fixed;z-index:9999;display:flex;gap:.5rem;pointer-events:none;max-width:90vw}.toast-region.top-left{top:1rem;left:1rem;flex-direction:column}.toast-region.top-right{top:1rem;right:1rem;flex-direction:column}.toast-region.bottom-left{bottom:1rem;left:1rem;flex-direction:column-reverse}.toast-region.bottom-right{bottom:1rem;right:1rem;flex-direction:column-reverse}.xps-test-toast{background:#202938;color:white;padding:.8rem 1rem;border-radius:.5rem;box-shadow:0 4px 18px #0004;pointer-events:auto}</style><section style=""font-family:system-ui;max-width:760px;margin:0 auto;padding:0 16px 24px""><h2>Toast notifications</h2><label for=""toast-position"">Position</label> <select id=""toast-position""><option value=""top-left"">Top left</option><option value=""top-right"">Top right</option><option value=""bottom-left"">Bottom left</option><option value=""bottom-right"">Bottom right</option></select> <label for=""toast-duration"">Duration (seconds)</label> <input id=""toast-duration"" type=""number"" min=""1"" max=""30"" value=""3""> <button type=""button"" id=""toast-show"">Show toast</button></section><div id=""toast-top-left"" class=""toast-region top-left"" role=""region"" aria-label=""Top left notifications""></div><div id=""toast-top-right"" class=""toast-region top-right"" role=""region"" aria-label=""Top right notifications""></div><div id=""toast-bottom-left"" class=""toast-region bottom-left"" role=""region"" aria-label=""Bottom left notifications""></div><div id=""toast-bottom-right"" class=""toast-region bottom-right"" role=""region"" aria-label=""Bottom right notifications""></div><script>window.xpsToastSequence=0;document.getElementById('toast-show').addEventListener('click',function(){const position=document.getElementById('toast-position').value;const raw=Number(document.getElementById('toast-duration').value)||3;const seconds=Math.max(1,Math.min(30,raw));const toast=document.createElement('div');toast.className='xps-test-toast';toast.setAttribute('role','status');toast.textContent='Toast '+(++window.xpsToastSequence)+' ('+position+'), expires in '+seconds+'s';document.getElementById('toast-'+position).appendChild(toast);window.setTimeout(function(){toast.remove();},seconds*1000);});</script>")
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
