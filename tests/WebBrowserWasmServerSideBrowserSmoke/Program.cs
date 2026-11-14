using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using XPScript.Web.Compiler;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-wasm-serverside-browser-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Environment.SetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS", "1");
    await File.WriteAllTextAsync(Path.Combine(root, "index.xps"), """
[Platform:browser-wasm]
[ServerSide]
Function ServerTransform(value As String) As String
    ServerTransform = UCase(value) & "-SERVER"
End Function
Sub Main()
    Dim form As New UIForm("WASM ServerSide")
    Call form.AddTextField("status", "Status")
    Call form.SetFieldValue("status", "CLIENT")
    Call form.AddButtonCallback("send", "Send to server", "SendToServer")
    Call form.Show(False)
End Sub
Sub SendToServer(evt As Variant)
    Call ContinueOnClient(evt)
End Sub
Sub ContinueOnClient(evt As Variant)
    Dim currentForm As Variant
    currentForm = evt.Form
    Dim serverValue As String
    serverValue = ServerTransform("wasm")
    Call currentForm.SetFieldValue("status", serverValue & "-CLIENT")
End Sub
""");
    await using var dispatcher = new XpsWebDispatcher(root, new XpsWebCompilationCacheOptions { EnablePersistentCache = false, ConfigurationIdentity = "wasm-serverside-browser" });
    await using var app = XpsKestrelAdapter.Build(new XpsKestrelOptions { Address = System.Net.IPAddress.Loopback, Port = 0, AllowedHosts = ["127.0.0.1"] }, new XpsServerInfo("wasm-serverside-browser", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"), dispatcher);
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.Single() ?? throw new Exception("Kestrel address missing.");
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    var page = await browser.NewPageAsync();
    var errors = new List<string>();
    page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
    page.PageError += (_, error) => errors.Add(error);
    page.RequestFailed += (_, request) => Console.WriteLine($"REQUEST_FAILED {request.Method} {request.Url} {request.Failure}");
    page.Response += (_, response) => { if (response.Status >= 400) Console.WriteLine($"HTTP_ERROR {response.Status} {response.Url}"); };
    await page.GotoAsync(address + "/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
    Console.WriteLine($"PAGE_URL={page.Url}");
    Console.WriteLine("SCRIPT_SRC=" + await page.EvaluateAsync<string>("document.querySelector('script')?.src || ''"));
    Console.WriteLine("RESOURCES=" + await page.EvaluateAsync<string>("performance.getEntriesByType('resource').map(x => x.name).join(' | ')"));
    var status = page.Locator("input[name='status']");
    try
    {
        await status.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 120000 });
    }
    catch
    {
        Console.WriteLine("BROWSER_ERRORS=" + string.Join(" | ", errors));
        Console.WriteLine("PAGE_HTML=" + (await page.ContentAsync()).Replace('\n', ' ').Replace('\r', ' '));
        throw;
    }
    if (await status.InputValueAsync() != "CLIENT") throw new Exception("Initial WASM state missing.");
    await page.GetByRole(AriaRole.Button, new() { Name = "Send to server" }).ClickAsync();
    await Assertions.Expect(status).ToHaveValueAsync("WASM-SERVER-CLIENT", new() { Timeout = 120000 });
    if (errors.Count != 0) throw new Exception("Browser errors: " + string.Join(" | ", errors));
    Console.WriteLine("WASM_PLAYWRIGHT_PAGE_OK");
    Console.WriteLine("WASM_SERVERSIDE_EVENT_OK");
}
finally { try { Directory.Delete(root, true); } catch { } }
