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
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XPSCRIPT_TARGET_FRAMEWORK")))
        Environment.SetEnvironmentVariable("XPSCRIPT_TARGET_FRAMEWORK", "net10.0");
    await File.WriteAllTextAsync(Path.Combine(root, "index.xps"), """
[Platform:browser-wasm]
[ServerSide]
Function ServerTransform() As String
    ServerTransform = "WASM-SERVER"
End Function
Sub RefreshCities(form As UIForm)
    Call form.ClearOptions("city")
    If form.GetFieldValue("country") = "Norway" Then
        Call form.AddOption("city", "Oslo")
        Call form.AddOption("city", "Bergen")
    Else
        Call form.AddOption("city", "Stockholm")
        Call form.AddOption("city", "Gothenburg")
    End If
End Sub
Sub Main()
    Dim form As New UIForm("WASM ServerSide")
    Dim tabs As Variant
    Dim mainTab As Variant
    tabs = form.AddTabContainer("mainTabs")
    mainTab = tabs.AddTab("main", "Main")
    mainTab.Background = "#e8f0ff"
    mainTab.Font = "Arial"
    mainTab.FontSize = 18
    mainTab.Bold = True
    mainTab.SelectedBackground = "#3366CC"
    mainTab.SelectedFontColor = "#FFFFFF"
    mainTab.SelectedFontSize = 20
    mainTab.SelectedBold = True
    Dim detailsTab As Variant
    detailsTab = tabs.AddTab("details", "Details")
    detailsTab.Background = "#F0F0F0"
    detailsTab.FontColor = "#111111"
    detailsTab.FontSize = 13
    tabs.TabPlacement = "Bottom"
    Call form.AddTextField("status", "Status")
    Call form.SetFieldTab("status", "main")
    Call form.SetFieldValue("status", "CLIENT")
    Dim country As Variant
    country = form.NewSelect("country", "Country")
    Call country.AddOption("Sweden")
    Call country.AddOption("Norway")
    Dim city As Variant
    city = form.NewSelect("city", "City")
    Call city.AddOption("Stockholm")
    Call city.AddOption("Gothenburg")
    Dim cityRegion As Variant
    cityRegion = form.NewFieldRegion("city", "cityRegion")
    Call cityRegion.AddUIElement(city)
    Call form.AddUIElement(cityRegion)
    Call form.SetRefreshOnChange("country", "cityRegion", "RefreshCities")
    Call form.AddButtonCallback("send", "Send to server", "HandleSend")
    Call form.Show(False)
End Sub
Sub HandleSend(evt As Variant)
    Print "WASM_TRACE callback HandleSend"
    Call ContinueAfterServer()
End Sub
Function ContinueAfterServer() As String
    Print "WASM_TRACE client helper entered"
    Print "WASM_TRACE before ServerSide"
    ContinueAfterServer = ServerTransform()
    Print "WASM_TRACE after ServerSide"
    Print "WASM_SERVERSIDE_EVENT_OK"
    Print "WASM_TRACE field updated"
End Function
""");
    await using var dispatcher = new XpsWebDispatcher(root, new XpsWebCompilationCacheOptions { EnablePersistentCache = false, ConfigurationIdentity = "wasm-serverside-browser" });
    await using var app = XpsKestrelAdapter.Build(new XpsKestrelOptions { Address = System.Net.IPAddress.Loopback, Port = 0, AllowedHosts = ["127.0.0.1"] }, new XpsServerInfo("wasm-serverside-browser", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"), dispatcher, sessions: new XpsSessionStore());
    await app.StartAsync();
    var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses.Single() ?? throw new Exception("Kestrel address missing.");
    using var playwright = await Playwright.CreateAsync();
    var chromiumPath = new[]
    {
        Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE"),
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        "/usr/bin/google-chrome-stable",
        "/usr/bin/google-chrome",
        "/usr/bin/chromium"
    }.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true, ExecutablePath = chromiumPath });
    var page = await browser.NewPageAsync();
    var errors = new List<string>();
    var logs = new List<string>();
    page.Console += (_, message) => { Console.WriteLine($"BROWSER_{message.Type}: {message.Text}"); logs.Add(message.Text); if (message.Type == "error") errors.Add(message.Text); };
    await page.AddInitScriptAsync("""
        document.addEventListener('click', e => console.log('WASM_TRACE DOM click ' + e.target.tagName), true);
        """);
    await page.RouteAsync("**/xpscript-browser.js", async route =>
    {
        var response = await route.FetchAsync();
        var module = await response.TextAsync();
        Console.WriteLine("WASM_TRACE browser module fetched");
        module = module.Replace("const dispatchUiEvent=async(eventToken,submittedValue)=>{", "const dispatchUiEvent=async(eventToken,submittedValue)=>{console.log('WASM_TRACE JS dispatch',eventToken,typeof eventDispatcher);")
            .Replace("for (const [name, value] of Object.entries(parsedHeaders)) xhr.setRequestHeader(name, String(value));", "for (const [name, value] of Object.entries(parsedHeaders)) { console.log('WASM_TRACE bridge header',name,String(value).length); xhr.setRequestHeader(name, String(value)); }")
            .Replace("const resultJson=await eventDispatcher(eventToken,submittedValue);", "console.log('WASM_TRACE invoking managed dispatcher');const resultJson=await eventDispatcher(eventToken,submittedValue);console.log('WASM_TRACE managed result',resultJson);")
            .Replace("catch{root.dataset.xpscriptError", "catch(error){console.error('WASM_TRACE dispatch failed',String(error),error?.stack);root.dataset.xpscriptError")
            .Replace("xhr.send(safeMethod", "console.log('WASM_TRACE bridge request',safeMethod,url);xhr.send(safeMethod")
            .Replace("let xhr = perform(null);", "let xhr = perform(null);console.log('WASM_TRACE bridge response',xhr.status);");
        await route.FulfillAsync(new() { Response = response, Body = module });
    });
    page.Request += (_, request) => { if (request.Url.Contains("__xpscript_bridge")) Console.WriteLine($"WASM_TRACE HTTP {request.Method} bridge"); };
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
        await status.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });
    }
    catch
    {
        Console.WriteLine("BROWSER_ERRORS=" + string.Join(" | ", errors));
        Console.WriteLine("PAGE_HTML=" + (await page.ContentAsync()).Replace('\n', ' ').Replace('\r', ' '));
        throw;
    }
    if (await status.InputValueAsync() != "CLIENT") throw new Exception("Initial WASM state missing.");
    var tabButton = page.Locator("[data-xps-tab]").First;
    await tabButton.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 30000 });
    var tabTop = await tabButton.EvaluateAsync<double>("element => element.parentElement.getBoundingClientRect().top");
    var fieldTop = await status.EvaluateAsync<double>("element => element.getBoundingClientRect().top");
    if (tabTop <= fieldTop) throw new Exception($"Bottom tab strip was rendered above the form fields (tabs={tabTop}, field={fieldTop}).");
    if (!await tabButton.EvaluateAsync<bool>("element => element.style.backgroundColor !== '' && element.style.color !== '' && element.style.fontSize === '20px' && element.style.fontWeight === '700'"))
        throw new Exception("Selected tab formatting properties were not applied in the browser renderer.");
    var detailsTab = page.Locator("[data-xps-tab='details']");
    await detailsTab.ClickAsync();
    if (!await detailsTab.EvaluateAsync<bool>("element => element.style.backgroundColor !== '' && element.style.color !== '' && element.style.fontSize === '13px' && element.classList.contains('active')"))
        throw new Exception("Tab formatting did not update after selecting another tab.");
    await page.Locator("select[name='country']").SelectOptionAsync(new[] { "Norway" });
    await page.Locator("select[name='city'] option").Filter(new LocatorFilterOptions { HasText = "Oslo" }).WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = 30000 });
    var cityOptions = await page.Locator("select[name='city'] option").AllTextContentsAsync();
    if (!cityOptions.Contains("Oslo")) throw new Exception("WASM backend partial refresh did not update city options.");
    Console.WriteLine("WASM_BACKEND_PARTIAL_REFRESH_OK");
    await page.GetByRole(AriaRole.Button, new() { Name = "Send to server" }).ClickAsync();
    try
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!logs.Any(log => log.Contains("WASM_TRACE after ServerSide", StringComparison.Ordinal)) && DateTime.UtcNow < deadline)
            await Task.Delay(100);
        if (!logs.Any(log => log.Contains("WASM_TRACE after ServerSide", StringComparison.Ordinal)))
            throw new Exception("Server-side callback did not return to the client.");
    }
    catch
    {
        Console.WriteLine("BROWSER_ERRORS=" + string.Join(" | ", errors));
        Console.WriteLine("FORM_ERROR=" + await page.Locator("#xpscript-app").GetAttributeAsync("data-xpscript-error"));
        Console.WriteLine("FORM_RESULT=" + await page.Locator("#xpscript-app").GetAttributeAsync("data-xpscript-result"));
        throw;
    }
    if (errors.Count != 0) throw new Exception("Browser errors: " + string.Join(" | ", errors));
    Console.WriteLine("WASM_PLAYWRIGHT_PAGE_OK");
    Console.WriteLine("WASM_SERVERSIDE_EVENT_OK");
}
finally
{
    if (!string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_KEEP_WASM_TEST_ROOT"), "1", StringComparison.Ordinal))
    {
        try { Directory.Delete(root, true); } catch { }
    }
    else Console.WriteLine("WASM_TEST_ROOT=" + root);
}
